using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RegCheckMcp.Resources;

namespace RegCheckMcp.Auth;

public record AuthCodePayload(
    string Username,
    string CodeChallenge,
    string CodeChallengeMethod,
    string ClientId,
    string RedirectUri,
    long ExpiresAt
);

public static class AuthCode
{
    private static readonly byte[] SigningKey =
        Encoding.UTF8.GetBytes(Secrets.Load().AuthCodeSigningKey);

    private const int ExpirySeconds = 60; // auth codes are very short-lived

    public static string Create(AuthCodePayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(json));
        var sig = Sign(payloadB64);
        return $"{payloadB64}.{sig}";
    }

    public static AuthCodePayload? Verify(string code)
    {
        var parts = code.Split('.');
        if (parts.Length != 2) return null;

        var expectedSig = Sign(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSig),
                Encoding.UTF8.GetBytes(parts[1])))
            return null;

        try
        {
            var json = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
            var payload = JsonSerializer.Deserialize<AuthCodePayload>(json);
            if (payload is null || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > payload.ExpiresAt)
                return null;
            return payload;
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

    public static AuthCodePayload NewPayload(string username, AuthSession session) => new(
        username,
        session.CodeChallenge,
        session.CodeChallengeMethod,
        session.ClientId,
        session.RedirectUri,
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ExpirySeconds);
}