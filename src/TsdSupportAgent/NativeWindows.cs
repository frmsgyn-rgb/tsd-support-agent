[Reading 267 lines from start (total: 267 lines, 0 remaining)]

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

static class NativeWindows
{
    static readonly Lazy<(string name, string build)> OsIdentity = new(ReadOsIdentityCore);
    const uint RSMB = 0x52534D42;
    const uint RelationProcessorCore = 0;
    const uint WSC_SECURITY_PROVIDER_ANTIVIRUS = 0x4;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint GetSystemFirmwareTable(
        uint providerSignature,
        uint tableId,
        IntPtr buffer,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetLogicalProcessorInformationEx(
        uint relationshipType,
        IntPtr buffer,
        ref uint returnedLength);

    [DllImport("wscapi.dll")]
    static extern int WscGetSecurityProviderHealth(
        uint providers,
        out int health);

    public static (string name, string build) ReadOsIdentity() => OsIdentity.Value;

    static (string name, string build) ReadOsIdentityCore()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            var productName = key?.GetValue("ProductName")?.ToString()?.Trim();
            var buildText = key?.GetValue("CurrentBuildNumber")?.ToString()?.Trim()
                ?? key?.GetValue("CurrentBuild")?.ToString()?.Trim();
            var ubrObj = key?.GetValue("UBR");

            var buildNumber = 0;
            _ = int.TryParse(buildText, out buildNumber);

            if (!IsServer()
                && buildNumber >= 22000
                && !string.IsNullOrWhiteSpace(productName)
                && productName.StartsWith("Windows 10 ", StringComparison.OrdinalIgnoreCase))
            {
                productName = "Windows 11 " + productName["Windows 10 ".Length..];
            }

            var build = buildText ?? Environment.OSVersion.Version.Build.ToString();
            if (ubrObj is not null && int.TryParse(ubrObj.ToString(), out var ubr))
                build += "." + ubr;

            return (
                string.IsNullOrWhiteSpace(productName)
                    ? Environment.OSVersion.VersionString
                    : productName,
                build
            );
        }
        catch
        {
            return (
                Environment.OSVersion.VersionString,
                Environment.OSVersion.Version.Build.ToString()
            );
        }
    }

    public static (string? manufacturer, string? model, string? serial) ReadSystemIdentity()
    {
        try
        {
            var size = GetSystemFirmwareTable(RSMB, 0, IntPtr.Zero, 0);
            if (size < 16) return (null, null, null);

            var ptr = Marshal.AllocHGlobal((int)size);
            try
            {
                var written = GetSystemFirmwareTable(RSMB, 0, ptr, size);
                if (written != size) return (null, null, null);
                var raw = new byte[size];
                Marshal.Copy(ptr, raw, 0, (int)size);
                return ParseSystemIdentity(raw);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch { }

        return (null, null, null);
    }

    internal static (string? manufacturer, string? model, string? serial) ParseSystemIdentity(byte[] raw)
    {
        if (raw.Length < 8) return (null, null, null);

        var tableLength = BitConverter.ToUInt32(raw, 4);
        var end = Math.Min(raw.Length, 8 + (int)tableLength);
        var offset = 8;

        while (offset + 4 <= end)
        {
            var type = raw[offset];
            var length = raw[offset + 1];
            if (length < 4 || offset + length > end) break;

            var stringsStart = offset + length;
            var next = FindNextStructure(raw, stringsStart, end);

            if (type == 1 && length >= 8)
            {
                return (
                    GetSmbiosString(raw, stringsStart, next, raw[offset + 4]),
                    GetSmbiosString(raw, stringsStart, next, raw[offset + 5]),
                    GetSmbiosString(raw, stringsStart, next, raw[offset + 7])
                );
            }

            if (type == 127) break;
            offset = next;
        }

        return (null, null, null);
    }

    static int FindNextStructure(byte[] raw, int stringsStart, int end)
    {
        var i = stringsStart;
        while (i + 1 < end)
        {
            if (raw[i] == 0 && raw[i + 1] == 0) return i + 2;
            i++;
        }
        return end;
    }
    static string? GetSmbiosString(byte[] raw, int start, int end, byte index)
    {
        if (index == 0 || start >= end) return null;

        var current = 1;
        var pos = start;
        while (pos < end)
        {
            var zero = Array.IndexOf(raw, (byte)0, pos, end - pos);
            if (zero < 0) zero = end;
            if (current == index)
            {
                var len = zero - pos;
                if (len <= 0) return null;
                return System.Text.Encoding.ASCII.GetString(raw, pos, len).Trim();
            }

            current++;
            pos = zero + 1;
            if (pos < end && raw[pos] == 0) break;
        }
        return null;
    }

    public static (string? cpuName, int? cores, int? logical) ReadCpuIdentity()
    {
        string? name = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            name = key?.GetValue("ProcessorNameString")?.ToString()?.Trim();
        }
        catch { }

        int? cores = ReadPhysicalCoreCount();
        int? logical = Environment.ProcessorCount > 0 ? Environment.ProcessorCount : null;
        return (name, cores, logical);
    }

    static int? ReadPhysicalCoreCount()
    {
        try
        {
            uint length = 0;
            _ = GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
            if (length < 8) return null;
            var ptr = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!GetLogicalProcessorInformationEx(RelationProcessorCore, ptr, ref length))
                    return null;

                var raw = new byte[length];
                Marshal.Copy(ptr, raw, 0, (int)length);
                return ParsePhysicalCoreCount(raw);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch { return null; }
    }

    internal static int? ParsePhysicalCoreCount(byte[] raw)
    {
        var offset = 0;
        var count = 0;

        while (offset + 8 <= raw.Length)
        {
            var relationship = BitConverter.ToUInt32(raw, offset);
            var size = BitConverter.ToUInt32(raw, offset + 4);
            if (size < 8 || offset + size > raw.Length) break;
            if (relationship == RelationProcessorCore) count++;
            offset += (int)size;
        }

        return count > 0 ? count : null;
    }

    public static (int? code, bool? good, string source) ReadAntivirusHealth()
    {
        if (IsServer())
            return (null, null, "UNAVAILABLE_SERVER");

        try
        {
            var hr = WscGetSecurityProviderHealth(
                WSC_SECURITY_PROVIDER_ANTIVIRUS,
                out var health);

            if (hr == 0)
                return (health, health == 0, "WSC");

            if (hr == 1)
                return (health, health == 0, "WSC_SERVICE_NOT_RUNNING");

            return (null, null, "WSC_UNAVAILABLE");
        }
        catch
        {
            return (null, null, "WSC_UNAVAILABLE");
        }
    }

    static bool IsServer()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\ProductOptions");
            var type = key?.GetValue("ProductType")?.ToString();
            if (string.IsNullOrWhiteSpace(type)) return false;
            return !string.Equals(type, "WinNT", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
