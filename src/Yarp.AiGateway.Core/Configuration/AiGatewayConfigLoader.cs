using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yarp.AiGateway.Core.Configuration;

public static partial class AiGatewayConfigLoader
{
    public static AiGatewayConfiguration Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"AI Gateway config file not found: {path}", path);

        var json = File.ReadAllText(path);
        json = ResolveEnvironmentVariables(json);

        return JsonSerializer.Deserialize<AiGatewayConfiguration>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize AI Gateway configuration.");
    }

    private static string ResolveEnvironmentVariables(string json)
    {
        return EnvVarRegex().Replace(json, match =>
        {
            var varName = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(varName) ?? string.Empty;
            return $"\"{value}\"";
        });
    }

    [GeneratedRegex(@"""env:([^""]+)""", RegexOptions.Compiled)]
    private static partial Regex EnvVarRegex();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
