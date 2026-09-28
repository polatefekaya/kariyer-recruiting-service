using System.Text.Json;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Microsoft.EntityFrameworkCore;

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

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateInterviewsAsync(interview.JobUid, cancellationToken);

        return view with { Outcome = ConfirmationOutcome.Answered, ConfirmationStatus = confirmation };
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
