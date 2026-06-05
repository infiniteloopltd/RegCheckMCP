using System.Text.Json.Serialization;

namespace RegCheckMcp.Resources;

public class Secrets
{
    [JsonPropertyName("SessionSigningKey")]
    public string SessionSigningKey { get; init; } = "";

    [JsonPropertyName("AuthCodeSigningKey")]
    public string AuthCodeSigningKey { get; init; } = "";

    public static Secrets Load() => EmbeddedResource.LoadJson<Secrets>("secrets.json");
}