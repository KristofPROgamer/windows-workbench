using System.Text.Json;

namespace Optimizer.Core;

public sealed class ChangeEngine(ActionCatalog catalog, IActionProvider provider, Func<DeviceContext> contextProvider, string stateDirectory)
{
    private string DatabasePath => Path.Combine(SafeStorage.ValidateDirectory(stateDirectory), "journal.sqlite");

    public RunResult Apply(ChangePlan plan, string approvedDigest)
    {
        ArgumentNullException.ThrowIfNull(plan);
        // Snapshot caller-owned collections before validating or using them.
        var snapshot = JsonSerializer.Deserialize<ChangePlan>(JsonSerializer.Serialize(plan, ActionCatalog.Json), ActionCatalog.Json)
            ?? throw new InvalidOperationException("The reviewed plan is missing.");
        ValidatePlan(snapshot, approvedDigest);
        var selected = snapshot.Items.Where(i => i.Status == PlanItemStatus.Ready).ToArray();
        if (selected.Length == 0) return new("", "NoChanges", snapshot.Items.Select(i => new ActionResult(i.ActionId, i.Status.ToString(), i.Reason)).ToArray());
        using var writer = SafeStorage.Acquire(stateDirectory);
        using var journal = new Journal(DatabasePath, false);
        if (journal.ContainsPlan(snapshot.PlanId)) throw new InvalidOperationException("This plan has already been used. Create a fresh assessment.");
        if (journal.HasPending()) throw new InvalidOperationException("A previous change needs recovery. Open History before applying another plan.");
        // Lock first, then repeat preflight, because another process may have changed state while waiting.
        ValidatePlan(snapshot, approvedDigest);
        var id = Guid.NewGuid().ToString("N");
        journal.Prepare(id, snapshot); // All originals committed before the first mutation.
        journal.SetRun(id, "Applying");
        var results = new List<ActionResult>();
        for (int sequence = 0; sequence < selected.Length; sequence++)
        {
            var item = selected[sequence];
            var action = catalog.Find(item.ActionId)!;
            bool mutationPrepared = false;
            try
            {
                ContextPolicy.RequireSame(snapshot.Context, contextProvider());
                RequireAllowed(action, snapshot.Context);
                RequireValue(action, item.Before!, "The setting changed after review. Nothing further was applied.");
                journal.SetAction(id, sequence, "Applying", "Original state recorded; mutation may begin.");
                mutationPrepared = true;
                provider.Write(action, item.Desired!);
                RequireValue(action, item.Desired!, "The requested setting could not be verified.");
                journal.SetAction(id, sequence, "Verified", "Requested state independently read back and verified.");
                results.Add(new(item.ActionId, "Verified", "Requested state independently read back and verified."));
            }
            catch (Exception ex)
            {
                var detail = $"Execution stopped: {ex.Message} Review recovery before continuing.";
                // Persistence failure must not cause later writes. Earlier Applying state remains recoverable.
                try { journal.SetAction(id, sequence, mutationPrepared ? "Failed" : "NotRun", detail); journal.SetRun(id, "Failed"); }
                catch (Exception journalError) { detail += $" Journal update also failed: {journalError.Message}"; }
                results.Add(new(item.ActionId, "Failed", detail));
                results.AddRange(selected.Skip(sequence + 1).Select(i => new ActionResult(i.ActionId, "NotRun", "Stopped after an earlier failure.")));
                return new(id, "Failed", results.AsReadOnly());
            }
        }
        try { journal.SetRun(id, "Succeeded"); }
        catch (Exception ex)
        {
            return new(id, "Failed", results.Append(new ActionResult("journal", "Failed", $"Changes were verified but completion could not be recorded: {ex.Message}. Use recovery history.")).ToArray());
        }
        return new(id, "Succeeded", results.AsReadOnly());
    }

