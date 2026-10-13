using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Optimizer.Core;

public sealed class ActionCatalog
{
    internal static readonly JsonSerializerOptions Json = CreateOptions();
    private readonly IReadOnlyDictionary<string, ActionDefinition> index;
    public string Version { get; }
    public IReadOnlyList<ActionDefinition> Actions { get; }

    public ActionCatalog(string version, IReadOnlyList<ActionDefinition> actions)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 64 || actions is null || actions.Count == 0 || actions.Count > 1000)
            throw new ArgumentException("A catalog needs a version and between one and 1000 actions.");
        var copy = actions.Select(a => a with { Profiles = a.Profiles?.ToArray() ?? [] }).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in copy)
        {
            if (!Regex.IsMatch(action.Id ?? "", "^[a-z][a-z0-9.-]{2,95}$") || !ids.Add(action.Id!))
                throw new ArgumentException("Catalog action IDs must be unique lowercase identifiers.");
            foreach (var value in new[] { action.Title, action.Category, action.Summary, action.Benefit, action.Tradeoff })
                if (string.IsNullOrWhiteSpace(value) || value.Length > 5000) throw new ArgumentException($"Incomplete action: {action.Id}.");
            if (!Enum.IsDefined(action.Mode)) throw new ArgumentException($"Invalid action mode: {action.Id}.");
            if (action.SourceUrl is not null && (!Uri.TryCreate(action.SourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)))
                throw new ArgumentException($"Source must be an HTTPS URL: {action.Id}.");
            if (action.SettingsUri is not null && !Regex.IsMatch(action.SettingsUri, "^ms-settings:[a-z0-9-]+$"))
                throw new ArgumentException($"Invalid Settings link: {action.Id}.");
            if (action.Profiles.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 64) || action.Profiles.Distinct(StringComparer.Ordinal).Count() != action.Profiles.Length)
                throw new ArgumentException($"Invalid profiles: {action.Id}.");
        }
        Version = version;
        Actions = Array.AsReadOnly(copy);
        index = copy.ToDictionary(a => a.Id, StringComparer.Ordinal);
    }

    public ActionDefinition? Find(string id) => index.GetValueOrDefault(id);

    public static ActionCatalog LoadDefault()
    {
        using var stream = typeof(ActionCatalog).Assembly.GetManifestResourceStream("Optimizer.Core.Catalog.json")
            ?? throw new InvalidOperationException("The application catalog is missing.");
        using var doc = JsonDocument.Parse(stream);
        RejectDuplicateProperties(doc.RootElement);
        var data = doc.Deserialize<CatalogFile>(Json) ?? throw new InvalidDataException("Empty catalog.");
        return new ActionCatalog(data.Version, data.Actions);
    }

    private sealed record CatalogFile(string Version, ActionDefinition[] Actions);
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in element.EnumerateObject())
            {
                if (!keys.Add(p.Name)) throw new InvalidDataException($"Duplicate catalog field: {p.Name}.");
                RejectDuplicateProperties(p.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
}
