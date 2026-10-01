using System.Text.Json;
using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Interviews.ConfirmInterview;

public enum ConfirmationOutcome
{
    /// <summary>The link is good and the interview is still answerable.</summary>
    Answerable,
    Answered,
    AlreadyAnswered,
    Expired,
    Invalid,
    NotFound,
    Closed,
}

public sealed record ConfirmationView(
    ConfirmationOutcome Outcome,
    string Answer,
    string InterviewUid,
    string? Token = null,
    string? JobTitle = null,
    string? CompanyName = null,
    DateTimeOffset? StartsAt = null,
    int? DurationMinutes = null,
    string? TimeZone = null,
    string? Type = null,
    string ConfirmationStatus = InterviewConfirmation.Pending);

/// <summary>
/// The candidate's own answer to an invitation. There is no session behind these calls — the
/// recipient of an e-mail is not signed in — so the signed token in the link is the authorisation,
/// and nothing is written until the candidate submits the form on the landing page.
/// </summary>
public sealed class ConfirmInterviewHandler(
    RecruitingDbContext db,
    IInterviewRepository interviews,
    IActivityWriter activity,
    InterviewConfirmationTokens tokens,
    IApplicationReadStore readStore,
    ICompanyDirectory directory,
    IIntegrationEventPublisher publisher,
    IOptions<RecruitingOptions> options,
    CacheInvalidator cache,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<ConfirmationView> PreviewAsync(
        string interviewUid, string answer, string? token, CancellationToken cancellationToken) =>
        ResolveAsync(interviewUid, answer, token, apply: false, cancellationToken);

    public Task<ConfirmationView> ApplyAsync(
        string interviewUid, string answer, string? token, CancellationToken cancellationToken) =>
        ResolveAsync(interviewUid, answer, token, apply: true, cancellationToken);

    private async Task<ConfirmationView> ResolveAsync(
        string interviewUid,
        string answer,
        string? token,
        bool apply,
        CancellationToken cancellationToken)
    {
        string normalized = answer.Trim().ToLowerInvariant();

        string? confirmation = normalized switch
        {
            ConfirmationAnswer.Accept => InterviewConfirmation.Accepted,
            ConfirmationAnswer.Decline => InterviewConfirmation.Declined,
            _ => null,
        };

        if (confirmation is null)
        {
            return new ConfirmationView(ConfirmationOutcome.Invalid, normalized, interviewUid);
        }

        switch (tokens.Verify(interviewUid, normalized, token))
        {
            case ConfirmationTokenResult.Invalid:
                return new ConfirmationView(ConfirmationOutcome.Invalid, normalized, interviewUid);

            case ConfirmationTokenResult.Expired:
                return new ConfirmationView(ConfirmationOutcome.Expired, normalized, interviewUid);
        }

        Interview? interview = await interviews.FindAsync(interviewUid, cancellationToken);

        if (interview is null)
        {
            return new ConfirmationView(ConfirmationOutcome.NotFound, normalized, interviewUid);
        }

        DateTimeOffset now = clock.GetUtcNow();
        ConfirmationView view = await DescribeAsync(interview, normalized, token, cancellationToken);

        if (interview.Status != InterviewStatus.Scheduled || interview.StartsAt <= now)
        {
            return view with { Outcome = ConfirmationOutcome.Closed };
        }

        if (interview.ConfirmationStatus == confirmation)
        {
            return view with { Outcome = ConfirmationOutcome.AlreadyAnswered };
        }

        if (!apply)
        {
            return view;
        }

        interview.Confirm(confirmation, now);

        activity.Write(ActivityEntry.Create(
            interview.ApplicationUid,
            interview.JobUid,
            interview.CompanyUid,
            ActivityType.InterviewUpdated,
            null,
            null,
            JsonSerializer.Serialize(new { confirmation, by = "candidate" }),
            now));

        // Staged through the outbox in the same commit as the answer, so the company side hears
        // about exactly the answers that were saved.
        await publisher.PublishAsync(await AnsweredEventAsync(interview, confirmation, view, now, cancellationToken), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateInterviewsAsync(interview.JobUid, cancellationToken);

        return view with { Outcome = ConfirmationOutcome.Answered, ConfirmationStatus = confirmation };
    }

    /// <summary>
    /// Everyone on the company side who should hear the answer: whoever created the invitation,
    /// the interviewer, and the participants — once each, and only those with an address.
    /// </summary>
    private async Task<InterviewAnsweredEvent> AnsweredEventAsync(
        Interview interview, string confirmation, ConfirmationView view, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ApplicationSummary? summary = await readStore.FindAsync(interview.ApplicationUid, interview.CompanyUid, cancellationToken);

        List<InterviewParticipantContract> recipients = [];

        foreach (string userUid in new[] { interview.CreatedBy, interview.InterviewerUid }.Distinct())
        {
            if (string.IsNullOrWhiteSpace(userUid))
            {
                continue;
            }

            CompanyMember? member = await directory.FindMemberAsync(interview.CompanyUid, userUid, cancellationToken);

            if (!string.IsNullOrWhiteSpace(member?.Email))
            {
                recipients.Add(new InterviewParticipantContract
                {
                    Email = member.Email,
                    Name = member.Name,
                    Role = userUid == interview.InterviewerUid ? "INTERVIEWER" : "RECRUITER",
                });
            }
        }

        recipients.AddRange(interview.Participants.Select(p =>
            new InterviewParticipantContract { Email = p.Email, Name = p.Name ?? string.Empty, Role = p.Role }));

        return new InterviewAnsweredEvent
        {
            MessageId = $"{interview.Uid}:{confirmation}:{now.ToUnixTimeMilliseconds()}",
            InterviewUid = interview.Uid,
            ApplicationUid = interview.ApplicationUid,
            JobUid = interview.JobUid,
            JobTitle = view.JobTitle ?? summary?.JobTitle ?? string.Empty,
            CandidateUid = interview.CandidateUid,
            CandidateName = summary is null ? string.Empty : $"{summary.CandidateName} {summary.CandidateSurname}".Trim(),
            CompanyUid = interview.CompanyUid,
            CompanyName = view.CompanyName ?? string.Empty,
            Answer = confirmation,
            StartsAt = interview.StartsAt,
            TimeZone = interview.TimeZone,
            Type = interview.Type,
            Recipients = [.. recipients.DistinctBy(r => r.Email.Trim().ToLowerInvariant())],
            CompanyReviewUrl = $"{options.Value.EmployerPortalUrl.TrimEnd('/')}/ilanlar/{Uri.EscapeDataString(interview.JobUid)}",
            AnsweredAt = now,
        };
    }

    private async Task<ConfirmationView> DescribeAsync(
        Interview interview, string answer, string? token, CancellationToken cancellationToken)
    {
        var posting = await db.CompanyJobs
            .Where(job => job.Uid == interview.JobUid)
            .Select(job => new
            {
                job.Title,
                CompanyName = db.Companies
                    .Where(company => company.Uid == job.CompanyUid)
                    .Select(company => company.CompanyName)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new ConfirmationView(
            ConfirmationOutcome.Answerable,
            answer,
            interview.Uid,
            token,
            posting?.Title,
            posting?.CompanyName,
            interview.StartsAt,
            interview.DurationMinutes,
            interview.TimeZone,
            interview.Type,
            interview.ConfirmationStatus);
    }
}

public static class ConfirmationAnswer
{
    public const string Accept = "accept";
    public const string Decline = "decline";
}
