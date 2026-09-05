using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string AgentVersion = "0.4.0-oss";
const int AgentProtocolVersion = 1;
string[] AgentCapabilities = [
    "health.v1",
    "inventory.native.v1",
    "antivirus.wsc.v1",
    "events.summary.v1",
    "release.trust.v1",
    "communication.toggle.v1"
];
const string KeyName = "TSD-SUPPORT-AGENT-DEVICE-KEY-V1";
if (args.Length == 1 && args[0].Equals("probe", StringComparison.OrdinalIgnoreCase))
{
    var probePayload = new SyncPayload {
        agent_version = AgentVersion,
        os_name = OsName(),
        os_build = OsBuild(),
        protocol_version = AgentProtocolVersion,
        capabilities = AgentCapabilities,
        health = WinMetrics.Read(),
        inventory = Inventory.Read()
    };
    Console.WriteLine(JsonSerializer.Serialize(
        probePayload,
        AgentJsonContext.Default.SyncPayload));
    return;
}

if (args.Length == 1 && args[0].Equals("release-key-info", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"release_key_id={ReleaseTrust.KeyId}");
    return;
}

if (args.Length == 4 && args[0].Equals("verify-release", StringComparison.OrdinalIgnoreCase))
{
    var manifestJson = File.ReadAllText(args[1]).Trim();
    var signatureB64 = File.ReadAllText(args[2]).Trim();

    if (!ReleaseTrust.VerifyManifest(manifestJson, signatureB64))
    {
        Console.Error.WriteLine("RELEASE_SIGNATURE_INVALID");
        Environment.ExitCode = 3;
        return;
    }

    if (!ReleaseTrust.VerifyBinary(manifestJson, args[3], out var reason))
    {
        Console.Error.WriteLine($"RELEASE_BINARY_INVALID {reason}");
        Environment.ExitCode = 4;
        return;
    }

    Console.WriteLine("RELEASE_SIGNATURE_OK");
    Console.WriteLine("RELEASE_BINARY_OK");
    return;
}

var stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TSD", "SupportAgent");
var stateFile = Path.Combine(stateDir, "state.json");
var enrollmentFile = Path.Combine(stateDir, "enrollment.code");
var onlineMarkerFile = Path.Combine(stateDir, "online.ok");
var inventoryMarkerFile = Path.Combine(stateDir, "inventory.last");
var configFile = Path.Combine(stateDir, "config.json");
var logDir = Path.Combine(stateDir, "logs");
var agentLogFile = Path.Combine(logDir, "agent.log");
Directory.CreateDirectory(stateDir);
Directory.CreateDirectory(logDir);
AgentFileLog.Initialize(agentLogFile);
AgentFileLog.Write($"START version={AgentVersion} user={WindowsIdentity.GetCurrent().Name}");

ECDsa? deviceKey = null;
HttpClient? httpClient = null;
bool communicationDisabledLogged = false;

ECDsa GetDeviceKey()
{
    if (deviceKey is not null) return deviceKey;
    deviceKey = OpenOrCreateKey();
    if (deviceKey is ECDsaCng ecKey)
        AgentFileLog.Write($"KEY_PROVIDER {ecKey.Key.Provider?.Provider ?? "unknown"}");
    return deviceKey;
}

string GetKeyProviderName(ECDsa key) =>
    key is ECDsaCng ecKey ? (ecKey.Key.Provider?.Provider ?? "unknown") : "unknown";

HttpClient GetHttpClient() =>
    httpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

AgentConfiguration CurrentConfig() => AgentConfiguration.Load(configFile);

if (args.Length == 0) {
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(o => o.ServiceName = "TSD Support Agent");
    builder.Services.AddSingleton<Func<Task>>(() => ServiceCycle());
    builder.Services.AddHostedService<AgentWorker>();
    await builder.Build().RunAsync();
    return;
}

Console.WriteLine("Uso: TsdSupportAgent.exe [probe | release-key-info | verify-release <manifest> <signature> <binary>]");

