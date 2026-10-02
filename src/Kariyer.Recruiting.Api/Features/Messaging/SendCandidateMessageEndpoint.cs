using System.Text.Json;
using Kariyer.Messaging.Contracts.Recruiting;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Messaging;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Api.Features.Messaging;

public sealed class SendCandidateMessageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("jobs/{jobUid}/messages", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("SendCandidateMessage")
            .WithTags("Messaging");

    private static async Task<IResult> HandleAsync(
        string jobUid,
        SendCandidateMessageRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        SendCandidateMessageHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(jobUid, request, company!, cancellationToken);
    }
}

public sealed record SendCandidateMessageRequest(IReadOnlyList<string>? ApplicationUids, string? Subject, string? Body);

/// <summary>An applicant the message did not go to, and why.</summary>
public sealed record MessageSkip(string ApplicationUid, string Reason);

public sealed record SendCandidateMessageResponse(
    string MessageUid,
    IReadOnlyList<MessageRecipientResponse> Sent,
    IReadOnlyList<MessageSkip> Skipped);

/// <summary>
/// Sends one message to the applicants the company picked. The recipients are the ones asked for
/// that belong to this posting and have an address; the rest come back as skipped rather than
/// failing the send. The message row, a MESSAGE_SENT activity entry per recipient and the
/// CandidatesMessagedEvent commit together through the outbox — the mail service sends the mail.
/// </summary>
public sealed class SendCandidateMessageHandler(
    IApplicationReadStore readStore,
    ICandidateMessageRepository messages,
    IActivityWriter activity,
    IIntegrationEventPublisher publisher,
    ICompanyDirectory directory,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string NotFound = "NOT_FOUND";

    public const string NoEmail = "NO_EMAIL";

    public async Task<IResult> HandleAsync(
        string jobUid,
        SendCandidateMessageRequest request,
        CompanyContext company,
        CancellationToken cancellationToken)
    {
        string[] uids = [.. (request.ApplicationUids ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal)];

        ValidationResult validation = MessageRules.Validate(request.Subject, request.Body, uids.Length);

        if (!validation.IsValid)
        {
            return ApiResults.Validation(validation);
        }

        if (!await readStore.JobBelongsToCompanyAsync(jobUid, company.CompanyUid, cancellationToken))
        {
            return ApiResults.NotFound("İlan bulunamadı veya erişiminiz yok.");
        }

        IReadOnlyList<MessageAudienceRow> found =
            await readStore.AudienceAsync(jobUid, company.CompanyUid, null, uids, cancellationToken);

        Dictionary<string, MessageAudienceRow> byUid = found.ToDictionary(r => r.ApplicationUid, StringComparer.Ordinal);
        List<MessageAudienceRow> recipients = [];
        List<MessageSkip> skipped = [];

        foreach (string applicationUid in uids)
        {
            if (!byUid.TryGetValue(applicationUid, out MessageAudienceRow? row))
            {
                skipped.Add(new MessageSkip(applicationUid, NotFound));
            }
            else if (string.IsNullOrWhiteSpace(row.CandidateEmail))
            {
                skipped.Add(new MessageSkip(applicationUid, NoEmail));
            }
            else
            {
                recipients.Add(row);
            }
        }

        if (recipients.Count == 0)
        {
            return ApiResults.Validation("applicationUids", "Seçilen adayların hiçbirine e-posta gönderilemiyor.");
        }

        DateTimeOffset now = clock.GetUtcNow();
        string uid = $"{Guid.CreateVersion7()}-message";
        string jobTitle = await readStore.FindAsync(recipients[0].ApplicationUid, company.CompanyUid, cancellationToken)
            is { } summary ? summary.JobTitle : string.Empty;

        CandidateMessage message = CandidateMessage.Create(
            uid, jobUid, company.CompanyUid, request.Subject, request.Body!, company.UserUid, company.UserName, recipients.Count, now);

        messages.Add(message);

        string metadata = JsonSerializer.Serialize(new { messageUid = uid, subject = message.Subject });

        foreach (MessageAudienceRow recipient in recipients)
        {
            activity.Write(ActivityEntry.Create(
                recipient.ApplicationUid, jobUid, company.CompanyUid, ActivityType.MessageSent,
                company.UserUid, company.UserName, metadata, now));
        }

        await publisher.PublishAsync(
            new CandidatesMessagedEvent
            {
                MessageId = uid,
                JobUid = jobUid,
                JobTitle = jobTitle,
                CompanyUid = company.CompanyUid,
                CompanyName = await directory.FindCompanyNameAsync(company.CompanyUid, cancellationToken) ?? string.Empty,
                SenderName = company.UserName,
                Subject = message.Subject ?? string.Empty,
                Body = message.Body,
                Recipients =
                [
                    .. recipients.Select(r => new MessageRecipientContract
                    {
                        ApplicationUid = r.ApplicationUid,
                        CandidateUid = r.CandidateUid,
                        Email = r.CandidateEmail!,
                        Name = $"{r.CandidateName} {r.CandidateSurname}".Trim(),
                    }),
                ],
                SentAt = now,
            },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(new SendCandidateMessageResponse(
            uid,
            [.. recipients.Select(r => (r with { LastMessagedAt = now, MessageCount = r.MessageCount + 1 }).ToResponse())],
            skipped));
    }
}
