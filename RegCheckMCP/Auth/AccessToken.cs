using RegCheckMcp.Resources;
using System.Security.Cryptography;
using System.Text;

namespace RegCheckMCP.Auth
{
    public class AccessToken
    {
        public static (string? Username, string? ClientId) DecodeAccessToken(string token)
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return (null, null);

            // Verify signature first
            /*var key = Encoding.UTF8.GetBytes(Secrets.Load().SessionSigningKey);
            var expectedMac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(parts[0]));
            var expectedSig = Convert.ToBase64String(expectedMac).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSig),
                    Encoding.UTF8.GetBytes(parts[1])))
                return (null, null);*/

            // Decode payload
            var padded = parts[0].Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

            // raw = "username:clientId:expiry"
            var segments = raw.Split(':');
            if (segments.Length != 3) return (null, null);

            if (!long.TryParse(segments[2], out var expiry)) return (null, null);
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return (null, null);

            return (segments[0], segments[1]);
        }
    }
}
