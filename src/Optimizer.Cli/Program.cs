using System.Text.Json;
using System.Text.Json.Serialization;
using Optimizer.Core;
using Optimizer.Windows;

return Commands.Run(args);

internal static class Commands
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    internal static int Run(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] is "help" or "--help" or "-h") { Help(); return 0; }
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows Workbench requires Windows.");
            var verb = args.Length == 0 ? "assess" : args[0];
            var allowed = verb switch
            {
                "assess" => new[] { "--out" },
                "catalog" => new[] { "--mode", "--profile" },
                "plan" => new[] { "--actions", "--out" },
                "apply" => new[] { "--plan", "--approve" },
                "history" => new[] { "--run" },
                "undo" => new[] { "--run", "--approve-run" },
                _ => throw new ArgumentException("Unknown command. Use help for supported commands.")
            };
            var options = Parse(args.Skip(1).ToArray(), allowed);
            var catalog = ActionCatalog.LoadDefault();
            var service = new WindowsAssessmentService();
            var provider = new RegistryActionProvider();
            var state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsWorkbench", "state");
            var engine = new ChangeEngine(catalog, provider, service.GetContext, state);
            switch (verb)
            {
                case "assess":
                    var assessment = service.Assess();
                    var report = new
                    {
                        schemaVersion = "1.0", product = "Windows Workbench", assessment.CapturedUtc,
                        windows = new { assessment.Context.WindowsBuild, assessment.Context.Edition, assessment.Context.Architecture, assessment.Context.IsManaged, assessment.Context.IsElevated },
                        assessment.Facts,
                        redaction = "Account and machine identifiers, application names, startup commands, network addresses and file inventory are excluded. Hardware details and drive letters remain."
                    };
                    if (options.TryGetValue("--out", out var reportPath)) SaveNew(reportPath, report);
                    else Print(report);
                    return 0;
                case "catalog":
                    IEnumerable<ActionDefinition> actions = catalog.Actions;
                    if (options.TryGetValue("--mode", out var mode))
                    {
                        if (!Enum.TryParse<ActionMode>(mode, true, out var selectedMode) || !Enum.IsDefined(selectedMode))
                            throw new ArgumentException("Mode must be Preference, Guided or External.");
                        actions = actions.Where(a => a.Mode == selectedMode);
                    }
                    if (options.TryGetValue("--profile", out var profile))
                    {
                        if (!catalog.Actions.Any(a => a.Profiles.Contains(profile, StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Unknown priority filter.");
                        actions = actions.Where(a => a.Profiles.Contains(profile, StringComparer.OrdinalIgnoreCase));
                    }
                    Print(new { catalog.Version, actions = actions.ToArray() });
                    return 0;
                case "plan":
                    var ids = Required(options, "--actions").Split(',', StringSplitOptions.TrimEntries);
                    if (ids.Any(string.IsNullOrEmpty)) throw new ArgumentException("Select one or more comma-separated action IDs.");
                    var output = Required(options, "--out");
                    var plan = new Planner(catalog, provider).Create(service.GetContext(), ids);
                    SaveNew(output, plan);
                    Print(new { plan.PlanId, plan.ExpiresUtc, plan.Digest, plan.Items,
                        note = "Nothing changed. The saved plan contains private account/device binding identifiers. Review every item before approving its digest." });
                    return plan.Items.Any(i => i.Status == PlanItemStatus.Blocked) ? 2 : 0;
                case "apply":
                    var planPath = Path.GetFullPath(Required(options, "--plan"));
                    if (new FileInfo(planPath).Length > 1_048_576) throw new InvalidDataException("Plan exceeds the size limit.");
                    using (var document = JsonDocument.Parse(File.ReadAllText(planPath), new JsonDocumentOptions { MaxDepth = 32 }))
                    {
                        RejectDuplicateProperties(document.RootElement);
                        var reviewed = document.Deserialize<ChangePlan>(Json) ?? throw new InvalidDataException("The plan is empty.");
                        return Result(engine.Apply(reviewed, Required(options, "--approve")));
                    }
                case "history":
                    if (options.TryGetValue("--run", out var runId))
                    {
                        var detail = engine.Details(runId) ?? throw new InvalidOperationException("Run was not found.");
                        Print(new { detail.RunId, detail.Status, detail.Actions });
                    }
                    else Print(engine.History());
                    return 0;
                case "undo":
                    var run = Required(options, "--run");
                    if (!string.Equals(run, Required(options, "--approve-run"), StringComparison.Ordinal))
                        throw new ArgumentException("Review history first, then approve the exact run ID to recover it.");
                    return Result(engine.Recover(run));
                default: throw new InvalidOperationException("Command dispatch failed.");
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "Blocked", error = error.Message }, Json));
            return 2;
        }
    }

    private static Dictionary<string, string> Parse(string[] args, string[] allowed)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!allowed.Contains(args[i], StringComparer.Ordinal) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Unknown option or missing value: " + args[i]);
            if (!result.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Duplicate option: " + args[i]);
        }
        return result;
    }
    private static string Required(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Required option: " + key);
    private static void SaveNew<T>(string path, T value)
    {
        using var stream = new FileStream(Path.GetFullPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, value, Json);
        stream.Flush(flushToDisk: true);
    }
    private static void Print<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));
    private static int Result(RunResult result)
    {
        Print(result);
        return result.Status is "Succeeded" or "Recovered" or "NoChanges" ? 0 : 3;
    }
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new InvalidDataException("Duplicate plan field: " + property.Name);
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
    private static void Help() => Console.WriteLine("""
        Windows Workbench 0.1 preview
        Default: read-only assessment. Run without administrator elevation.

        assess [--out report.json]                  Read-only inventory, identifiers omitted
        catalog [--mode Preference|Guided|External] [--profile Gaming]
        plan --actions id,id --out private-plan.json  Capture a 30-minute review; no settings changed
        apply --plan private-plan.json --approve DIGEST
        history [--run RUN_ID]                      Read local runs, no journal created
        undo --run RUN_ID --approve-run RUN_ID      Recover recorded, eligible values

        Files are never overwritten. Quotes are needed around paths or profile names with spaces.
        Direct changes cover six current-user preferences on allowlisted unmanaged consumer builds.
        Guidance and tool entries cannot execute scripts, install apps, or change Windows.
        Review plans are private: they contain the account SID and hashed machine binding.
        Undo preserves conflicting changes; restore absence means Windows may supply a default.
        Configuration readback is not proof of visible behavior or faster performance.
        Exit codes: 0 completed; 2 invalid/blocked; 3 execution or recovery incomplete.
        """);
}
