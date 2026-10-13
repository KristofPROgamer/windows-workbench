using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Optimizer.Core;

namespace Optimizer.Windows;

/// <summary>Local, read-only inventory. Failures are observations, never evidence of absence.</summary>
public sealed class WindowsAssessmentService
{
    private const string WindowsVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public DeviceContext GetContext()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows assessment requires Windows.");
        var nativeVersion = new NativeMethods.OsVersion { Size = (uint)Marshal.SizeOf<NativeMethods.OsVersion>(), ServicePack = string.Empty };
        int build = NativeMethods.RtlGetVersion(ref nativeVersion) == 0 ? checked((int)nativeVersion.Build) : 0;
        string edition = "Unknown", machineId = string.Empty;
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var versionKey = machine.OpenSubKey(WindowsVersionPath);
            edition = versionKey?.GetValue("EditionID") as string ?? "Unknown";
            using var identityKey = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var rawIdentity = identityKey?.GetValue("MachineGuid") as string;
            if (!string.IsNullOrWhiteSpace(rawIdentity))
                machineId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("WindowsWorkbench:v1:" + rawIdentity)));
        }
        catch (Exception ex) when (IsObservationFailure(ex)) { /* Empty identity blocks all writes. */ }
        using var identity = WindowsIdentity.GetCurrent();
        return new DeviceContext
        {
            MachineId = machineId,
            UserId = identity.User?.Value ?? string.Empty,
            WindowsBuild = build,
            Edition = edition,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            IsElevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),
            IsManaged = DetectManagement()
        };
    }

    public Assessment Assess()
    {
        var context = GetContext();
        var facts = new List<AssessmentFact>
        {
            new("Windows", "Edition", context.Edition, context.Edition == "Unknown" ? ObservationState.Unknown : ObservationState.Known),
            new("Windows", "Build", context.WindowsBuild > 0 ? context.WindowsBuild.ToString(CultureInfo.InvariantCulture) : "Unknown", context.WindowsBuild > 0 ? ObservationState.Known : ObservationState.Unknown),
            Known("Windows", "Architecture", context.Architecture),
            Known("Safety", "Execution scope", context.IsElevated ? "Elevated — preference changes blocked" : "Current user, not elevated"),
            new("Safety", "Management indicators", context.IsManaged switch { true => "Management or organizational registration detected", false => "No management indicators detected", _ => "Unknown — preference changes blocked" },
                context.IsManaged.HasValue ? ObservationState.Known : ObservationState.Unknown,
                "Domain role, Entra/workplace join and native MDM registration checks. Absence of indicators is not a full enterprise-policy audit."),
            Known("Safety", "Native preference coverage", "Preview configuration only", "Allowlisted x64 consumer Home/Pro builds; visible effects and hardware compatibility still require lab qualification.")
        };
        Capture(facts, "Windows", "Revision", () =>
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = root.OpenSubKey(WindowsVersionPath);
            if (key?.GetValue("UBR") is not int revision) throw new IOException("Update revision is unavailable.");
            return [Known("Windows", "Revision", context.WindowsBuild + "." + revision)];
        });
        Capture(facts, "Processor", "CPU", () =>
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            if (string.IsNullOrWhiteSpace(name)) throw new IOException("Processor information is unavailable.");
            return [Known("Processor", "CPU", name.Trim()), Known("Processor", "Logical processors", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture), "Processors available to this process; group/affinity limits may apply.")];
        });
        Capture(facts, "Memory", "Physical memory", ReadMemory);
        Capture(facts, "Memory", "Commit usage", ReadCommit);
        Capture(facts, "Storage", "Fixed volumes", ReadDrives);
        Capture(facts, "Network", "Active interfaces", ReadNetwork);
        Capture(facts, "Power", "Power source", ReadPower);
        Capture(facts, "Power", "Active power plan", ReadPowerPlan);
        Capture(facts, "Display", "Primary refresh rate", ReadDisplay);
        Capture(facts, "Startup", "Startup entries", ReadStartup);
        Capture(facts, "Applications", "Desktop application entries", ReadApplications);
        Capture(facts, "Windows", "Restart indicators", ReadRestart);
        foreach (string service in new[] { "Spooler", "WSearch", "SysMain", "WinDefend", "EventLog", "BITS", "wuauserv" })
            Capture(facts, "Services", service, () => ReadService(service));
        return new(context, DateTimeOffset.UtcNow, facts.AsReadOnly());
    }

    private static bool? DetectManagement()
    {
        IntPtr domain = IntPtr.Zero, join = IntPtr.Zero;
        bool unknown = false;
        try
        {
            if (NativeMethods.DsRoleGetPrimaryDomainInformation(null, 1, out domain) != 0 || domain == IntPtr.Zero) unknown = true;
            else if (Marshal.ReadInt32(domain) != 0) return true; // Only a standalone workstation qualifies.
            if (NativeMethods.NetGetAadJoinInformation(null, out join) != 0) unknown = true;
            else if (join != IntPtr.Zero)
            {
                int type = Marshal.ReadInt32(join);
                if (type is 1 or 2) return true;
                unknown = true;
            }
            // EnrollmentState exists for Windows' own providers too; registry-key presence
            // is not evidence of device management. Ask the documented registration API.
            // A null UPN buffer explicitly requests only the boolean, preserving account privacy.
            if (NativeMethods.IsDeviceRegisteredWithManagement(out bool registered, 0, IntPtr.Zero) != 0) unknown = true;
            else if (registered) return true;
            return unknown ? null : false;
        }
        catch (Exception ex) when (IsObservationFailure(ex) || ex is EntryPointNotFoundException or DllNotFoundException) { return null; }
        finally
        {
            if (domain != IntPtr.Zero) NativeMethods.DsRoleFreeMemory(domain);
            if (join != IntPtr.Zero) NativeMethods.NetFreeAadJoinInformation(join);
        }
    }

    private static IEnumerable<AssessmentFact> ReadMemory()
    {
        var status = new NativeMethods.MemoryStatus { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatus>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status)) throw new IOException("Memory API failed.");
        return [Known("Memory", "Installed usable RAM", GiB(status.TotalPhysical)), Known("Memory", "Available physical memory", GiB(status.AvailablePhysical)),
            Known("Memory", "Memory load", status.Load + "%", "A point-in-time snapshot; free RAM alone does not measure performance.")];
    }

    private static IEnumerable<AssessmentFact> ReadCommit()
    {
        var info = new NativeMethods.PerformanceInfo { Size = (uint)Marshal.SizeOf<NativeMethods.PerformanceInfo>() };
        if (!NativeMethods.GetPerformanceInfo(ref info, info.Size)) throw new IOException("Performance information API failed.");
        ulong page = info.PageSize.ToUInt64();
        return [Known("Memory", "System committed memory", GiB(checked(info.CommitTotal.ToUInt64() * page)) + " / " + GiB(checked(info.CommitLimit.ToUInt64() * page)), "Current system commitment and limit; this is not a pagefile sizing recommendation."),
            Known("Processes", "Running processes", info.Processes.ToString(CultureInfo.InvariantCulture), "A count is inventory, not an optimization score.")];
    }

    private static IEnumerable<AssessmentFact> ReadDrives()
    {
        var result = new List<AssessmentFact>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            try
            {
                if (!drive.IsReady) { result.Add(new("Storage", drive.Name, "Unavailable", ObservationState.Unavailable)); continue; }
                result.Add(Known("Storage", "Volume " + drive.Name, GiB((ulong)drive.AvailableFreeSpace) + " available / " + GiB((ulong)drive.TotalSize),
                    "Available to this user; no volume labels, folder names, or file inventory collected."));
            }
            catch (Exception ex) when (IsObservationFailure(ex)) { result.Add(new("Storage", drive.Name, "Unavailable", ObservationState.Unavailable, ex.GetType().Name)); }
        }
        return result.Count > 0 ? result : [new("Storage", "Fixed volumes", "None reported", ObservationState.Unknown)];
    }

    private static IEnumerable<AssessmentFact> ReadNetwork()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces().Where(a => a.OperationalStatus == OperationalStatus.Up && a.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
        int dnsCount = 0;
        foreach (var adapter in adapters) if (adapter.GetIPProperties().DnsAddresses.Count > 0) dnsCount++;
        return [Known("Network", "Active interfaces", adapters.Length.ToString(CultureInfo.InvariantCulture), "Includes virtual/VPN interfaces. Names, IP addresses, DNS addresses, MACs and routes are excluded."),
            Known("Network", "Interfaces with DNS servers", dnsCount.ToString(CultureInfo.InvariantCulture), "Read-only configuration count; no resolver queries or connectivity probes were sent.")];
    }

    private static IEnumerable<AssessmentFact> ReadPower()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var power)) throw new IOException("Power status API failed.");
        var facts = new List<AssessmentFact>
        {
            new("Power", "Power source", power.AcLineStatus switch { 0 => "Battery / DC", 1 => "AC", _ => "Unknown" }, power.AcLineStatus is 0 or 1 ? ObservationState.Known : ObservationState.Unknown)
        };
        if (power.BatteryFlag == 255) facts.Add(new("Power", "Battery", "Unknown", ObservationState.Unknown));
        else if ((power.BatteryFlag & 128) != 0) facts.Add(new("Power", "Battery", "No system battery reported", ObservationState.NotApplicable));
        else facts.Add(new("Power", "Battery charge", power.BatteryLifePercent == 255 ? "Unknown" : power.BatteryLifePercent + "%", power.BatteryLifePercent == 255 ? ObservationState.Unknown : ObservationState.Known));
        return facts;
    }

    private static IEnumerable<AssessmentFact> ReadPowerPlan()
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            if (NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out pointer) != 0 || pointer == IntPtr.Zero) throw new IOException("Active scheme API failed.");
            var scheme = Marshal.PtrToStructure<Guid>(pointer);
            string name = scheme.ToString().ToLowerInvariant() switch
            {
                "381b4222-f694-41f0-9685-ff5bb260df2e" => "Balanced",
                "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "High performance",
                "a1841308-3541-4fab-bc81-f71556f20b4a" => "Power saver",
                _ => "Custom / vendor plan"
            };
            return [Known("Power", "Active power plan", name, "Plan identity does not reveal the separate Windows power-mode overlay or prove performance.")];
        }
        finally { if (pointer != IntPtr.Zero) NativeMethods.LocalFree(pointer); }
    }

    private static IEnumerable<AssessmentFact> ReadDisplay()
    {
        var mode = new NativeMethods.DisplayMode { DeviceName = string.Empty, FormName = string.Empty, Size = (ushort)Marshal.SizeOf<NativeMethods.DisplayMode>() };
        if (!NativeMethods.EnumDisplaySettings(null, -1, ref mode)) throw new IOException("Display mode API failed.");
        return [new("Display", "Primary refresh rate", mode.DisplayFrequency > 1 ? mode.DisplayFrequency + " Hz" : "Default / unknown", mode.DisplayFrequency > 1 ? ObservationState.Known : ObservationState.Unknown,
            "Current primary mode only; maximum supported rate, VRR and all-monitor coverage need further assessment.")];
    }

    private static IEnumerable<AssessmentFact> ReadStartup()
    {
        int registryCount = 0, folderCount = 0;
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                if (hive == RegistryHive.CurrentUser && view == RegistryView.Registry32) continue;
                using var root = RegistryKey.OpenBaseKey(hive, view);
                foreach (var subkey in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" })
                {
                    using var key = root.OpenSubKey(subkey);
                    registryCount += key?.ValueCount ?? 0;
                }
            }
        foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
        {
            string path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path)) throw new IOException("A startup folder could not be resolved.");
            try { folderCount += Directory.EnumerateFiles(path).Count(p => !Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)); }
            catch (DirectoryNotFoundException) { /* A missing startup folder contains no startup files. Other errors remain unavailable. */ }
        }
        return [Known("Startup", "Run / RunOnce entries", registryCount.ToString(CultureInfo.InvariantCulture), "Entry count includes disabled entries. Scheduled tasks, services and packaged startup tasks are outside this inventory."),
            Known("Startup", "Startup folder files", folderCount.ToString(CultureInfo.InvariantCulture), "Names and command lines are not collected.")];
    }

    private static IEnumerable<AssessmentFact> ReadApplications()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall == null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var app = uninstall.OpenSubKey(name);
                    if (app == null) throw new IOException("Application inventory changed while being read.");
                    if (app.GetValue("DisplayName") is string displayName && !string.IsNullOrWhiteSpace(displayName) && app.GetValue("SystemComponent") is not 1)
                        names.Add(displayName);
                }
            }
        return [Known("Applications", "Desktop application entries", names.Count.ToString(CultureInfo.InvariantCulture), "Deduplicated visible uninstall names, current user and machine. Store apps and portable applications are outside this count; names are not exported.")];
    }

    private static IEnumerable<AssessmentFact> ReadRestart()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var servicing = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
        using var updates = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
        using var session = root.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
        bool pending = servicing != null || updates != null || session?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 };
        return [Known("Windows", "Restart indicators", pending ? "Restart indicator present" : "No checked indicator present", "Checks servicing, Windows Update and pending file operations; other restart requirements may exist.")];
    }

    private static IEnumerable<AssessmentFact> ReadService(string service)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + service);
        if (key == null) return [new("Services", service, "Not installed / unavailable", ObservationState.Unknown)];
        if (key.GetValue("Start") is not int mode) throw new IOException("Service start setting unavailable.");
        string name = mode switch { 0 => "Boot", 1 => "System", 2 => "Automatic", 3 => "Manual / trigger start", 4 => "Disabled", _ => "Unknown" };
        return [new("Services", service, name, mode is >= 0 and <= 4 ? ObservationState.Known : ObservationState.Unknown, "Configured startup only; this does not indicate whether the service is running.")];
    }

    private static void Capture(List<AssessmentFact> facts, string category, string name, Func<IEnumerable<AssessmentFact>> reader)
    {
        try { facts.AddRange(reader()); }
        catch (Exception ex) when (IsObservationFailure(ex) || ex is EntryPointNotFoundException or DllNotFoundException or OverflowException)
        { facts.Add(new(category, name, "Unavailable", ObservationState.Unavailable, ex.GetType().Name + ": read-only inventory could not complete.")); }
    }

    private static AssessmentFact Known(string category, string name, string value, string detail = "") => new(category, name, value, ObservationState.Known, detail);
    private static string GiB(ulong bytes) => (bytes / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GiB";
    private static bool IsObservationFailure(Exception ex) => ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception;
}
