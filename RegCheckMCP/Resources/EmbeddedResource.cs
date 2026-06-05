using System.Reflection;
using System.Text.Json;

namespace RegCheckMcp.Resources;

public static class EmbeddedResource
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    public static string LoadText(string endsWith)
    {
        var name = FindName(endsWith);
        using var stream = Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static T LoadJson<T>(string endsWith)
    {
        var json = LoadText(endsWith);
        return JsonSerializer.Deserialize<T>(json)
               ?? throw new InvalidOperationException($"Failed to deserialize embedded resource '{endsWith}'.");
    }

    private static string FindName(string endsWith)
    {
        return Assembly.GetManifestResourceNames()
                   .FirstOrDefault(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(
                   $"Embedded resource ending in '{endsWith}' not found. " +
                   $"Available resources: {string.Join(", ", Assembly.GetManifestResourceNames())}");
    }
}