    public RunResult Recover(string runId)
    {
        RequireId(runId);
        if (!File.Exists(DatabasePath)) throw new InvalidOperationException("Recovery journal does not exist.");
        using var writer = SafeStorage.Acquire(stateDirectory);
        using var journal = new Journal(DatabasePath, false);
        var run = journal.Read(runId) ?? throw new InvalidOperationException("Recovery run was not found.");
        ContextPolicy.RequireSame(run.Context, contextProvider());
        if (run.CatalogVersion != catalog.Version) throw new InvalidOperationException("This recovery needs the catalog version that created it.");
        if (run.Status == "Recovered") return new(run.Id, "Recovered", run.Actions.Select(a => new ActionResult(a.Id, a.Status, "Recovery was already completed; current settings were left unchanged.")).ToArray());
        if (!new[] { "Prepared", "Applying", "Succeeded", "Failed", "Recovering", "RecoveryBlocked" }.Contains(run.Status))
            throw new InvalidDataException("Unrecognized recovery run status.");
        if (journal.HasLaterOverlap(run)) throw new InvalidOperationException("Recover newer changes to these settings first.");
        // Validate every journal target before making any recovery write.
        foreach (var item in run.Actions)
        {
            var action = catalog.Find(item.Id) ?? throw new InvalidDataException("The journal contains an unknown action.");
            if (action.Mode != ActionMode.Preference || provider.DesiredState(action) != item.Desired)
                throw new InvalidDataException("The recovery target does not match the installed action catalog.");
            RequireAllowed(action, run.Context);
            if (!new[] { "Prepared", "Applying", "Verified", "Failed", "NotRun", "NotChanged", "Restoring", "Restored", "RecoveryConflict", "RecoveryFailed" }.Contains(item.Status))
                throw new InvalidDataException("Unrecognized recovery action status.");
        }
        journal.SetRun(run.Id, "Recovering");
        var results = new List<ActionResult>();
        bool blocked = false;
        foreach (var item in run.Actions.Reverse())
        {
            var action = catalog.Find(item.Id)!;
            // These durable states must not be replaced with RecoveryFailed if a
            // later check or journal update fails: no mutation remains to undo.
            if (item.Status is "Prepared" or "NotRun" or "NotChanged")
            {
                // Prepared/NotRun prove mutation never began. A completed restoration must
                // also stay completed if a later retry encounters an external change.
                journal.SetAction(run.Id, item.Sequence, "NotChanged", "This action was never applied; its current value was preserved.");
                results.Add(new(item.Id, "NotChanged", "This action was never applied; its current value was preserved."));
                continue;
            }
            if (item.Status == "Restored")
            {
                results.Add(new(item.Id, "Restored", "Restoration was already completed; current settings were left unchanged."));
                continue;
            }
            try
            {
                ContextPolicy.RequireSame(run.Context, contextProvider());
                RequireAllowed(action, run.Context);
                var observed = provider.Read(action);
                if (observed.Status != ObservationState.Known || observed.Value is null)
                    throw new InvalidOperationException("Current setting cannot be read; recovery made no change.");
                StateValidation.Validate(observed.Value);
                if (observed.Value == item.Before)
                {
                    journal.SetAction(run.Id, item.Sequence, "Restored", "Original state already present; no write needed.");
                    results.Add(new(item.Id, "Restored", "Original state already present; no write needed."));
                    continue;
                }
                if (observed.Value != item.Desired)
                {
                    journal.SetAction(run.Id, item.Sequence, "RecoveryConflict", "Setting changed outside this run. Its current value was preserved.");
                    results.Add(new(item.Id, "RecoveryConflict", "Setting changed outside this run. Its current value was preserved."));
                    blocked = true;
                    // Do not overwrite divergent values or continue a partially understood recovery.
                    break;
                }
                journal.SetAction(run.Id, item.Sequence, "Restoring", "Restoration prepared before mutation.");
                RequireValue(action, item.Desired, "Setting changed during recovery. Its current value was preserved.");
                provider.Write(action, item.Before);
                RequireValue(action, item.Before, "Original setting could not be verified after recovery.");
                journal.SetAction(run.Id, item.Sequence, "Restored", "Exact original value, type, or absence verified.");
                results.Add(new(item.Id, "Restored", "Exact original value, type, or absence verified."));
            }
            catch (Exception ex)
            {
                var detail = $"Recovery stopped: {ex.Message}";
                try { journal.SetAction(run.Id, item.Sequence, "RecoveryFailed", detail); }
                catch (Exception journalError) { detail += $" Journal update also failed: {journalError.Message}"; }
                results.Add(new(item.Id, "RecoveryFailed", detail));
                blocked = true;
                break;
            }
        }
        var status = blocked ? "RecoveryBlocked" : "Recovered";
        try { journal.SetRun(run.Id, status); }
        catch (Exception ex) { return new(run.Id, "RecoveryBlocked", results.Append(new ActionResult("journal", "RecoveryFailed", $"Recovery completion could not be recorded: {ex.Message}")).ToArray()); }
        return new(run.Id, status, results.AsReadOnly());
    }