ECDsa OpenOrCreateKey()
{
    foreach (var provider in new[] { CngProvider.MicrosoftPlatformCryptoProvider, CngProvider.MicrosoftSoftwareKeyStorageProvider })
    {
        try
        {
            if (CngKey.Exists(KeyName, provider, CngKeyOpenOptions.MachineKey))
                return new ECDsaCng(CngKey.Open(KeyName, provider, CngKeyOpenOptions.MachineKey));

            var cp = new CngKeyCreationParameters {
                Provider = provider,
                KeyUsage = CngKeyUsages.Signing,
                ExportPolicy = CngExportPolicies.None,
                KeyCreationOptions = CngKeyCreationOptions.MachineKey
            };
            var key = CngKey.Create(CngAlgorithm.ECDsaP256, KeyName, cp);
            Console.WriteLine("KEY_PROVIDER=" + provider.Provider);
            return new ECDsaCng(key);
        }
        catch (Exception ex)
        {
            AgentFileLog.Write($"KSP_FAIL provider={provider.Provider}", ex);
        }
    }
    throw new InvalidOperationException("Nao foi possivel criar/abrir chave ECDSA.");
}

string OsName() => NativeWindows.ReadOsIdentity().name;

string OsBuild() => NativeWindows.ReadOsIdentity().build;

async Task ServiceCycle()
{
    var cfg = CurrentConfig();
    if (!cfg.communication_enabled)
    {
        if (!communicationDisabledLogged)
        {
            AgentFileLog.Write("COMMUNICATION_DISABLED");
            communicationDisabledLogged = true;
        }
        return;
    }

    communicationDisabledLogged = false;

    if (!File.Exists(stateFile))
    {
        await BootstrapEnrollment();
        return;
    }

    await Sync(InventoryDue());
}

bool InventoryDue()
{
    try
    {
        if (!File.Exists(inventoryMarkerFile)) return true;
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(inventoryMarkerFile);
        return age >= TimeSpan.FromHours(24);
    }
    catch
    {
        return true;
    }
}

async Task Enroll(string code)
{
    AgentFileLog.Write("ENROLL_START");
    var cfg = CurrentConfig();
    if (!cfg.communication_enabled)
        throw new InvalidOperationException("communication_disabled");

    var ecdsa = GetDeviceKey();
    var http = GetHttpClient();
    var baseUrl = cfg.base_url;

    var payload = new EnrollPayload {
        code = code,
        device_name = Environment.MachineName,
        public_key_pem = ecdsa.ExportSubjectPublicKeyInfoPem(),
        key_provider = GetKeyProviderName(ecdsa),
        agent_version = AgentVersion,
        os_name = OsName(),
        os_build = OsBuild(),
        protocol_version = AgentProtocolVersion,
        capabilities = AgentCapabilities,
        privacy_policy_version = cfg.privacy_policy_version
    };
    using var response = await http.PostAsJsonAsync(
        baseUrl + "/api/agent/v1/enroll",
        payload,
        AgentJsonContext.Default.EnrollPayload);
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"HTTP={(int)response.StatusCode}");
    Console.WriteLine(body);
    AgentFileLog.Write($"ENROLL_HTTP {(int)response.StatusCode}");
    response.EnsureSuccessStatusCode();

    using var doc = JsonDocument.Parse(body);
    var deviceId = doc.RootElement.GetProperty("device_id").GetString()!;
    File.WriteAllText(
        stateFile,
        JsonSerializer.Serialize(
            new DeviceState { device_id = deviceId },
            AgentJsonContext.Default.DeviceState));
    AgentFileLog.Write($"ENROLL_OK device_id={deviceId}");
    Console.WriteLine("STATE_SAVED=" + stateFile);
}

