using System.Runtime.InteropServices;

namespace Optimizer.Windows;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct OsVersion
    {
        internal uint Size, Major, Minor, Build, Platform;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string ServicePack;
    }
    [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
    internal static extern int RtlGetVersion(ref OsVersion version);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatus
    {
        internal uint Length, Load;
        internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PerformanceInfo
    {
        internal uint Size;
        internal UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        internal uint Handles, Processes, Threads;
    }
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPerformanceInfo(ref PerformanceInfo info, uint size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PowerStatus
    {
        internal byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        internal uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("powrprof.dll")]
    internal static extern uint PowerGetActiveScheme(IntPtr rootKey, out IntPtr scheme);
    [DllImport("kernel32.dll")]
    internal static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint DsRoleGetPrimaryDomainInformation(string? server, int level, out IntPtr buffer);
    [DllImport("netapi32.dll")]
    internal static extern void DsRoleFreeMemory(IntPtr buffer);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern int NetGetAadJoinInformation(string? tenantId, out IntPtr joinInfo);
    [DllImport("netapi32.dll")]
    internal static extern void NetFreeAadJoinInformation(IntPtr joinInfo);

    [DllImport("MDMRegistration.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int IsDeviceRegisteredWithManagement([MarshalAs(UnmanagedType.Bool)] out bool registered, uint upnCapacity, IntPtr upn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string DeviceName;
        internal ushort SpecVersion, DriverVersion, Size, DriverExtra;
        internal uint Fields;
        internal int PositionX, PositionY;
        internal uint DisplayOrientation, DisplayFixedOutput;
        internal short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string FormName;
        internal ushort LogPixels;
        internal uint BitsPerPel, PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency, IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplaySettings(string? deviceName, int mode, ref DisplayMode displayMode);
}
