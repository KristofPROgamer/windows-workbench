using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Optimizer.Core;

public static class PlanIntegrity
{
    public static string ComputeDigest(ChangePlan plan) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan with { Digest = "" }, ActionCatalog.Json))));

    internal static bool Matches(string? left, string? right) => left is not null && right is not null
        && left.Length == 64 && right.Length == 64
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
}

public sealed class Planner(ActionCatalog catalog, IActionProvider provider)
{
    public ChangePlan Create(DeviceContext context, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ids);
        var selected = ids.ToArray();
        if (selected.Length > 1000 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
            throw new InvalidOperationException("Select each action only once.");
        foreach (var id in selected)
            if (string.IsNullOrEmpty(id) || catalog.Find(id) is null) throw new InvalidOperationException($"Unknown action: {id}.");
        var items = new List<PlanItem>();
        foreach (var id in selected)
        {
            var action = catalog.Find(id)!;
            if (action.Mode != ActionMode.Preference)
            {
                items.Add(new(id, action.Title, PlanItemStatus.Guided, null, null, action.Summary));
                continue;
            }
            try
            {
                var reason = ContextPolicy.BlockReason(context) ?? provider.GetBlockReason(action, context);
                if (reason is not null) { items.Add(new(id, action.Title, PlanItemStatus.Blocked, null, null, reason)); continue; }
                var observed = provider.Read(action);
                if (observed.Status != ObservationState.Known || observed.Value is null)
                {
                    items.Add(new(id, action.Title, PlanItemStatus.Blocked, null, null, $"{observed.Status}: {observed.Detail}"));
                    continue;
                }
                StateValidation.Validate(observed.Value);
                var desired = provider.DesiredState(action);
                StateValidation.Validate(desired);
                items.Add(new(id, action.Title, observed.Value == desired ? PlanItemStatus.AlreadyConfigured : PlanItemStatus.Ready,
                    observed.Value, desired, observed.Value == desired ? "The requested setting is already present." : "Ready for review."));
            }
            catch (Exception ex) { items.Add(new(id, action.Title, PlanItemStatus.Blocked, null, null, $"Assessment failed: {ex.Message}")); }
        }
        var now = DateTimeOffset.UtcNow;
        var plan = new ChangePlan("1.0", catalog.Version, Guid.NewGuid().ToString("N"), now, now.AddMinutes(30), context, items.AsReadOnly(), "");
        return plan with { Digest = PlanIntegrity.ComputeDigest(plan) };
    }
}

internal static class ContextPolicy
{
    internal static string? BlockReason(DeviceContext context)
    {
        if (string.IsNullOrWhiteSpace(context.MachineId) || string.IsNullOrWhiteSpace(context.UserId)) return "Machine or original-user identity is unavailable.";
        if (context.IsElevated) return "Run the application without administrator elevation to preserve the original user's settings.";
        if (context.IsManaged != false) return "Device management could not be ruled out. Personal preferences are read-only.";
        if (context.WindowsBuild < 22000) return "This Windows build is not supported.";
        return null;
    }
    internal static void RequireSame(DeviceContext saved, DeviceContext current)
    {
        if (saved != current) throw new InvalidOperationException("Device, user, Windows version, or management state changed. Create a fresh plan.");
        var reason = BlockReason(current);
        if (reason is not null) throw new InvalidOperationException(reason);
    }
}

internal static class StateValidation
{
    internal static void Validate(ResourceState state)
    {
        if (state is null || string.IsNullOrWhiteSpace(state.Kind) || state.Kind.Length > 64 || state.Data?.Length > 65536)
            throw new InvalidDataException("Invalid resource state.");
        if (!state.Exists && (state.Kind != "None" || state.Data is not null)) throw new InvalidDataException("Invalid absent resource state.");
        if (state.Exists && (state.Kind == "None" || state.Data is null)) throw new InvalidDataException("Present resources need a kind and data.");
    }
}
