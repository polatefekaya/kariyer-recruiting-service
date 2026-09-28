using System.Text.Json;
using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Api.Features.Interviews.ConfirmInterview;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Recruiting.Domain.Validation;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Interviews.UpdateInterview;

public sealed class UpdateInterviewEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("interviews/{interviewUid}", UpdateAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("UpdateInterview")
            .WithTags("Interviews");

        app.MapDelete("interviews/{interviewUid}", CancelAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("CancelInterview")
            .WithTags("Interviews");
    }

    private static async Task<IResult> UpdateAsync(
        string interviewUid,
        UpdateInterviewRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        UpdateInterviewHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(interviewUid, request, company!, cancellationToken);
    }

    private static async Task<IResult> CancelAsync(
        string interviewUid,
        HttpContext http,
        CompanyContextResolver resolver,
        UpdateInterviewHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(
            interviewUid,
            new UpdateInterviewRequest { Status = InterviewStatus.Cancelled },
            company!,
            cancellationToken);
    }
}

public sealed record UpdateInterviewRequest
{
    public string? Status { get; init; }

    public string? Result { get; init; }

    public string? Type { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public int? DurationMinutes { get; init; }

    public string? TimeZone { get; init; }

    public string? Location { get; init; }

    public string? VideoUrl { get; init; }

    public string? CandidateMessage { get; init; }

    public string? Note { get; init; }

    public string? InterviewerUid { get; init; }

    public bool WantsReschedule =>
        StartsAt is not null || DurationMinutes is not null || Type is not null
        || Location is not null || VideoUrl is not null || InterviewerUid is not null;
}

public sealed class UpdateInterviewHandler(
    IInterviewRepository interviews,
    IApplicationReadStore readStore,
    ICompanyDirectory directory,
    IActivityWriter activity,
    IIntegrationEventPublisher publisher,
    CacheInvalidator cache,
    IUnitOfWork unitOfWork,
    RecruitingMetrics metrics,
    InterviewConfirmationTokens tokens,
    IOptions<RecruitingOptions> options,
    TimeProvider clock)
{
    public async Task<IResult> HandleAsync(
        string interviewUid,
        UpdateInterviewRequest request,
        CompanyContext company,
        CancellationToken cancellationToken)
    {
        Interview? interview = await interviews.FindAsync(interviewUid, cancellationToken);

        if (interview is null || !string.Equals(interview.CompanyUid, company.CompanyUid, StringComparison.Ordinal))
        {
            return ApiResults.NotFound("Mülakat bulunamadı veya erişiminiz yok.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        ApplicationSummary? summary = await readStore.FindAsync(
            interview.ApplicationUid, company.CompanyUid, cancellationToken);

        try
        {
            if (request.WantsReschedule)
            {
                InterviewDraft draft = Merge(interview, request);
                ValidationResult validation = InterviewRules.Validate(draft, now);

                if (!validation.IsValid)
                {
                    return ApiResults.Validation(validation);
                }

                DateTimeOffset previousStart = interview.StartsAt;

                interview.Reschedule(draft, company.UserUid, now);

                if (previousStart != interview.StartsAt)
                {
                    await PublishRescheduleAsync(interview, summary, company, previousStart, now, cancellationToken);
                }

                Record(ActivityType.InterviewUpdated, new { rescheduledTo = interview.StartsAt });
            }

            switch (request.Status)
            {
                case InterviewStatus.Completed:
                    interview.Complete(request.Result ?? InterviewResult.Undecided, request.Note, now);
                    Record(ActivityType.InterviewCompleted, new { result = interview.Result });
                    metrics.InterviewAction("completed");
                    break;

                case InterviewStatus.NoShow:
                    interview.MarkNoShow(now);
                    Record(ActivityType.InterviewUpdated, new { status = InterviewStatus.NoShow });
                    metrics.InterviewAction("no_show");
                    break;

                case InterviewStatus.Cancelled:
                    interview.Cancel(company.UserUid, request.CandidateMessage, now);
                    Record(ActivityType.InterviewCancelled, new { });
                    await PublishCancellationAsync(interview, summary, company, now, cancellationToken);
                    metrics.InterviewAction("cancelled");
                    break;
            }
        }
        catch (InterviewNotActiveException exception)
        {
            return ApiResults.Conflict(exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateInterviewsAsync(interview.JobUid, cancellationToken);

        IReadOnlyDictionary<string, CompanyMember> members =
            (await directory.ListMembersAsync(company.CompanyUid, cancellationToken))
            .ToDictionary(m => m.Uid, StringComparer.Ordinal);

        return Results.Ok(interview.ToDetail(members));

        void Record(string type, object metadata) => activity.Write(ActivityEntry.Create(
            interview.ApplicationUid,
            interview.JobUid,
            interview.CompanyUid,
            type,
            company.UserUid,
            company.UserName,
            JsonSerializer.Serialize(metadata),
            now));
    }

    private static InterviewDraft Merge(Interview interview, UpdateInterviewRequest request)
    {
        string type = request.Type?.Trim().ToUpperInvariant() ?? interview.Type;

        return new InterviewDraft
        {
            Type = type,
            StartsAt = request.StartsAt ?? interview.StartsAt,
            DurationMinutes = request.DurationMinutes ?? interview.DurationMinutes,
            TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? interview.TimeZone : request.TimeZone,
            VideoUrl = type == InterviewType.Video ? request.VideoUrl ?? request.Location ?? interview.VideoUrl : null,
            Location = type == InterviewType.Video ? null : request.Location ?? interview.Location,
            CandidateMessage = request.CandidateMessage ?? interview.CandidateMessage,
            InterviewerUid = request.InterviewerUid ?? interview.InterviewerUid,
            Participants = [.. interview.Participants.Select(p =>
                new InterviewParticipantDraft(p.Email, p.Role, p.Name))],
        };
    }

    private async Task PublishRescheduleAsync(
        Interview interview,
        ApplicationSummary? summary,
        CompanyContext company,
        DateTimeOffset previousStart,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new InterviewRescheduledEvent
            {
                MessageId = $"{interview.Uid}:{now.ToUnixTimeMilliseconds()}",
                InterviewUid = interview.Uid,
                ApplicationUid = interview.ApplicationUid,
                JobUid = interview.JobUid,
                JobTitle = summary?.JobTitle ?? string.Empty,
                CandidateUid = interview.CandidateUid,
                CandidateEmail = summary?.CandidateEmail ?? string.Empty,
                CandidateName = $"{summary?.CandidateName} {summary?.CandidateSurname}".Trim(),
                CompanyUid = interview.CompanyUid,
                CompanyName = company.UserName,
                PreviousStartsAt = previousStart,
                StartsAt = interview.StartsAt,
                TimeZone = interview.TimeZone,
                DurationMinutes = interview.DurationMinutes,
                Type = interview.Type,
                VideoUrl = interview.VideoUrl ?? string.Empty,
                Location = interview.Location ?? string.Empty,
                CandidateMessage = interview.CandidateMessage ?? string.Empty,
                RescheduledByName = company.UserName,
                Participants = [.. interview.Participants.Select(p =>
                    new InterviewParticipantContract { Email = p.Email, Name = p.Name ?? string.Empty, Role = p.Role })],
                AcceptUrl = ConfirmationUrl(options.Value.PublicApiUrl, interview.Uid, ConfirmationAnswer.Accept, tokens),
                DeclineUrl = ConfirmationUrl(options.Value.PublicApiUrl, interview.Uid, ConfirmationAnswer.Decline, tokens),
                RescheduledAt = now,
            },
            cancellationToken);

    private static string ConfirmationUrl(
        string apiUrl, string interviewUid, string answer, InterviewConfirmationTokens tokens) =>
        $"{apiUrl.TrimEnd('/')}/interviews/{interviewUid}/confirmation/{answer}?token={tokens.Issue(interviewUid, answer)}";

    private async Task PublishCancellationAsync(
        Interview interview,
        ApplicationSummary? summary,
        CompanyContext company,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await publisher.PublishAsync(
            new InterviewCancelledEvent
            {
                MessageId = $"{interview.Uid}:cancelled:{now.ToUnixTimeMilliseconds()}",
                InterviewUid = interview.Uid,
                ApplicationUid = interview.ApplicationUid,
                JobUid = interview.JobUid,
                JobTitle = summary?.JobTitle ?? string.Empty,
                CandidateUid = interview.CandidateUid,
                CandidateEmail = summary?.CandidateEmail ?? string.Empty,
                CandidateName = $"{summary?.CandidateName} {summary?.CandidateSurname}".Trim(),
                CompanyUid = interview.CompanyUid,
                CompanyName = company.UserName,
                StartsAt = interview.StartsAt,
                TimeZone = interview.TimeZone,
                CandidateMessage = interview.CandidateMessage ?? string.Empty,
                CancelledByName = company.UserName,
                Participants = [.. interview.Participants.Select(p =>
                    new InterviewParticipantContract { Email = p.Email, Name = p.Name ?? string.Empty, Role = p.Role })],
                CancelledAt = now,
            },
            cancellationToken);
}
