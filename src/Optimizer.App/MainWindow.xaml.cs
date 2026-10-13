using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Optimizer.Core;
using Optimizer.Windows;

namespace Optimizer.App;

public partial class MainWindow : Window
{
    private readonly ActionCatalog _catalog = ActionCatalog.LoadDefault();
    private readonly WindowsAssessmentService _assessmentService = new();
    private readonly RegistryActionProvider _provider = new();
    private readonly HashSet<string> _selection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _nav = new(StringComparer.Ordinal);
    private readonly string[] _arguments;
    private readonly string _stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsWorkbench", "state");
    private Assessment? _assessment;
    private ChangePlan? _plan;
    private bool _busy;
    private string _screen = "Overview";
    private string _profile = "All priorities";
    private string _search = "";
    private StackPanel? _cards;
    private TextBlock? _catalogCount;
    private int _visibleActionCount;
    private bool _smokeFailed;
    private bool IsSmokeRun => _arguments.Contains("--smoke-ui", StringComparer.Ordinal);

    public MainWindow(string[] arguments)
    {
        InitializeComponent();
        _arguments = arguments;
        foreach (var name in new[] { "Overview", "Windows preferences", "Performance", "Apps & startup", "Maintenance", "Tools", "Review changes", "History" })
        {
            var button = new Button
            {
                Content = name, HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 5), Padding = new Thickness(12, 12, 6, 12),
                BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch,
                ToolTip = name
            };
            AutomationProperties.SetName(button, name);
            button.Click += async (_, _) => await NavigateAsync(name);
            _nav[name] = button;
            Navigation.Children.Add(button);
        }
        CatalogLine.Text = $"CATALOG {_catalog.Version}";
        Loaded += async (_, _) =>
        {
            await AssessAsync();
            if (IsSmokeRun) await CaptureSmokeAsync();
        };
        Closing += OnClosing;
        RenderOverview();
        PaintNavigation();
    }

    private Brush Brush(string name) => (Brush)FindResource(name);
    private TextBlock Text(string text, double size = 14, bool muted = false, bool bold = false)
        => new() { Text = text, FontSize = size, Foreground = Brush(muted ? "MutedBrush" : "InkBrush"), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
    private TextBlock Code(string text) => new() { Text = text, FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), Foreground = Brush("MutedBrush") };
    private Border Card(UIElement child, bool tinted = false) => new()
    {
        Background = Brush(tinted ? "TintBrush" : "SurfaceBrush"), BorderBrush = Brush("LineBrush"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7), Padding = new Thickness(22), Margin = new Thickness(0, 0, 0, 14), Child = child
    };
    private Button Button(string caption, Func<Task> action, bool primary = false)
    {
        var button = new Button { Content = caption };
        if (primary) button.Style = (Style)FindResource("PrimaryButton");
        button.Click += async (_, _) => await action();
        AutomationProperties.SetName(button, caption);
        return button;
    }
    private static WrapPanel Row(params UIElement[] children)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }
    private void Header(string eyebrow, string title, string summary)
    {
        Page.Children.Clear();
        _cards = null;
        _catalogCount = null;
        PageScroll.ScrollToTop();
        Page.Children.Add(Code(eyebrow.ToUpperInvariant()));
        Page.Children.Add(Text(title, 32, bold: true));
        var description = Text(summary, 14, true);
        description.Margin = new Thickness(0, 0, 0, 24);
        Page.Children.Add(description);
    }
    private void PaintNavigation()
    {
        foreach (var (name, button) in _nav)
        {
            button.Background = name == _screen ? Brush("AccentBrush") : Brushes.Transparent;
            button.Foreground = Brush("RailInkBrush");
            button.FontWeight = name == _screen ? FontWeights.SemiBold : FontWeights.Normal;
        }
        ReviewShortcut.Content = $"Review changes · {_selection.Count}";
    }
    private async Task NavigateAsync(string name)
    {
        if (_busy) return;
        _screen = name;
        _search = "";
        PaintNavigation();
        PageScroll.ScrollToTop();
        if (name == "Overview") RenderOverview();
        else if (name == "Review changes") await PrepareReviewAsync();
        else if (name == "History") await ShowHistoryAsync();
        else RenderCatalog();
    }
    private async void ReviewShortcut_Click(object sender, RoutedEventArgs e) => await NavigateAsync("Review changes");

    private async Task RunBusyAsync(string message, Func<Task> work)
    {
        if (_busy) return;
        _busy = true;
        BusyIndicator.Visibility = Visibility.Visible;
        Navigation.IsEnabled = false;
        Page.IsEnabled = false;
        ReviewShortcut.IsEnabled = false;
        StatusLine.Text = message;
        try { await work(); }
        catch (Exception error)
        {
            StatusLine.Text = "The operation could not complete. Review the details before continuing.";
            if (IsSmokeRun)
            {
                _smokeFailed = true;
                Page.Children.Add(Card(Text($"Operation failed: {error.Message}"), true));
            }
            else MessageBox.Show(this, error.Message, "Windows Workbench", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
            BusyIndicator.Visibility = Visibility.Collapsed;
            Navigation.IsEnabled = true;
            Page.IsEnabled = true;
            ReviewShortcut.IsEnabled = true;
        }
    }
    private async Task AssessAsync()
    {
        await RunBusyAsync("Reading Windows configuration. No settings are being changed…", async () =>
        {
            _assessment = await Task.Run(_assessmentService.Assess);
            _plan = null;
            DeviceLine.Text = $"Windows build {_assessment.Context.WindowsBuild}  /  {_assessment.Context.Edition}  /  {_assessment.Context.Architecture}";
            RenderOverview();
            StatusLine.Text = $"Assessment updated at {_assessment.CapturedUtc.ToLocalTime():HH:mm}. No Windows settings changed.";
        });
    }

    private void RenderOverview()
    {
        Header("Assessment / local & read-only", "Make Windows work for you.", "Understand this PC, choose useful improvements, and review every change before applying it.");
        var intro = new StackPanel();
        intro.Children.Add(Text("A clear view before a single change.", 23, bold: true));
        intro.Children.Add(Text($"Explore {_catalog.Actions.Count(a => a.Mode != ActionMode.Preference)} guides and tool references, plus {_catalog.Actions.Count(a => a.Mode == ActionMode.Preference)} reversible preferences. Only individually selected preferences can be applied in this preview.", 14, true));
        intro.Children.Add(Row(Button("Explore preferences", () => NavigateAsync("Windows preferences"), true), Button("Performance guidance", () => NavigateAsync("Performance"))));
        Page.Children.Add(Card(intro, true));

        var overview = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        overview.ColumnDefinitions.Add(new ColumnDefinition()); overview.ColumnDefinitions.Add(new ColumnDefinition()); overview.ColumnDefinitions.Add(new ColumnDefinition());
        var values = new[]
        {
            ($"{_catalog.Actions.Count()}", "Capabilities to explore", "Preferences, guided checks and optional tools"),
            ($"{_selection.Count}", "Changes selected", "Your selection stays yours when priorities change"),
            (_assessment is null ? "Reading" : $"{_assessment.Facts.Count(f => f.Status == ObservationState.Known)}", "Known observations", "Unknown information stays visibly unknown")
        };
        for (var i = 0; i < values.Length; i++)
        {
            var stack = new StackPanel { Margin = new Thickness(i == 0 ? 0 : 18, 6, 14, 0) };
            stack.Children.Add(Text(values[i].Item1, 32, bold: true)); stack.Children.Add(Text(values[i].Item2, 14, bold: true)); stack.Children.Add(Text(values[i].Item3, 12, true));
            Grid.SetColumn(stack, i); overview.Children.Add(stack);
        }
        Page.Children.Add(overview);
        Page.Children.Add(Text("This PC", 21, bold: true));
        if (_assessment is null) Page.Children.Add(Text("Reading system information…", muted: true));
        else
        {
            var context = _assessment.Context;
            if (context.IsElevated || context.IsManaged != false)
            {
                var reason = context.IsElevated ? "This window is running as administrator. Relaunch normally to change preferences for the correct account." : context.IsManaged == true ? "This PC reports organizational management. Assessment and guidance are available; direct changes are blocked." : "Organizational management could not be determined. Direct changes are blocked until it can be checked.";
                Page.Children.Add(Card(Text(reason), true));
            }
            foreach (var group in _assessment.Facts.GroupBy(f => f.Category))
            {
                var panel = new StackPanel();
                panel.Children.Add(Code(group.Key.ToUpperInvariant()));
                foreach (var fact in group)
                {
                    var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.Children.Add(Text(fact.Name, 13, true));
                    var details = new StackPanel();
                    details.Children.Add(Text(fact.Status == ObservationState.Known ? fact.Value : $"{fact.Status}: {fact.Value}", 13, bold: fact.Status == ObservationState.Known));
                    if (!string.IsNullOrWhiteSpace(fact.Detail)) details.Children.Add(Text(fact.Detail, 12, true));
                    Grid.SetColumn(details, 1); grid.Children.Add(details); panel.Children.Add(grid);
                }
                Page.Children.Add(Card(panel));
            }
        }
        Page.Children.Add(Row(Button("Refresh assessment", AssessAsync), Button("Export redacted report", ExportAssessmentAsync)));
    }

    private void RenderCatalog()
    {
        var summaries = new Dictionary<string, string>
        {
            ["Windows preferences"] = "Choose the small details that make Windows easier to use. Every preference starts unselected.",
            ["Performance"] = "Find the bottleneck before changing a setting. These checks explain what may help and how to measure it.",
            ["Apps & startup"] = "Review what starts and what you use. Follow Windows guidance for app and startup decisions.",
            ["Maintenance"] = "Keep Windows dependable with supported maintenance and clear recovery boundaries.",
            ["Tools"] = "Explore useful utilities at their official sources. Opening a source never installs a tool."
        };
        Header("Capability catalog", _screen, summaries.GetValueOrDefault(_screen, "Explore available capabilities."));
        var controls = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) });
        var search = new TextBox { Text = _search, Margin = new Thickness(0, 4, 16, 0) };
        AutomationProperties.SetName(search, "Search capabilities"); search.ToolTip = "Search titles, benefits and tradeoffs";
        var searchPanel = new StackPanel(); searchPanel.Children.Add(Text("Search capabilities", 12, true)); searchPanel.Children.Add(search);
        controls.Children.Add(searchPanel);
        var profile = new ComboBox { MinHeight = 38, Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
        foreach (var name in new[] { "All priorities", "Everyday", "Gaming", "Laptop", "Privacy", "Minimal Distraction" }) profile.Items.Add(name);
        profile.SelectedItem = _profile;
        AutomationProperties.SetName(profile, "Priority filter");
        var profilePanel = new StackPanel(); profilePanel.Children.Add(Text("Filter recommendations", 12, true)); profilePanel.Children.Add(profile);
        Grid.SetColumn(profilePanel, 1); controls.Children.Add(profilePanel);
        Page.Children.Add(controls);
        Page.Children.Add(Text("A priority filters suggestions. It never selects changes or overrides your choices.", 12, true));
        _cards = new StackPanel(); Page.Children.Add(_cards);
        search.TextChanged += (_, _) => { _search = search.Text; RenderCards(); };
        profile.SelectionChanged += (_, _) => { _profile = (string?)profile.SelectedItem ?? "All priorities"; RenderCards(); };
        RenderCards();
    }

    private bool MatchesScreen(ActionDefinition action) => _screen switch
    {
        "Windows preferences" => action.Mode == ActionMode.Preference || action.Category.Equals("Windows preferences", StringComparison.OrdinalIgnoreCase),
        "Tools" => action.Mode == ActionMode.External || action.Category.Equals("Tools", StringComparison.OrdinalIgnoreCase),
        _ => action.Category.Equals(_screen, StringComparison.OrdinalIgnoreCase)
    };
    private void RenderCards()
    {
        if (_cards is null) return;
        _cards.Children.Clear();
        var actions = _catalog.Actions.Where(MatchesScreen)
            .Where(a => _profile == "All priorities" || a.Profiles.Contains(_profile, StringComparer.OrdinalIgnoreCase))
            .Where(a => string.IsNullOrWhiteSpace(_search) || $"{a.Title} {a.Summary} {a.Benefit} {a.Tradeoff}".Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        _visibleActionCount = actions.Length;
        _catalogCount = Code($"{_visibleActionCount} CAPABILITIES  /  {_selection.Count} PREFERENCES SELECTED");
        _cards.Children.Add(_catalogCount);
        foreach (var action in actions)
        {
            var panel = new StackPanel();
            panel.Children.Add(Code(action.Mode switch { ActionMode.Preference => "REVERSIBLE PREFERENCE · CURRENT USER", ActionMode.External => "OPTIONAL TOOL · OFFICIAL SOURCE", _ => "GUIDED CHECK · MANUAL ACTION" }));
            panel.Children.Add(Text(action.Title, 20, bold: true)); panel.Children.Add(Text(action.Summary, 14, true));
            panel.Children.Add(Text($"Why consider it: {action.Benefit}", 13));
            panel.Children.Add(Text($"Tradeoff: {action.Tradeoff}", 13, true));
            if (action.Mode == ActionMode.Preference)
            {
                var check = new CheckBox { Content = "Include in my review", IsChecked = _selection.Contains(action.Id), Margin = new Thickness(0, 8, 0, 2) };
                AutomationProperties.SetName(check, $"Include {action.Title} in review");
                check.Checked += (_, _) => Select(action.Id, true);
                check.Unchecked += (_, _) => Select(action.Id, false);
                panel.Children.Add(check);
            }
            var links = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (action.SettingsUri is { Length: > 0 } settings) links.Children.Add(Button("Open Windows Settings", () => OpenLinkAsync(settings, true)));
            if (action.SourceUrl is { Length: > 0 } source) links.Children.Add(Button(action.Mode == ActionMode.External ? "Visit official source" : "Read supporting guidance", () => OpenLinkAsync(source, false)));
            if (links.Children.Count > 0) panel.Children.Add(links);
            _cards.Children.Add(Card(panel));
        }
        if (actions.Length == 0) _cards.Children.Add(Card(Text("No capabilities match this view. Clear the search or choose All priorities.")));
    }
    private void Select(string id, bool selected)
    {
        if (selected) _selection.Add(id); else _selection.Remove(id);
        _plan = null;
        if (_catalogCount is not null) _catalogCount.Text = $"{_visibleActionCount} CAPABILITIES  /  {_selection.Count} PREFERENCES SELECTED";
        PaintNavigation();
        StatusLine.Text = $"{_selection.Count} preferences selected. Review shows the exact current and proposed values.";
    }
    private Task OpenLinkAsync(string target, bool settings)
    {
        try
        {
            if (settings) SafeLauncher.OpenSettings(target); else SafeLauncher.OpenSource(target);
            StatusLine.Text = settings ? "Opened Windows Settings. Any changes there are made by you." : "Opened the source in your browser. No tool was installed.";
        }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Could not open this link", MessageBoxButton.OK, MessageBoxImage.Warning); }
        return Task.CompletedTask;
    }

    private async Task PrepareReviewAsync()
    {
        Header("Proposal / no changes yet", "Review your changes", "Check the exact before and after values. Only ready items can be applied; blocked and already configured items remain unchanged.");
        if (_selection.Count == 0)
        {
            Page.Children.Add(Card(Text("Your review is empty. Choose a Windows preference to include it here.")));
            Page.Children.Add(Button("Choose preferences", () => NavigateAsync("Windows preferences"), true)); return;
        }
        await RunBusyAsync("Reading the current values and preparing your review…", async () =>
        {
            var ids = _selection.ToArray();
            _plan = await Task.Run(() => new Planner(_catalog, _provider).Create(_assessmentService.GetContext(), ids));
            RenderReview(); StatusLine.Text = "Review ready. No Windows settings changed.";
        });
    }
    private static string State(ResourceState? state) => state is null ? "Unavailable" : !state.Exists ? "Value absent (Windows default)" : $"{state.Kind}: {state.Data}";
    private static string PreferenceState(string id, ResourceState? state)
    {
        if (state is null) return "Could not read this setting";
        if (!state.Exists) return "Windows default (no saved preference)";
        if (!state.Kind.Equals("DWord", StringComparison.OrdinalIgnoreCase) || state.Data is not ("0" or "1")) return State(state);
        var enabled = state.Data == "1";
        return id switch
        {
            "explorer.extensions" => enabled ? "File extensions hidden" : "File extensions visible",
            "explorer.taskbar-left" => enabled ? "Taskbar icons centered" : "Taskbar icons aligned left",
            "explorer.task-view" => enabled ? "Task view button shown" : "Task view button hidden",
            "appearance.transparency" => enabled ? "Transparency effects on" : "Transparency effects off",
            "privacy.suggestions" => enabled ? "Suggested content on" : "Suggested content off",
            "privacy.advertising" => enabled ? "Advertising ID enabled" : "Advertising ID disabled",
            _ => State(state)
        };
    }
    private string ActionTitle(string id) => _catalog.Find(id)?.Title ?? (id == "journal" ? "Local change history" : $"Run detail: {id}");
    private void RenderReview()
    {
        if (_plan is null) return;
        Header("Proposal / exact values", "Review your changes", "These preferences affect the account running this window. Original values are recorded before a change, and each result is read back to verify it.");
        foreach (var item in _plan.Items)
        {
            var panel = new StackPanel(); panel.Children.Add(Code(item.Status.ToString().ToUpperInvariant()));
            panel.Children.Add(Text(item.Title, 20, bold: true));
            var definition = _catalog.Find(item.ActionId);
            if (definition is not null)
            {
                panel.Children.Add(Text(definition.Benefit, 13)); panel.Children.Add(Text($"Tradeoff: {definition.Tradeoff}", 13, true));
            }
            var values = new Grid { Margin = new Thickness(0, 10, 0, 8) };
            values.ColumnDefinitions.Add(new ColumnDefinition()); values.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); values.ColumnDefinitions.Add(new ColumnDefinition());
            var before = new StackPanel(); before.Children.Add(Code("CURRENT")); before.Children.Add(Text(item.Status == PlanItemStatus.Blocked && item.Before is null ? "Not read (change blocked)" : PreferenceState(item.ActionId, item.Before), 15, bold: true)); values.Children.Add(before);
            var arrow = Text("→", 22, true); Grid.SetColumn(arrow, 1); values.Children.Add(arrow);
            var desired = new StackPanel(); desired.Children.Add(Code("PROPOSED")); desired.Children.Add(Text(item.Status == PlanItemStatus.Blocked && item.Desired is null ? "No change proposed" : PreferenceState(item.ActionId, item.Desired), 15, bold: true)); Grid.SetColumn(desired, 2); values.Children.Add(desired);
            panel.Children.Add(values);
            if (!string.IsNullOrWhiteSpace(item.Reason)) panel.Children.Add(Text(item.Reason, 13, true));
            panel.Children.Add(Button("Remove from review", async () => { Select(item.ActionId, false); await PrepareReviewAsync(); }));
            Page.Children.Add(Card(panel));
        }
        var ready = _plan.Items.Count(i => i.Status == PlanItemStatus.Ready);
        Page.Children.Add(Text($"This review expires at {_plan.ExpiresUtc.ToLocalTime():t}. Refresh it if your settings change.", 13, true));
        Page.Children.Add(new Expander { Header = "Technical review details", Margin = new Thickness(0, 0, 0, 14), Content = Code($"PLAN {_plan.PlanId}\nREVIEW DIGEST {_plan.Digest}\n" + string.Join("\n", _plan.Items.Select(i => $"{i.ActionId}: {State(i.Before)} → {State(i.Desired)}"))) });
        Page.Children.Add(Text("Undo restores the recorded value if it still matches what this run applied. A value changed elsewhere needs a fresh review; it is never overwritten by recovery.", 13, true));
        var apply = Button($"Apply {ready} reviewed preferences", ApplyAsync, true); apply.IsEnabled = ready > 0;
        Page.Children.Add(Row(apply, Button("Refresh review", PrepareReviewAsync)));
    }
    private ChangeEngine Engine() => new(_catalog, _provider, _assessmentService.GetContext, _stateDirectory);
    private async Task ApplyAsync()
    {
        var plan = _plan;
        if (plan is null) return;
        var ready = plan.Items.Where(i => i.Status == PlanItemStatus.Ready).ToArray();
        if (ready.Length == 0) return;
        var summary = string.Join("\n", ready.Select(i => $"• {i.Title}\n   {PreferenceState(i.ActionId, i.Before)} → {PreferenceState(i.ActionId, i.Desired)}"));
        var answer = MessageBox.Show(this, $"Apply these {ready.Length} preferences to this Windows account?\n\n{summary}\n\nThe run will record original values and verify each change.", "Apply reviewed preferences", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;
        await RunBusyAsync("Applying the reviewed preferences and verifying their values…", async () =>
        {
            var result = await Task.Run(() => Engine().Apply(plan, plan.Digest));
            _plan = null;
            foreach (var action in result.Actions.Where(a => a.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase) || a.Status.Equals("AlreadyConfigured", StringComparison.OrdinalIgnoreCase))) _selection.Remove(action.ActionId);
            PaintNavigation(); RenderRunResult(result, false);
            StatusLine.Text = $"Run finished: {result.Status}. Read each result below.";
        });
    }
    private void RenderRunResult(RunResult result, bool recovery)
    {
        Header(recovery ? "Recovery / recorded state" : "Results / verified outcomes", recovery ? "Recovery results" : "Change results", $"Run {result.RunId} · {result.Status}");
        foreach (var action in result.Actions)
        {
            var panel = new StackPanel(); panel.Children.Add(Code(action.Status.ToUpperInvariant())); panel.Children.Add(Text(ActionTitle(action.ActionId), 19, bold: true)); panel.Children.Add(Text(action.Detail, 13, true)); Page.Children.Add(Card(panel));
        }
        Page.Children.Add(Button("Open change history", () => NavigateAsync("History"), true));
    }
    private async Task ShowHistoryAsync()
    {
        Header("Activity / original values", "Change history", "Inspect recorded runs and recover eligible preferences. History belongs to this account and this PC.");
        await RunBusyAsync("Reading local change history…", async () =>
        {
            var runs = await Task.Run(() => Engine().History().ToArray());
            if (runs.Length == 0) Page.Children.Add(Card(Text("No changes have been recorded. Your first applied review will appear here, with its original values and verification results.")));
            foreach (var run in runs)
            {
                var panel = new StackPanel(); panel.Children.Add(Code(run.Status.ToUpperInvariant())); panel.Children.Add(Text(run.StartedUtc.ToLocalTime().ToString("f"), 19, bold: true));
                panel.Children.Add(Text($"{run.ActionCount} actions · {run.RunId}", 12, true)); panel.Children.Add(Button("Inspect run", () => ShowRunAsync(run.RunId))); Page.Children.Add(Card(panel));
            }
            StatusLine.Text = $"{runs.Length} local runs. Reading history does not create a journal.";
        });
    }
    private async Task ShowRunAsync(string runId)
    {
        await RunBusyAsync("Reading the selected run…", async () =>
        {
            var details = await Task.Run(() => Engine().Details(runId));
            if (details is null)
            {
                Header("History / unavailable run", "This run is no longer available", "The local history may have changed since this list was loaded.");
                Page.Children.Add(Button("Refresh history", ShowHistoryAsync, true));
                StatusLine.Text = "The selected run could not be found. Refresh history to continue.";
                return;
            }
            Header("History / run details", "Recorded preferences", $"Run {runId} · {details.Status}");
            foreach (var action in details.Actions)
            {
                var panel = new StackPanel(); panel.Children.Add(Code(action.Status.ToUpperInvariant())); panel.Children.Add(Text(action.Title, 19, bold: true));
                panel.Children.Add(Text($"Original: {PreferenceState(action.ActionId, action.Before)}\nApplied target: {PreferenceState(action.ActionId, action.Desired)}", 13)); panel.Children.Add(Text(action.Detail, 13, true)); Page.Children.Add(Card(panel));
            }
            Page.Children.Add(Text("Recovery checks this PC, account, and the current setting before restoring the original value. Conflicts are reported for your review.", 13, true));
            var recover = Button("Recover eligible preferences", () => RecoverAsync(runId), true);
            recover.IsEnabled = details.Status != "Recovered";
            Page.Children.Add(Row(recover, Button("Back to history", ShowHistoryAsync)));
            StatusLine.Text = details.Status == "Recovered" ? "This run has already been recovered." : "Inspect the original values before starting recovery.";
        });
    }
    private async Task RecoverAsync(string runId)
    {
        if (MessageBox.Show(this, "Restore the recorded original values for eligible preferences in this run?\n\nSettings changed outside this run will be reported as conflicts and left unchanged.", "Recover preferences", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        await RunBusyAsync("Comparing current values and recovering eligible preferences…", async () =>
        {
            var result = await Task.Run(() => Engine().Recover(runId));
            _plan = null; RenderRunResult(result, true); StatusLine.Text = $"Recovery finished: {result.Status}. Review each result for conflicts or failures.";
        });
    }
    private async Task ExportAssessmentAsync()
    {
        if (_assessment is not { } assessment) return;
        var dialog = new SaveFileDialog { Title = "Export redacted assessment", Filter = "JSON report (*.json)|*.json", FileName = $"windows-workbench-{DateTime.Now:yyyy-MM-dd}.json", AddExtension = true, DefaultExt = ".json", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        await RunBusyAsync("Saving the redacted report…", async () =>
        {
            // Deliberately exclude raw facts, values, details, and principal identifiers.
            // Future inventory providers may contain paths or names in any of these fields.
            var report = new
            {
                SchemaVersion = "1", Product = "Windows Workbench", assessment.CapturedUtc,
                Windows = new { assessment.Context.WindowsBuild, assessment.Context.Edition, assessment.Context.Architecture, assessment.Context.IsManaged },
                ObservationSummary = Enum.GetValues<ObservationState>().ToDictionary(s => s.ToString(), s => assessment.Facts.Count(f => f.Status == s)),
                CatalogVersion = _catalog.Version,
                Redaction = "Machine/user identity, raw inventory facts, paths, names, values, and diagnostic details are omitted."
            };
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dialog.FileName, json);
            StatusLine.Text = "Redacted report saved. Raw inventory and account or machine identifiers were excluded.";
        });
    }
    private async Task CaptureSmokeAsync()
    {
        var index = Array.IndexOf(_arguments, "--smoke-ui");
        if (index < 0 || index + 1 >= _arguments.Length) { Application.Current.Shutdown(2); return; }
        try
        {
            // Test rendering can select known preferences for a preview, but never invokes Apply or Recover.
            var selectIndex = Array.IndexOf(_arguments, "--smoke-select");
            if (selectIndex >= 0 && selectIndex + 1 < _arguments.Length)
                foreach (var id in _arguments[selectIndex + 1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (_catalog.Find(id)?.Mode == ActionMode.Preference) _selection.Add(id);
            var screenIndex = Array.IndexOf(_arguments, "--smoke-screen");
            if (screenIndex >= 0 && screenIndex + 1 < _arguments.Length && _nav.ContainsKey(_arguments[screenIndex + 1])) await NavigateAsync(_arguments[screenIndex + 1]);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
            var image = new RenderTargetBitmap((int)Math.Ceiling(RootLayout.ActualWidth), (int)Math.Ceiling(RootLayout.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(RootLayout);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            var imagePath = Path.GetFullPath(_arguments[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            using var file = File.Create(imagePath); encoder.Save(file);
            Application.Current.Shutdown(_smokeFailed ? 4 : 0);
        }
        catch { Application.Current.Shutdown(3); }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_busy) return;
        e.Cancel = true;
        StatusLine.Text = "Wait for the current operation to finish before closing this window.";
    }
}