    public IReadOnlyList<RunSummary> History()
    {
        if (!File.Exists(DatabasePath)) return [];
        using var journal = new Journal(DatabasePath, true);
        return journal.History();
    }

    public RunDetails? Details(string runId)
    {
        RequireId(runId);
        if (!File.Exists(DatabasePath)) return null;
        using var journal = new Journal(DatabasePath, true);
        var run = journal.Read(runId);
        return run is null ? null : new RunDetails(run.Id, run.Status, run.Context,
            run.Actions.Select(a => new HistoryAction(a.Id, catalog.Find(a.Id)?.Title ?? a.Title, a.Status, a.Before, a.Desired, a.Detail)).ToArray());
    }

    private void ValidatePlan(ChangePlan plan, string approvedDigest)
    {
        if (plan.SchemaVersion != "1.0" || plan.CatalogVersion != catalog.Version)
            throw new InvalidOperationException("The reviewed plan uses a different application or catalog version.");
        RequireId(plan.PlanId);
        if (!PlanIntegrity.Matches(plan.Digest, approvedDigest) || !PlanIntegrity.Matches(plan.Digest, PlanIntegrity.ComputeDigest(plan)))
            throw new InvalidOperationException("The reviewed plan changed or was not approved with its exact digest.");
        var now = DateTimeOffset.UtcNow;
        if (plan.CreatedUtc > now.AddMinutes(1) || plan.ExpiresUtc <= now || plan.ExpiresUtc <= plan.CreatedUtc || plan.ExpiresUtc - plan.CreatedUtc > TimeSpan.FromMinutes(30))
            throw new InvalidOperationException("The reviewed plan expired or has invalid dates. Create a fresh assessment.");
        ContextPolicy.RequireSame(plan.Context, contextProvider());
        if (plan.Items is null || plan.Items.Count > 1000 || plan.Items.Select(i => i.ActionId).Distinct(StringComparer.Ordinal).Count() != plan.Items.Count)
            throw new InvalidOperationException("Invalid or duplicated plan actions.");
        foreach (var item in plan.Items)
        {
            var action = catalog.Find(item.ActionId) ?? throw new InvalidOperationException($"Unknown action: {item.ActionId}.");
            if (item.Title != action.Title || !Enum.IsDefined(item.Status)) throw new InvalidOperationException("Action metadata differs from the installed catalog.");
            if (action.Mode != ActionMode.Preference)
            {
                if (item.Status != PlanItemStatus.Guided || item.Before is not null || item.Desired is not null)
                    throw new InvalidOperationException("Guided and external actions cannot be executed by the preference engine.");
                continue;
            }
            if (item.Status is not (PlanItemStatus.Ready or PlanItemStatus.AlreadyConfigured) || item.Before is null || item.Desired is null)
                throw new InvalidOperationException($"Action is blocked: {item.Title}. {item.Reason}");
            StateValidation.Validate(item.Before); StateValidation.Validate(item.Desired);
            RequireAllowed(action, plan.Context);
            if (provider.DesiredState(action) != item.Desired) throw new InvalidOperationException("The requested value does not match the installed action definition.");
            if ((item.Before == item.Desired) != (item.Status == PlanItemStatus.AlreadyConfigured))
                throw new InvalidOperationException("The action state does not match its reviewed classification.");
            RequireValue(action, item.Before, "The setting changed since assessment. Create a fresh plan.");
        }
    }
    private void RequireAllowed(ActionDefinition action, DeviceContext context)
    {
        var reason = provider.GetBlockReason(action, context);
        if (reason is not null) throw new InvalidOperationException(reason);
    }
    private void RequireValue(ActionDefinition action, ResourceState expected, string error)
    {
        var observed = provider.Read(action);
        if (observed.Status != ObservationState.Known || observed.Value != expected) throw new InvalidOperationException(error);
    }
    private static void RequireId(string id)
    {
        if (id is null || !Guid.TryParseExact(id, "N", out _)) throw new InvalidOperationException("Invalid plan or recovery identifier.");
    }
}