async Task Sync(bool includeInventory)
{
    var cfg = CurrentConfig();
    if (!cfg.communication_enabled)
        throw new InvalidOperationException("communication_disabled");

    if (!File.Exists(stateFile))
        throw new InvalidOperationException("state.json ausente.");

    var ecdsa = GetDeviceKey();
    var http = GetHttpClient();
    var baseUrl = cfg.base_url;

    using var stateDoc = JsonDocument.Parse(File.ReadAllText(stateFile));
    var deviceId = stateDoc.RootElement.GetProperty("device_id").GetString()!;

    var payload = new SyncPayload {
        agent_version = AgentVersion,
        os_name = OsName(),
        os_build = OsBuild(),
        protocol_version = AgentProtocolVersion,
        capabilities = AgentCapabilities,
        privacy_policy_version = cfg.privacy_policy_version,
        health = WinMetrics.Read(),
        inventory = includeInventory ? Inventory.Read() : null
    };
    var raw = JsonSerializer.Serialize(payload, AgentJsonContext.Default.SyncPayload);
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
    var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    var canonical = $"POST\n/api/agent/v1/sync\n{timestamp}\n{nonce}\n{bodyHash}";
    var signature = Convert.ToBase64String(ecdsa.SignData(
        Encoding.UTF8.GetBytes(canonical),
        HashAlgorithmName.SHA256,
        DSASignatureFormat.Rfc3279DerSequence));

    using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/agent/v1/sync");
    req.Content = new StringContent(raw, Encoding.UTF8, "application/json");
    req.Headers.Add("X-TSD-Device-ID", deviceId);
    req.Headers.Add("X-TSD-Timestamp", timestamp);
    req.Headers.Add("X-TSD-Nonce", nonce);
    req.Headers.Add("X-TSD-Signature", signature);

    using var response = await http.SendAsync(req);
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"HTTP={(int)response.StatusCode}");
    Console.WriteLine(body);
    response.EnsureSuccessStatusCode();
    if (!File.Exists(onlineMarkerFile)) {
        File.WriteAllText(onlineMarkerFile, DateTime.UtcNow.ToString("O"));
        AgentFileLog.Write("FIRST_SYNC_OK");
    }
    if (includeInventory)
    {
        File.WriteAllText(inventoryMarkerFile, DateTime.UtcNow.ToString("O"));
        AgentFileLog.Write("INVENTORY_SYNC_OK");
    }
    if (File.Exists(enrollmentFile)) {
        File.Delete(enrollmentFile);
        AgentFileLog.Write("ENROLLMENT_CODE_REMOVED_AFTER_SYNC");
    }
}

async Task BootstrapEnrollment()
{
    if (!File.Exists(enrollmentFile))
        throw new InvalidOperationException("enrollment_code_missing");

    var code = (await File.ReadAllTextAsync(enrollmentFile)).Trim();
    if (code.Length < 8)
        throw new InvalidOperationException("enrollment_code_invalid");

    await Enroll(code);
    await Sync(true);
}

sealed class WinMetrics
{
    static DateTime _eventCacheAtUtc = DateTime.MinValue;
    static int? _systemErrorsCache;
    static int? _applicationErrorsCache;

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatusEx {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct FileTime {
        public uint Low;
        public uint High;
        public ulong Value => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll")]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll")]
    static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    public static HealthSnapshot Read()
    {
        var mem = new MemoryStatusEx();
        mem.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        GlobalMemoryStatusEx(ref mem);

        var cpu = ReadCpu();
        var c = new DriveInfo("C");
        var proc = Process.GetCurrentProcess();
        var antivirus = NativeWindows.ReadAntivirusHealth();
        var errors = ReadEventCountsCached();

        return new HealthSnapshot {
            cpu_percent = Math.Round(cpu, 2),
            memory_total_mb = (long)(mem.ullTotalPhys / 1024 / 1024),
            memory_available_mb = (long)(mem.ullAvailPhys / 1024 / 1024),
            disk_c_total_mb = c.IsReady ? c.TotalSize / 1024 / 1024 : 0,
            disk_c_free_mb = c.IsReady ? c.AvailableFreeSpace / 1024 / 1024 : 0,
            uptime_seconds = Environment.TickCount64 / 1000,
            agent_working_set_mb = proc.WorkingSet64 / 1024 / 1024,
            antivirus_health_code = antivirus.code,
            antivirus_health_good = antivirus.good,
            antivirus_health_source = antivirus.source,
            defender_antivirus_enabled = null,
            defender_realtime_enabled = null,
            defender_behavior_enabled = null,
            firewall_domain_enabled = ReadFirewall("DomainProfile"),
            firewall_private_enabled = ReadFirewall("StandardProfile"),
            firewall_public_enabled = ReadFirewall("PublicProfile"),
            system_errors_24h = errors.system,
            application_errors_24h = errors.application
        };
    }

    static double ReadCpu()
    {
        if (!GetSystemTimes(out var i1, out var k1, out var u1)) return 0;
        Thread.Sleep(350);
        if (!GetSystemTimes(out var i2, out var k2, out var u2)) return 0;
        var idle = i2.Value - i1.Value;
        var total = (k2.Value - k1.Value) + (u2.Value - u1.Value);
        if (total == 0) return 0;
        return Math.Max(0, Math.Min(100, 100.0 * (total - idle) / total));
    }

