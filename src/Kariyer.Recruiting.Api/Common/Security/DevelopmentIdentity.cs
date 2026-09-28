using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Security;

/// <summary>
/// Accepts every request as a fixed company user, for local development against a seeded
/// database and no Supabase project.
///
/// Guarded twice on purpose: it is only registered when the host environment is Development AND
/// <c>Auth:DevIdentity:Enabled</c> is explicitly true, and the service refuses to start if it is
/// ever switched on outside Development. An auth bypass that could be enabled by one environment
/// variable in production is not a development convenience, it is an open door.
/// </summary>
public sealed class DevelopmentIdentityHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<Configuration.AuthOptions> auth) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentIdentity";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = [new(ClaimTypes.NameIdentifier, auth.Value.DevIdentity.ExternalId), new("sub", auth.Value.DevIdentity.ExternalId)];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, SchemeName));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
