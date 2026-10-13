using Optimizer.Core;
using Optimizer.Windows;

// These tests exercise pure guards. They never call OpenSettings, OpenSource or registry Write.
int passed = 0, failed = 0;
void Check(string name, Func<bool> assertion)
{
    try
    {
        if (!assertion()) throw new InvalidOperationException("Assertion failed.");
        Console.WriteLine("PASS " + name);
        passed++;
    }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); failed++; }
}

var catalog = ActionCatalog.LoadDefault();
var provider = new RegistryActionProvider();
var preference = catalog.Find("explorer.extensions") ?? throw new InvalidOperationException("Missing preference.");
var context = new DeviceContext
{
    MachineId = new string('A', 64), UserId = "S-1-5-21-100-100-100-1001", WindowsBuild = 26200,
    Architecture = "X64", Edition = "Professional", IsElevated = false, IsManaged = false
};
Check("Every shipped Settings destination is reviewed", () => catalog.Actions.All(a => a.SettingsUri == null || SafeLauncher.CanOpenSettings(a.SettingsUri)));
Check("Every shipped source host is reviewed", () => catalog.Actions.All(a => a.SourceUrl == null || SafeLauncher.CanOpenSource(a.SourceUrl)));
Check("Six compiled preference IDs match the shipped catalog", () => catalog.Actions.Where(a => a.Mode == ActionMode.Preference).Select(a => a.Id).Order().SequenceEqual(new[]
{
    "appearance.transparency", "explorer.extensions", "explorer.task-view", "explorer.taskbar-left", "privacy.advertising", "privacy.suggestions"
}.Order()));
Check("Preferences have compiled desired values", () => catalog.Actions.Where(a => a.Mode == ActionMode.Preference).All(a => provider.DesiredState(a) == new ResourceState(true, "DWord", "0")));
Check("Supported current-user context passes", () => provider.GetBlockReason(preference, context) == null);
Check("Elevated process is blocked", () => provider.GetBlockReason(preference, context with { IsElevated = true }) != null);
Check("Managed device is blocked", () => provider.GetBlockReason(preference, context with { IsManaged = true }) != null);
Check("Unknown management status is blocked", () => provider.GetBlockReason(preference, context with { IsManaged = null }) != null);
Check("Missing machine identity is blocked", () => provider.GetBlockReason(preference, context with { MachineId = "" }) != null);
Check("Missing user identity is blocked", () => provider.GetBlockReason(preference, context with { UserId = " " }) != null);
Check("Windows 10 is blocked", () => provider.GetBlockReason(preference, context with { WindowsBuild = 19045 }) != null);
Check("Unknown future Windows build is blocked", () => provider.GetBlockReason(preference, context with { WindowsBuild = 99999 }) != null);
Check("ARM64 is blocked", () => provider.GetBlockReason(preference, context with { Architecture = "Arm64" }) != null);
Check("Enterprise edition is blocked", () => provider.GetBlockReason(preference, context with { Edition = "Enterprise" }) != null);
Check("Server edition is blocked", () => provider.GetBlockReason(preference, context with { Edition = "ServerStandard" }) != null);
Check("Unknown action is blocked", () => provider.GetBlockReason(preference with { Id = "arbitrary.registry" }, context) != null);
Check("Guided action cannot become a preference", () => provider.GetBlockReason(preference with { Mode = ActionMode.Guided }, context) != null);
Check("Known ID with external mode is blocked", () => provider.GetBlockReason(preference with { Mode = ActionMode.External }, context) != null);
foreach (var uri in new string?[] { null, "", "cmd.exe", "file:///C:/Windows/System32/cmd.exe", "ms-settings:taskbar?command=anything", "ms-settings:taskbar\n", " ms-settings:taskbar", "ms-settings:windowsupdate-action", "ms-settings:unsupported", "ms-settings://taskbar" })
{
    var input = uri;
    Check("Reject Settings destination " + (input ?? "<null>").Replace("\n", "[LF]"), () => !SafeLauncher.CanOpenSettings(input));
}
foreach (var url in new string?[] { null, "", "https://example.org/", "http://learn.microsoft.com/", "file:///C:/example.ps1", "javascript:alert(1)", "https://learn.microsoft.com.evil.example/", "https://learn.microsoft.com@evil.example/", "https://user:pass@learn.microsoft.com/", "https://learn.microsoft.com:8443/", "https://learn.microsoft.com\\evil.example/", "https://learn.microsoft.com./", " https://learn.microsoft.com/", "https://learn.microsoft.com/\n", "https://learn.microsoft.com/%0d%0a", "https://lеarn.microsoft.com/" })
{
    var input = url;
    Check("Reject source " + (input ?? "<null>").Replace("\n", "[LF]"), () => !SafeLauncher.CanOpenSource(input));
}
Check("Oversized source URL is blocked", () => !SafeLauncher.CanOpenSource("https://learn.microsoft.com/" + new string('a', 4096)));
Check("HTTPS source is allowed", () => SafeLauncher.CanOpenSource("https://learn.microsoft.com/en-us/windows/"));
Check("Reviewed GitHub source is allowed", () => SafeLauncher.CanOpenSource("https://github.com/Raphire/Win11Debloat"));
Console.WriteLine($"Windows guard tests: {passed} passed, {failed} failed. No Windows settings were changed.");
return failed == 0 ? 0 : 1;