    static bool? ReadFirewall(string profile)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\" + profile);
            var value = key?.GetValue("EnableFirewall");
            return value is null ? null : Convert.ToInt32(value) != 0;
        }
        catch { return null; }
    }

    static (int? system, int? application) ReadEventCountsCached()
    {
        var now = DateTime.UtcNow;
        if ((now - _eventCacheAtUtc) < TimeSpan.FromMinutes(10))
            return (_systemErrorsCache, _applicationErrorsCache);

        _systemErrorsCache = CountErrors("System");
        _applicationErrorsCache = CountErrors("Application");
        _eventCacheAtUtc = now;
        return (_systemErrorsCache, _applicationErrorsCache);
    }

    static int? CountErrors(string logName)
    {
        try
        {
            var xpath = "*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= 86400000]]]";
            var query = new EventLogQuery(logName, PathType.LogName, xpath);
            using var reader = new EventLogReader(query);
            var count = 0;
            while (count < 5000)
            {
                using var evt = reader.ReadEvent();
                if (evt is null) break;
                count++;
            }
            return count;
        }
        catch { return null; }
    }
}

sealed class Inventory
{
    public static InventorySnapshot Read()
    {
        var system = NativeWindows.ReadSystemIdentity();
        var cpu = NativeWindows.ReadCpuIdentity();

        var software = new List<SoftwareEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadSoftware(RegistryView.Registry64, "x64", software, seen);
        ReadSoftware(RegistryView.Registry32, "x86", software, seen);

        return new InventorySnapshot {
            hardware = new HardwareSnapshot {
                manufacturer = system.manufacturer,
                model = system.model,
                serial_number = system.serial,
                cpu_name = cpu.cpuName,
                cpu_cores = cpu.cores,
                cpu_logical_processors = cpu.logical
            },
            software = software
        };
    }

    static void ReadSoftware(RegistryView view, string arch, List<SoftwareEntry> output, HashSet<string> seen)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null) return;

            foreach (var subName in uninstall.GetSubKeyNames())
            {
                using var app = uninstall.OpenSubKey(subName);
                var name = app?.GetValue("DisplayName")?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Convert.ToInt32(app?.GetValue("SystemComponent", 0) ?? 0) == 1) continue;

                var version = app?.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "";
                var publisher = app?.GetValue("Publisher")?.ToString()?.Trim() ?? "";
                var dedup = $"{name}\0{version}\0{publisher}\0{arch}";
                if (!seen.Add(dedup)) continue;

                output.Add(new SoftwareEntry {
                    name = name,
                    version = version,
                    publisher = publisher,
                    architecture = arch
                });
            }
        }
        catch { }
    }
}


sealed class AgentWorker : BackgroundService
{
    readonly Func<Task> _sync;
    readonly Microsoft.Extensions.Logging.ILogger<AgentWorker> _log;

    public AgentWorker(Func<Task> sync, Microsoft.Extensions.Logging.ILogger<AgentWorker> log)
    {
        _sync = sync;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _sync();
                failures = 0;
            }
            catch (Exception ex)
            {
                failures++;
                _log.LogWarning(ex, "Agent sync failed");
                AgentFileLog.Write($"SYNC_FAIL failures={failures}", ex);
            }

            var delaySeconds = failures switch {
                0 => 60,
                1 => 15,
                2 => 30,
                3 => 60,
                4 => 120,
                _ => 300
            };

            try { await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }
}

static class AgentFileLog
{
    static readonly object Gate = new();
    static string? PathValue;

    public static void Initialize(string path)
    {
        PathValue = path;
        try { Write("LOG_INIT"); } catch { }
    }

    public static void Write(string message, Exception? ex = null)
    {
        var path = PathValue;
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (Gate)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                {
                    var old = path + ".1";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                }

                using var sw = new StreamWriter(path, append: true, Encoding.UTF8);
                sw.Write(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                sw.Write(" ");
                sw.Write(message);
                if (ex is not null)
                {
                    sw.Write(" | ");
                    sw.Write(ex.GetType().FullName);
                    sw.Write(" | ");
                    sw.Write(ex.Message.Replace("\r", " ").Replace("\n", " "));
                }
                sw.WriteLine();
            }
            catch { }
        }
    }
}
