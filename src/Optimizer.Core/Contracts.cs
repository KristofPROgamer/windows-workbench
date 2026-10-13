namespace Optimizer.Core;

public enum ActionMode { Preference, Guided, External }
public enum ObservationState { Known, Unknown, Unavailable, NotApplicable }
public enum PlanItemStatus { Ready, AlreadyConfigured, Blocked, Guided }

public sealed record ActionDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required string Benefit { get; init; }
    public required string Tradeoff { get; init; }
    public ActionMode Mode { get; init; }
    public string? SettingsUri { get; init; }
    public string? SourceUrl { get; init; }
    public string[] Profiles { get; init; } = [];
}

public sealed record DeviceContext
{
    public required string MachineId { get; init; }
    public required string UserId { get; init; }
    public required int WindowsBuild { get; init; }
    public required string Edition { get; init; }
    public required string Architecture { get; init; }
    public bool IsElevated { get; init; }
    public bool? IsManaged { get; init; }
}

public sealed record ResourceState(bool Exists, string Kind, string? Data)
{
    public static ResourceState Missing => new(false, "None", null);
}
public sealed record Observation(ObservationState Status, ResourceState? Value, string Detail);
public interface IActionProvider
{
    Observation Read(ActionDefinition action);
    ResourceState DesiredState(ActionDefinition action);
    string? GetBlockReason(ActionDefinition action, DeviceContext context);
    void Write(ActionDefinition action, ResourceState state);
}
public sealed record AssessmentFact(string Category, string Name, string Value, ObservationState Status, string Detail = "");
public sealed record Assessment(DeviceContext Context, DateTimeOffset CapturedUtc, IReadOnlyList<AssessmentFact> Facts);
public sealed record PlanItem(string ActionId, string Title, PlanItemStatus Status, ResourceState? Before, ResourceState? Desired, string Reason);
public sealed record ChangePlan(string SchemaVersion, string CatalogVersion, string PlanId, DateTimeOffset CreatedUtc, DateTimeOffset ExpiresUtc, DeviceContext Context, IReadOnlyList<PlanItem> Items, string Digest);
public sealed record ActionResult(string ActionId, string Status, string Detail);
public sealed record RunResult(string RunId, string Status, IReadOnlyList<ActionResult> Actions);
public sealed record RunSummary(string RunId, DateTimeOffset StartedUtc, string Status, int ActionCount);
public sealed record HistoryAction(string ActionId, string Title, string Status, ResourceState Before, ResourceState Desired, string Detail);
public sealed record RunDetails(string RunId, string Status, DeviceContext Context, IReadOnlyList<HistoryAction> Actions);
