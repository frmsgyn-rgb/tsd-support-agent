[Reading 39 lines from start (total: 39 lines, 0 remaining)]

using System;
using System.IO;
using System.Text.Json;

sealed class SetupConfiguration
{
    public bool communication_enabled { get; set; } = true;
    public string base_url { get; set; } = "https://agent.toservicedesk.com.br";
    public string privacy_policy_version { get; set; } = PrivacyPolicyText.Version;
    public string configured_at_utc { get; set; } = "";

    public static SetupConfiguration Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new SetupConfiguration();
            return JsonSerializer.Deserialize<SetupConfiguration>(File.ReadAllText(path))
                ?? new SetupConfiguration();
        }
        catch
        {
            return new SetupConfiguration();
        }
    }

    public static void Save(string path, bool communicationEnabled)
    {
        var cfg = new SetupConfiguration {
            communication_enabled = communicationEnabled,
            base_url = "https://agent.toservicedesk.com.br",
            privacy_policy_version = PrivacyPolicyText.Version,
            configured_at_utc = DateTime.UtcNow.ToString("O")
        };

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
    }
}
