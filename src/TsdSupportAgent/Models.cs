[Reading 89 lines from start (total: 89 lines, 0 remaining)]

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

sealed class EnrollPayload
{
    public string code { get; set; } = "";
    public string device_name { get; set; } = "";
    public string public_key_pem { get; set; } = "";
    public string key_provider { get; set; } = "";
    public string agent_version { get; set; } = "";
    public string os_name { get; set; } = "";
    public string os_build { get; set; } = "";
    public int protocol_version { get; set; }
    public string[] capabilities { get; set; } = Array.Empty<string>();
    public string privacy_policy_version { get; set; } = "1.0";
}

sealed class DeviceState
{
    public string device_id { get; set; } = "";
}

sealed class SyncPayload
{
    public string agent_version { get; set; } = "";
    public string os_name { get; set; } = "";
    public string os_build { get; set; } = "";
    public int protocol_version { get; set; }
    public string[] capabilities { get; set; } = Array.Empty<string>();
    public string privacy_policy_version { get; set; } = "1.0";
    public HealthSnapshot health { get; set; } = new();
    public InventorySnapshot? inventory { get; set; }
}

sealed class HealthSnapshot
{
    public double cpu_percent { get; set; }
    public long memory_total_mb { get; set; }
    public long memory_available_mb { get; set; }
    public long disk_c_total_mb { get; set; }
    public long disk_c_free_mb { get; set; }
    public long uptime_seconds { get; set; }
    public long agent_working_set_mb { get; set; }
    public int? antivirus_health_code { get; set; }
    public bool? antivirus_health_good { get; set; }
    public string? antivirus_health_source { get; set; }
    public bool? defender_antivirus_enabled { get; set; }
    public bool? defender_realtime_enabled { get; set; }
    public bool? defender_behavior_enabled { get; set; }
    public bool? firewall_domain_enabled { get; set; }
    public bool? firewall_private_enabled { get; set; }
    public bool? firewall_public_enabled { get; set; }
    public int? system_errors_24h { get; set; }
    public int? application_errors_24h { get; set; }
}

sealed class InventorySnapshot
{
    public HardwareSnapshot hardware { get; set; } = new();
    public List<SoftwareEntry> software { get; set; } = new();
}

sealed class HardwareSnapshot
{
    public string? manufacturer { get; set; }
    public string? model { get; set; }
    public string? serial_number { get; set; }
    public string? cpu_name { get; set; }
    public int? cpu_cores { get; set; }
    public int? cpu_logical_processors { get; set; }
}

sealed class SoftwareEntry
{
    public string name { get; set; } = "";
    public string version { get; set; } = "";
    public string publisher { get; set; } = "";
    public string architecture { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AgentConfiguration))]
[JsonSerializable(typeof(EnrollPayload))]
[JsonSerializable(typeof(DeviceState))]
[JsonSerializable(typeof(SyncPayload))]
internal partial class AgentJsonContext : JsonSerializerContext
{
}
