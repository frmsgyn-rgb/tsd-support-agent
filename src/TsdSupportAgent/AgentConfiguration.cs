[Reading 50 lines from start (total: 50 lines, 0 remaining)]

using System;
using System.IO;
using System.Text.Json;

sealed class AgentConfiguration
{
    public bool communication_enabled { get; set; } = true;
    public string base_url { get; set; } = "https://agent.toservicedesk.com.br";
    public string privacy_policy_version { get; set; } = "1.0";
    public string configured_at_utc { get; set; } = "";

    public static AgentConfiguration Load(string path)
    {
        if (!File.Exists(path))
            return new AgentConfiguration();

        try
        {
            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize(
                json,
                AgentJsonContext.Default.AgentConfiguration)
                ?? new AgentConfiguration();

            cfg.base_url = NormalizeBaseUrl(cfg.base_url);
            return cfg;
        }
        catch
        {
            return new AgentConfiguration {
                communication_enabled = false,
                configured_at_utc = DateTime.UtcNow.ToString("O")
            };
        }
    }

    public static string NormalizeBaseUrl(string? value)
    {
        var url = string.IsNullOrWhiteSpace(value)
            ? "https://agent.toservicedesk.com.br"
            : value.Trim().TrimEnd('/');

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A Central deve usar HTTPS.");

        return uri.GetLeftPart(UriPartial.Authority)
            + uri.AbsolutePath.TrimEnd('/');
    }
}
