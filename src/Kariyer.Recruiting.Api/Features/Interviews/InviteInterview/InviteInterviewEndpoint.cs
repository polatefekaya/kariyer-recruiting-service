using System.Text.Json;
using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Telemetry;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Api.Features.Interviews.ConfirmInterview;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Applications;
using Kariyer.Recruiting.Domain.Interviews;
using Kariyer.Recruiting.Domain.Pipeline;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Recruiting.Domain.Validation;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Features.Interviews.InviteInterview;

public sealed class InviteInterviewEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("applications/{applicationUid}/interviews", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("InviteInterview")
            .WithTags("Interviews");

    private static async Task<IResult> HandleAsync(
        string applicationUid,
        InviteInterviewRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        InviteInterviewHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(applicationUid, request, company!, cancellationToken);
    }
}

public sealed record InviteInterviewRequest(
    string Type,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? TimeZone,
    string? VideoUrl,
    string? Location,
    string? CandidateMessage,
    string? InternalNote,
    string? InterviewerUid,
    IReadOnlyList<ParticipantRequest>? Participants);

public sealed record ParticipantRequest(string Email, string Role, string? Name);

public sealed record InterviewResponse(
    string Id,
    string ApplicationUid,
    string Type,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string TimeZone,
    string? VideoUrl,
    string? Location,
    string Status,
    string ConfirmationStatus,
    string? Result,
    string? InternalNote,
    IReadOnlyList<ParticipantRequest> Participants);

