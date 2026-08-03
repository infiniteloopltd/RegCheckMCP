using System.Text.Json.Serialization;

namespace RegCheckMcp.Resources;

public class Secrets
{
    [JsonPropertyName("SessionSigningKey")]
    public string SessionSigningKey { get; init; } = "";

    [JsonPropertyName("AuthCodeSigningKey")]
    public string AuthCodeSigningKey { get; init; } = "";

    public static Secrets Load()
    {
        return new Secrets
        {
            AuthCodeSigningKey = "C1D5H3ZzIocWnGRuuAR8RV9oqaELXq3ughuT",
            SessionSigningKey = "OqoTLTWbDsZvmI47Itz00UhmH8EqUUZuFT6U"
        };
    }
}