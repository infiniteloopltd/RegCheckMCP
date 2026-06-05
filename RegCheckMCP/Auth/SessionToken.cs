using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RegCheckMcp.Resources;

namespace RegCheckMcp.Auth;

public record AuthSession(
    string CodeChallenge,
    string CodeChallengeMethod,
    string ClientId,
    string RedirectUri,
    string State,
    long ExpiresAt
);

public static class SessionToken
{

    private static readonly byte[] SigningKey =
        Encoding.UTF8.GetBytes(Secrets.Load().SessionSigningKey);

    private const int ExpirySeconds = 600; // 10 minutes to complete login

    public static string Create(AuthSession session)
    {
        var payload = JsonSerializer.Serialize(session);
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payload));
        var sig = Sign(payloadB64);
        return $"{payloadB64}.{sig}";
    }

    public static AuthSession? Verify(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2) return null;

        var expectedSig = Sign(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSig),
                Encoding.UTF8.GetBytes(parts[1])))
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
            var session = JsonSerializer.Deserialize<AuthSession>(json);
            if (session is null || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > session.ExpiresAt)
                return null;
            return session;
        }
        catch { return null; }
    }

    private static string Sign(string payload)
    {
        var mac = HMACSHA256.HashData(SigningKey, Encoding.UTF8.GetBytes(payload));
        return Base64Url(mac);
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s) =>
        Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));

    public static AuthSession NewSession(string codeChallenge, string codeChallengeMethod,
        string clientId, string redirectUri, string state) => new(
            codeChallenge, codeChallengeMethod, clientId, redirectUri, state,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ExpirySeconds);
}