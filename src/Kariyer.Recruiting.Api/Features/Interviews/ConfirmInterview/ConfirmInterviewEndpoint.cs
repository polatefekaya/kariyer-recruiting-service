using Kariyer.Recruiting.Api.Common.Web;

namespace Kariyer.Recruiting.Api.Features.Interviews.ConfirmInterview;

/// <summary>
/// The accept/decline links from the invitation e-mail. Anonymous by design: the candidate has no
/// account here. The GET only renders what is being answered — mail scanners follow links, so the
/// answer is never recorded until the POST behind the button on that page.
/// </summary>
public sealed class ConfirmInterviewEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("interviews/{interviewUid}/confirmation/{answer}", PreviewAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Confirmation)
            .ExcludeFromDescription()
            .WithName("PreviewInterviewConfirmation")
            .WithTags("Interviews");

        app.MapPost("interviews/{interviewUid}/confirmation/{answer}", ConfirmAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Confirmation)
            .WithName("ConfirmInterview")
            .WithTags("Interviews");
    }

    private static async Task<IResult> PreviewAsync(
        string interviewUid,
        string answer,
        string? token,
        HttpContext http,
        ConfirmInterviewHandler handler,
        CancellationToken cancellationToken)
    {
        ConfirmationView view = await handler.PreviewAsync(interviewUid, answer, token, cancellationToken);

        return Respond(view, http);
    }

    private static async Task<IResult> ConfirmAsync(
        string interviewUid,
        string answer,
        string? token,
        HttpContext http,
        ConfirmInterviewHandler handler,
        CancellationToken cancellationToken)
    {
        ConfirmationView view = await handler.ApplyAsync(
            interviewUid, answer, await FormTokenAsync(http, token, cancellationToken), cancellationToken);

        return Respond(view, http);
    }

    /// The landing page posts the token back as a form field; API callers send it in the query.
    private static async Task<string?> FormTokenAsync(HttpContext http, string? token, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(token) || !http.Request.HasFormContentType)
        {
            return token;
        }

        IFormCollection form = await http.Request.ReadFormAsync(cancellationToken);

        return form["token"].FirstOrDefault();
    }

    private static IResult Respond(ConfirmationView view, HttpContext http) =>
        WantsJson(http)
            ? Results.Json(
                new
                {
                    interviewUid = view.InterviewUid,
                    outcome = view.Outcome.ToString(),
                    confirmationStatus = view.ConfirmationStatus,
                    startsAt = view.StartsAt,
                },
                statusCode: ConfirmationPage.StatusOf(view.Outcome))
            : ConfirmationPage.Render(view);

    private static bool WantsJson(HttpContext http) =>
        http.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        && !http.Request.Headers.Accept.Any(value => value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
}
