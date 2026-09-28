using System.Text.Json;
using Kariyer.Recruiting.Api.Common.Caching;
using Kariyer.Recruiting.Api.Common.Security;
using Kariyer.Recruiting.Api.Common.Web;
using Kariyer.Recruiting.Domain.Activity;
using Kariyer.Recruiting.Domain.Notes;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Recruiting.Domain.Validation;

namespace Kariyer.Recruiting.Api.Features.Applications.SaveNote;

public sealed class SaveNoteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("applications/{applicationUid}/note", HandleAsync)
            .RequireAuthorization(AuthenticationExtensions.CompanyPolicy)
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName("SaveApplicationNote")
            .WithTags("Applications");

    private static async Task<IResult> HandleAsync(
        string applicationUid,
        SaveNoteRequest request,
        HttpContext http,
        CompanyContextResolver resolver,
        SaveNoteHandler handler,
        CancellationToken cancellationToken)
    {
        (CompanyContext? company, IResult? failure) = await resolver.ResolveAsync(http, cancellationToken);

        return failure ?? await handler.HandleAsync(applicationUid, request, company!, cancellationToken);
    }
}

public sealed record SaveNoteRequest(string? Body);

public sealed record NoteResponse(
    string ApplicationUid, string? Body, string? AuthorUid, string? AuthorName, DateTimeOffset? UpdatedAt);

public sealed class SaveNoteHandler(
    IApplicationReadStore readStore,
    IApplicationNoteRepository notes,
    IActivityWriter activity,
    CacheInvalidator cache,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IResult> HandleAsync(
        string applicationUid,
        SaveNoteRequest request,
        CompanyContext company,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = NoteRules.Validate(request.Body);

        if (!validation.IsValid)
        {
            return ApiResults.Validation(validation);
        }

        ApplicationSummary? summary = await readStore.FindAsync(applicationUid, company.CompanyUid, cancellationToken);

        if (summary is null)
        {
            return ApiResults.NotFound();
        }

        string? body = NoteRules.Normalize(request.Body);
        DateTimeOffset now = clock.GetUtcNow();
        ApplicationNote? note = await notes.FindAsync(applicationUid, cancellationToken);

        if (body is null)
        {
            if (note is not null)
            {
                notes.Remove(note);
                WriteActivity(ActivityType.NoteCleared, summary, company, now, null);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await cache.InvalidateJobAsync(summary.JobUid, cancellationToken);

            return Results.Ok(new NoteResponse(applicationUid, null, null, null, null));
        }

        if (note is null)
        {
            note = ApplicationNote.Create(
                summary.ApplicationUid,
                summary.JobUid,
                summary.CompanyUid,
                summary.CandidateUid,
                body,
                company.UserUid,
                now);

            notes.Add(note);
        }
        else
        {
            note.Edit(body, company.UserUid, now);
        }

        WriteActivity(ActivityType.NoteSaved, summary, company, now, body.Length);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateJobAsync(summary.JobUid, cancellationToken);

        return Results.Ok(new NoteResponse(applicationUid, note.Body, company.UserUid, company.UserName, note.UpdatedAt));

        void WriteActivity(string type, ApplicationSummary application, CompanyContext actor, DateTimeOffset at, int? length) =>
            activity.Write(ActivityEntry.Create(
                application.ApplicationUid,
                application.JobUid,
                application.CompanyUid,
                type,
                actor.UserUid,
                actor.UserName,
                JsonSerializer.Serialize(new { length }),
                at));
    }
}
