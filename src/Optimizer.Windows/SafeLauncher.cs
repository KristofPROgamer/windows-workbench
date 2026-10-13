using System.Diagnostics;

namespace Optimizer.Windows;

/// <summary>Opens reviewed Settings destinations or publisher pages, never a command or downloaded file.</summary>
public static class SafeLauncher
{
    // Exact destinations only: query arguments and arbitrary ms-settings pages are not accepted.
    private static readonly HashSet<string> SettingsDestinations = new(StringComparer.Ordinal)
    {
        "ms-settings:search", "ms-settings:defaultapps", "ms-settings:taskbar",
        "ms-settings:personalization-start", "ms-settings:notifications", "ms-settings:quiethours",
        "ms-settings:multitasking", "ms-settings:display", "ms-settings:clipboard",
        "ms-settings:easeofaccess-display", "ms-settings:easeofaccess-visualeffects",
        "ms-settings:nightlight", "ms-settings:mousetouchpad", "ms-settings:sound",
        "ms-settings:privacy-general", "ms-settings:privacy-feedback", "ms-settings:privacy-appdiagnostics",
        "ms-settings:privacy-webcam", "ms-settings:privacy-location", "ms-settings:privacy",
        "ms-settings:optionalfeatures", "ms-settings:appsfeatures", "ms-settings:storagesense",
        "ms-settings:storagepolicies", "ms-settings:startupapps", "ms-settings:windowsupdate-options",
        "ms-settings:windowsupdate", "ms-settings:recovery"
    };

    private static readonly HashSet<string> SourceHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "learn.microsoft.com", "support.microsoft.com", "devblogs.microsoft.com", "github.com",
        "community.amd.com", "www.amd.com", "www.intel.com", "www.nvidia.com",
        "www.voidtools.com", "windirstat.net", "www.resplendence.com"
    };

    public static bool CanOpenSettings(string? uri) => uri != null && SettingsDestinations.Contains(uri);

    public static bool CanOpenSource(string? url)
    {
        if (string.IsNullOrEmpty(url) || url.Length > 4096 || url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)) || url.Contains('\\'))
            return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps && parsed.IsDefaultPort
            && string.IsNullOrEmpty(parsed.UserInfo) && SourceHosts.Contains(parsed.IdnHost)
            && !url.Contains("%0a", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("%0d", StringComparison.OrdinalIgnoreCase);
    }

    public static void OpenSettings(string uri)
    {
        if (!CanOpenSettings(uri)) throw new ArgumentException("This Settings destination is outside the reviewed allowlist.", nameof(uri));
        Open(uri);
    }

    public static void OpenSource(string url)
    {
        if (!CanOpenSource(url)) throw new ArgumentException("This source must be an HTTPS page on a reviewed publisher host.", nameof(url));
        Open(url);
    }

    private static void Open(string destination)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Opening Windows destinations requires Windows.");
        // No shell command, arguments, download, installation, or execution of source content.
        using var process = Process.Start(new ProcessStartInfo { FileName = destination, UseShellExecute = true });
    }
}