public sealed class InviteInterviewHandler(
    IApplicationReadStore readStore,
    IInterviewRepository interviews,
    IApplicationPipelineRepository pipelines,
    IActivityWriter activity,
    IIntegrationEventPublisher publisher,
    ICompanyDirectory directory,
    CacheInvalidator cache,
    IUnitOfWork unitOfWork,
    RecruitingMetrics metrics,
    InterviewConfirmationTokens tokens,
    IOptions<RecruitingOptions> options,
    TimeProvider clock)
{
    public async Task<IResult> HandleAsync(
        string applicationUid,
        InviteInterviewRequest request,
        CompanyContext company,
        CancellationToken cancellationToken)
    {
        ApplicationSummary? summary = await readStore.FindAsync(applicationUid, company.CompanyUid, cancellationToken);

        if (summary is null)
        {
            return ApiResults.NotFound();
        }

        DateTimeOffset now = clock.GetUtcNow();

        InterviewDraft draft = new()
        {
            Type = request.Type?.Trim().ToUpperInvariant() ?? InterviewType.Video,
            StartsAt = request.StartsAt,
            DurationMinutes = request.DurationMinutes,
            TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Europe/Istanbul" : request.TimeZone,
            VideoUrl = request.VideoUrl,
            Location = request.Location,
            CandidateMessage = request.CandidateMessage,
            InterviewerUid = request.InterviewerUid ?? company.UserUid,
            Participants = [.. (request.Participants ?? []).Select(p =>
                new InterviewParticipantDraft(p.Email, p.Role?.Trim().ToUpperInvariant() ?? string.Empty, p.Name))],
        };

        ValidationResult validation = InterviewRules.Validate(draft, now);

        if (!validation.IsValid)
        {
            return ApiResults.Validation(validation);
        }

        ApplicationPipeline pipeline = await pipelines.FindAsync(applicationUid, cancellationToken)
            ?? StartPipeline(summary, now);

        List<StageChanged> moves = [];

        if (pipeline.Stage != ApplicationStage.Interview)
        {
            try
            {
                // Inviting someone IS the decision to review them, so a stage that cannot reach
                // INTERVIEW directly (NEW) walks through REVIEWING rather than refusing the
                // invitation and making the recruiter click twice for one intent. Both steps are
                // logged, so the pipeline still shows the review happened.
                if (!StageTransitions.IsAllowed(pipeline.Stage, ApplicationStage.Interview)
                    && StageTransitions.IsAllowed(pipeline.Stage, ApplicationStage.Reviewing))
                {
                    StageChanged implied = pipeline.MoveTo(ApplicationStage.Reviewing, company.UserUid, null, now);

                    moves.Add(implied);

                    activity.Write(ActivityEntry.Create(
                        summary.ApplicationUid,
                        summary.JobUid,
                        summary.CompanyUid,
                        ActivityType.StageChanged,
                        company.UserUid,
                        company.UserName,
                        JsonSerializer.Serialize(new { from = implied.FromStage, to = implied.ToStage, via = "interview_invite" }),
                        now));
                }

                moves.Add(pipeline.MoveTo(ApplicationStage.Interview, company.UserUid, null, now));
            }
            catch (InvalidStageTransitionException exception)
            {
                return ApiResults.InvalidTransition(exception);
            }
        }

        string uid = $"{Guid.CreateVersion7()}-interview";

        Interview interview = Interview.Schedule(
            uid,
            summary.ApplicationUid,
            summary.JobUid,
            summary.CompanyUid,
            summary.CandidateUid,
            draft,
            company.UserUid,
            request.InternalNote,
            now);

        interviews.Add(interview);

        activity.Write(ActivityEntry.Create(
            summary.ApplicationUid,
            summary.JobUid,
            summary.CompanyUid,
            ActivityType.InterviewCreated,
            company.UserUid,
            company.UserName,
            JsonSerializer.Serialize(new { interviewUid = uid, startsAt = interview.StartsAt, type = interview.Type }),
            now));

        activity.Write(ActivityEntry.Create(
            summary.ApplicationUid,
            summary.JobUid,
            summary.CompanyUid,
            ActivityType.StageChanged,
            company.UserUid,
            company.UserName,
            JsonSerializer.Serialize(new
            {
                from = moves.Count > 0 ? moves[^1].FromStage : ApplicationStage.Interview,
                to = ApplicationStage.Interview,
                via = "interview_invite",
            }),
            now));

        string companyName = await directory.FindCompanyNameAsync(summary.CompanyUid, cancellationToken)
            ?? company.UserName;

        // Every move is announced, including the walk through REVIEWING: analytics computes
        // time-to-first-review from these, and a stage that only ever appears in the activity log
        // is invisible to it. Mail stays silent on INTERVIEW — the invitation below is the one
        // message the candidate gets.
        foreach (StageChanged move in moves)
        {
            await publisher.PublishAsync(
                new ApplicationStageChangedEvent
                {
                    MessageId = $"{summary.ApplicationUid}:{move.ToStage}:{now.ToUnixTimeMilliseconds()}",
                    ApplicationUid = summary.ApplicationUid,
                    JobUid = summary.JobUid,
                    JobTitle = summary.JobTitle,
                    CandidateUid = summary.CandidateUid,
                    CompanyUid = summary.CompanyUid,
                    FromStage = move.FromStage,
                    ToStage = move.ToStage,
                    ActorUid = company.UserUid,
                    ChangedAt = now,
                },
                cancellationToken);

            metrics.StageChanged(move.FromStage, move.ToStage);
        }

        await publisher.PublishAsync(
            new InterviewInvitedEvent
            {
                MessageId = uid,
                InterviewUid = uid,
                ApplicationUid = summary.ApplicationUid,
                JobUid = summary.JobUid,
                JobTitle = summary.JobTitle,
                CandidateUid = summary.CandidateUid,
                CandidateEmail = summary.CandidateEmail ?? string.Empty,
                CandidateName = $"{summary.CandidateName} {summary.CandidateSurname}".Trim(),
                CompanyUid = summary.CompanyUid,
                CompanyName = companyName,
                StartsAt = interview.StartsAt,
                TimeZone = interview.TimeZone,
                DurationMinutes = interview.DurationMinutes,
                Type = interview.Type,
                VideoUrl = interview.VideoUrl ?? string.Empty,
                Location = interview.Location ?? string.Empty,
                CandidateMessage = interview.CandidateMessage ?? string.Empty,
                InvitedByName = company.UserName,
                Participants = [.. interview.Participants.Select(p =>
                    new InterviewParticipantContract { Email = p.Email, Name = p.Name ?? string.Empty, Role = p.Role })],
                AcceptUrl = ConfirmationUrl(options.Value.PublicApiUrl, uid, ConfirmationAnswer.Accept, tokens),
                DeclineUrl = ConfirmationUrl(options.Value.PublicApiUrl, uid, ConfirmationAnswer.Decline, tokens),
                InvitedAt = now,
            },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateInterviewsAsync(summary.JobUid, cancellationToken);

        metrics.InterviewAction("created");

        return Results.Created($"/api/recruiting/interviews/{uid}", interview.ToResponse());

        ApplicationPipeline StartPipeline(ApplicationSummary application, DateTimeOffset at)
        {
            ApplicationPipeline created = ApplicationPipeline.Start(
                application.ApplicationUid,
                application.JobUid,
                application.CompanyUid,
                application.CandidateUid,
                LegacyStageMapping.ToStage(application.LegacyStatus),
                at);

            pipelines.Add(created);

            return created;
        }
    }

    /// The link carries its own signature: the candidate has no session, so the token is what
    /// authorises the answer.
    private static string ConfirmationUrl(
        string apiUrl, string interviewUid, string answer, InterviewConfirmationTokens tokens) =>
        $"{apiUrl.TrimEnd('/')}/interviews/{interviewUid}/confirmation/{answer}?token={tokens.Issue(interviewUid, answer)}";
}

public static class InterviewMapping
{
    public static InterviewResponse ToResponse(this Interview interview) => new(
        interview.Uid,
        interview.ApplicationUid,
        interview.Type,
        interview.StartsAt,
        interview.DurationMinutes,
        interview.TimeZone,
        interview.VideoUrl,
        interview.Location,
        interview.Status,
        interview.ConfirmationStatus,
        interview.Result,
        interview.InternalNote,
        [.. interview.Participants.Select(p => new ParticipantRequest(p.Email, p.Role, p.Name))]);
}
