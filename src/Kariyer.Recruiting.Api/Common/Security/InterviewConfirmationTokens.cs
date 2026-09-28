using System.Security.Cryptography;
using System.Text;
using Kariyer.Recruiting.Api.Common.Configuration;
using Microsoft.Extensions.Options;

namespace Kariyer.Recruiting.Api.Common.Security;

/// <summary>
/// Signs the one-click accept/decline links sent to candidates. They carry no session — the link
/// IS the authorisation — so the token binds the interview, the answer and an expiry under an
/// HMAC, and is verified in constant time.
/// </summary>
public sealed class InterviewConfirmationTokens(IOptions<RecruitingOptions> options, TimeProvider clock)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.ConfirmationSigningKey);

    public string Issue(string interviewUid, string answer)
    {
        long expiresAt = clock.GetUtcNow().Add(options.Value.ConfirmationLinkLifetime).ToUnixTimeSeconds();
        string payload = $"{interviewUid}:{answer}:{expiresAt}";

        return $"{expiresAt}.{Convert.ToHexStringLower(Sign(payload))}";
    }

    public ConfirmationTokenResult Verify(string interviewUid, string answer, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ConfirmationTokenResult.Invalid;
        }

        string[] parts = token.Split('.', 2);

        if (parts.Length != 2 || !long.TryParse(parts[0], out long expiresAt))
        {
            return ConfirmationTokenResult.Invalid;
        }

        byte[] expected = Sign($"{interviewUid}:{answer}:{expiresAt}");
        byte[] actual;

        try
        {
            actual = Convert.FromHexString(parts[1]);
        }
        catch (FormatException)
        {
            return ConfirmationTokenResult.Invalid;
        }

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return ConfirmationTokenResult.Invalid;
        }

        return expiresAt < clock.GetUtcNow().ToUnixTimeSeconds()
            ? ConfirmationTokenResult.Expired
            : ConfirmationTokenResult.Valid;
    }

    private byte[] Sign(string payload) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
}

public enum ConfirmationTokenResult
{
    Valid,
    Expired,
    Invalid,
}
