using System.Globalization;
using Microsoft.Win32;
using Optimizer.Core;

namespace Optimizer.Windows;

/// <summary>Current-user DWORD preferences only. No caller-supplied registry paths.</summary>
public sealed class RegistryActionProvider : IActionProvider
{
    private sealed record Target(string Path, string Name);
    private static readonly IReadOnlyDictionary<string, Target> Targets = new Dictionary<string, Target>(StringComparer.Ordinal)
    {
        ["explorer.extensions"] = new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt"),
        ["explorer.taskbar-left"] = new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAl"),
        ["explorer.task-view"] = new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton"),
        ["appearance.transparency"] = new(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency"),
        ["privacy.suggestions"] = new(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled"),
        ["privacy.advertising"] = new(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled")
    };

    public Observation Read(ActionDefinition action)
    {
        if (!Targets.TryGetValue(action.Id, out var target) || action.Mode != ActionMode.Preference)
            return new(ObservationState.NotApplicable, null, "This action has no native preference provider.");
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = root.OpenSubKey(target.Path, writable: false);
            if (key == null || !key.GetValueNames().Contains(target.Name, StringComparer.OrdinalIgnoreCase))
                return new(ObservationState.Known, ResourceState.Missing, "No explicit value; Windows or policy may supply a default.");
            if (key.GetValueKind(target.Name) != RegistryValueKind.DWord)
                return new(ObservationState.Unknown, null, "The existing value has an unexpected type. It will not be replaced.");
            if (key.GetValue(target.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not int number)
                return new(ObservationState.Unknown, null, "The value changed while being read.");
            return new(ObservationState.Known, new(true, "DWord", unchecked((uint)number).ToString(CultureInfo.InvariantCulture)),
                "Explicit current-user configuration; this does not prove visible Windows behavior.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return new(ObservationState.Unavailable, null, "The current-user value could not be read (" + ex.GetType().Name + ").");
        }
    }

    public ResourceState DesiredState(ActionDefinition action)
    {
        RequireTarget(action);
        return new(true, "DWord", "0");
    }

    public string? GetBlockReason(ActionDefinition action, DeviceContext context)
    {
        if (action.Mode != ActionMode.Preference || !Targets.ContainsKey(action.Id)) return "No compiled provider for this action.";
        if (context.IsElevated) return "Close this elevated instance and reopen normally to preserve the original user's scope.";
        if (context.IsManaged != false) return "Device management status is managed or unknown. Only assessment and guidance are available.";
        if (context.MachineId.Length != 64 || string.IsNullOrWhiteSpace(context.UserId)) return "Device or user identity could not be established.";
        if (context.Architecture != "X64") return "Native changes are currently limited to x64 Windows.";
        // Release families confirmed against Microsoft release health on 2026-10-10.
        // This is a scope guard, not a claim of VM/hardware qualification.
        if (context.WindowsBuild is not (26100 or 26200 or 26300)) return "This Windows build is outside the preview configuration allowlist.";
        if (context.Edition is not ("Core" or "CoreN" or "CoreSingleLanguage" or "Professional" or "ProfessionalN"))
            return "Native changes are currently limited to consumer Home and Pro editions.";
        return null;
    }

    public void Write(ActionDefinition action, ResourceState state)
    {
        var target = RequireTarget(action);
        // Independently check the actual process and device, even if a caller bypasses the planner.
        var reason = GetBlockReason(action, new WindowsAssessmentService().GetContext());
        if (reason != null) throw new InvalidOperationException(reason);
        uint number = 0;
        if (state.Exists)
        {
            if (state.Kind != "DWord" || !uint.TryParse(state.Data, NumberStyles.None, CultureInfo.InvariantCulture, out number))
                throw new InvalidOperationException("Only captured DWORD values can be written by this provider.");
        }
        else if (state.Kind != "None" || state.Data != null)
            throw new InvalidOperationException("An absent value must use the canonical missing state.");
        var current = Read(action);
        if (current.Status != ObservationState.Known) throw new InvalidOperationException("Current value could not be safely read.");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        if (state.Exists)
        {
            using var key = root.CreateSubKey(target.Path, writable: true) ?? throw new IOException("The preference key could not be opened.");
            key.SetValue(target.Name, unchecked((int)number), RegistryValueKind.DWord);
            key.Flush();
        }
        else
        {
            // Restore absence without deleting a key that may contain other applications' values.
            using var key = root.OpenSubKey(target.Path, writable: true);
            key?.DeleteValue(target.Name, throwOnMissingValue: false);
            key?.Flush();
        }
    }

    private static Target RequireTarget(ActionDefinition action) =>
        action.Mode == ActionMode.Preference && Targets.TryGetValue(action.Id, out var target)
            ? target : throw new InvalidOperationException("Action is not in the compiled preference allowlist.");
}
