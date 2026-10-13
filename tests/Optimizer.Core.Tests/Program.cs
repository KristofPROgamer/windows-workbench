using Microsoft.Data.Sqlite;
using Optimizer.Core;
using System.Security.Cryptography;

var tests = new (string Name, Action Run)[]
{
    ("Embedded catalog is valid", () => Check(ActionCatalog.LoadDefault().Actions.Count > 0)),
    ("Duplicate catalog IDs are rejected", () => Throws(() => new ActionCatalog("1", [Fixture.Definition("test.one"), Fixture.Definition("test.one")]))),
    ("Unknown catalog modes are rejected", () => Throws(() => new ActionCatalog("1", [Fixture.Definition("test.one") with { Mode = (ActionMode)99 }]))),
    ("Executable source schemes are rejected", () => Throws(() => new ActionCatalog("1", [Fixture.Definition("test.one") with { SourceUrl = "file:///evil.exe" }]))),
    ("Unsafe settings arguments are rejected", () => Throws(() => new ActionCatalog("1", [Fixture.Definition("test.one") with { SettingsUri = "ms-settings:display&cmd=evil" }]))),
    ("Plan captures originals without writing", () => With(f => { var p = f.Plan(); Equal(PlanItemStatus.Ready, p.Items[0].Status); Equal(0, f.Provider.Writes); Equal(ResourceState.Missing, p.Items[0].Before!); Check(!Directory.Exists(f.State)); })),
    ("Unknown action selection is rejected", () => With(f => Throws(() => f.Planner.Create(f.Context, ["unknown.action"])))),
    ("Duplicate action selection is rejected", () => With(f => Throws(() => f.Planner.Create(f.Context, ["test.one", "test.one"])))),
    ("Guided actions stay guided", () => With(f => { var p = f.Planner.Create(f.Context, ["test.guide"]); Equal(PlanItemStatus.Guided, p.Items[0].Status); Equal("NoChanges", f.Engine.Apply(p, p.Digest).Status); Equal(0, f.Provider.Writes); })),
    ("Unknown inventory blocks changes", () => With(f => { f.Provider.ReadStatus = ObservationState.Unknown; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Unavailable inventory blocks changes", () => With(f => { f.Provider.ReadStatus = ObservationState.Unavailable; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Read failures block changes", () => With(f => { f.Provider.ThrowRead = true; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Management unknown blocks changes", () => With(f => { f.Context = f.Context with { IsManaged = null }; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Managed devices block changes", () => With(f => { f.Context = f.Context with { IsManaged = true }; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Elevation blocks current-user changes", () => With(f => { f.Context = f.Context with { IsElevated = true }; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Unsupported operating system blocks changes", () => With(f => { f.Context = f.Context with { WindowsBuild = 19045 }; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Provider compatibility block is respected", () => With(f => { f.Provider.Block = "Unsupported provider"; Equal(PlanItemStatus.Blocked, f.Plan().Items[0].Status); })),
    ("Blocked items cannot be applied", () => With(f => { f.Provider.Block = "Unsupported"; var p = f.Plan(); Throws(() => f.Engine.Apply(p, p.Digest)); Equal(0, f.Provider.Writes); })),
    ("Verified apply and exact absence recovery", () => With(f => { var r = f.Apply(); Equal("Succeeded", r.Status); Equal("Verified", r.Actions[0].Status); Check(f.Provider.States["test.one"].Exists); Equal("Recovered", f.Engine.Recover(r.RunId).Status); Equal(ResourceState.Missing, f.Provider.States["test.one"]); })),
    ("Original type and bytes are retained", () => With(f => { var original = new ResourceState(true, "String", "001"); f.Provider.States["test.one"] = original; var r = f.Apply(); Equal(original, f.Engine.Details(r.RunId)!.Actions[0].Before); Equal("Recovered", f.Engine.Recover(r.RunId).Status); Equal(original, f.Provider.States["test.one"]); })),
    ("History on a new installation creates no files", () => With(f => { Equal(0, f.Engine.History().Count); Check(!Directory.Exists(f.State)); Check(f.Engine.Details(Guid.NewGuid().ToString("N")) is null); Check(!Directory.Exists(f.State)); })),
    ("History preserves existing database contents", () => With(f => { f.Apply(); var before = SHA256.HashData(File.ReadAllBytes(f.Database)); Check(f.Engine.History().Count == 1); Equal(Convert.ToHexString(before), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f.Database)))); })),
    ("Wrong approval digest rejects writes", () => With(f => { var p = f.Plan(); Throws(() => f.Engine.Apply(p, new string('0',64))); Equal(0, f.Provider.Writes); })),
    ("Modified plan digest rejects writes", () => With(f => { var p = f.Plan(); var changed = p with { Items = [p.Items[0] with { Before = new ResourceState(true,"DWord","22") }] }; Throws(() => f.Engine.Apply(changed, p.Digest)); Equal(0, f.Provider.Writes); })),
    ("Rehashed false original state is rejected", () => With(f => { var p = f.Plan(); p = Rehash(p with { Items = [p.Items[0] with { Before = new ResourceState(true,"DWord","22") }] }); Throws(() => f.Engine.Apply(p, p.Digest)); Equal(0, f.Provider.Writes); })),
    ("Rehashed arbitrary desired value is rejected", () => With(f => { var p = f.Plan(); p = Rehash(p with { Items = [p.Items[0] with { Desired = new ResourceState(true,"DWord","999") }] }); Throws(() => f.Engine.Apply(p, p.Digest)); Equal(0, f.Provider.Writes); })),
    ("Rehashed misleading action title is rejected", () => With(f => { var p = f.Plan(); p = Rehash(p with { Items = [p.Items[0] with { Title = "Something different" }] }); Throws(() => f.Engine.Apply(p, p.Digest)); })),
    ("Expired plans are rejected", () => With(f => { var p = Rehash(f.Plan() with { CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-30) }); Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Overlong approval windows are rejected", () => With(f => { var p = Rehash(f.Plan() with { ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1) }); Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Changed catalog version is rejected", () => With(f => { var p = Rehash(f.Plan() with { CatalogVersion = "other" }); Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Changed schema version is rejected", () => With(f => { var p = Rehash(f.Plan() with { SchemaVersion = "9.0" }); Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Changed user rejects writes", () => With(f => { var p=f.Plan(); f.Context=f.Context with { UserId="another" }; Throws(() => f.Engine.Apply(p,p.Digest)); Equal(0,f.Provider.Writes); })),
    ("Changed machine rejects writes", () => With(f => { var p=f.Plan(); f.Context=f.Context with { MachineId="another" }; Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Changed build rejects writes", () => With(f => { var p=f.Plan(); f.Context=f.Context with { WindowsBuild=26200 }; Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Elevation after review rejects writes", () => With(f => { var p=f.Plan(); f.Context=f.Context with { IsElevated=true }; Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("External state drift rejects the whole plan", () => With(f => { var p=f.Plan("test.one","test.two"); f.Provider.States["test.two"]=new(true,"DWord","9"); Throws(() => f.Engine.Apply(p,p.Digest)); Equal(0,f.Provider.Writes); })),
    ("Already configured values are not written", () => With(f => { f.Apply(); var writes=f.Provider.Writes; var p=f.Plan(); Equal(PlanItemStatus.AlreadyConfigured,p.Items[0].Status); Equal("NoChanges",f.Engine.Apply(p,p.Digest).Status); Equal(writes,f.Provider.Writes); })),
    ("An applied then recovered plan cannot replay", () => With(f => { var p=f.Plan(); var r=f.Engine.Apply(p,p.Digest); f.Engine.Recover(r.RunId); Throws(() => f.Engine.Apply(p,p.Digest)); })),
    ("Write failure stops following actions", () => With(f => { f.Provider.ThrowBeforeWrite=true; var r=f.Apply("test.one","test.two"); Equal("Failed",r.Status); Equal(1,f.Provider.Writes); Equal("NotRun",r.Actions[1].Status); Equal(ResourceState.Missing,f.Provider.States["test.two"]); })),
    ("Partial mutation can restore exact originals", () => With(f => { f.Provider.AfterWrite=(_,_) => throw new IOException("Failure after write"); var r=f.Apply(); Equal("Failed",r.Status); f.Provider.AfterWrite=null; Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(ResourceState.Missing,f.Provider.States["test.one"]); })),
    ("Verification failure never reports success", () => With(f => { f.Provider.AfterWrite=(a,_) => f.Provider.States[a.Id]=new(true,"DWord","9"); var r=f.Apply(); Equal("Failed",r.Status); Check(r.Actions.All(a=>a.Status!="Verified")); })),
    ("Divergent recovery preserves outside changes", () => With(f => { var r=f.Apply(); f.Provider.States["test.one"]=new(true,"DWord","9"); int before=f.Provider.Writes; var recovery=f.Engine.Recover(r.RunId); Equal("RecoveryBlocked",recovery.Status); Equal("RecoveryConflict",recovery.Actions[0].Status); Equal(before,f.Provider.Writes); Equal("9",f.Provider.States["test.one"].Data!); })),
    ("Journal creation failure prevents mutations", () => With(f => { File.WriteAllText(f.State,"Occupied path"); Throws(()=>f.Apply()); Equal(0,f.Provider.Writes); })),
    ("Concurrent writer lock prevents mutations", () => With(f => { Directory.CreateDirectory(f.State); using var held=new FileStream(Path.Combine(f.State,"writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); Throws(()=>f.Apply()); Equal(0,f.Provider.Writes); })),
    ("Pending failure blocks unrelated new apply", () => With(f => { f.Provider.ThrowBeforeWrite=true; f.Apply(); f.Provider.ThrowBeforeWrite=false; int before=f.Provider.Writes; Throws(()=>f.Apply("test.two")); Equal(before,f.Provider.Writes); })),
    ("Interrupted prepared run recovers without writes", () => With(f => { var r=f.Apply(); f.Sql("UPDATE runs SET status='Prepared'; UPDATE actions SET status='Prepared'"); f.Provider.States["test.one"]=ResourceState.Missing; int before=f.Provider.Writes; Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes); })),
    ("Interrupted applying with original state is safe", () => With(f => { var r=f.Apply(); f.Sql("UPDATE runs SET status='Applying'; UPDATE actions SET status='Applying'"); f.Provider.States["test.one"]=ResourceState.Missing; int before=f.Provider.Writes; Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes); })),
    ("Interrupted applying with desired state restores", () => With(f => { var r=f.Apply(); f.Sql("UPDATE runs SET status='Applying'; UPDATE actions SET status='Applying'"); Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(ResourceState.Missing,f.Provider.States["test.one"]); })),
    ("Interrupted applying with divergence stays blocked", () => With(f => { var r=f.Apply(); f.Sql("UPDATE runs SET status='Applying'; UPDATE actions SET status='Applying'"); f.Provider.States["test.one"]=new(true,"DWord","9"); int before=f.Provider.Writes; Equal("RecoveryBlocked",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes); })),
    ("Recovery is idempotent", () => With(f => { var r=f.Apply(); f.Engine.Recover(r.RunId); int before=f.Provider.Writes; Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes); })),
    ("Recovery checks user identity", () => With(f => { var r=f.Apply(); f.Context=f.Context with { UserId="another" }; int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes); })),
    ("Recovery blocks elevated execution", () => With(f => { var r=f.Apply(); f.Context=f.Context with { IsElevated=true }; Throws(()=>f.Engine.Recover(r.RunId)); })),
    ("Recovery failure remains pending", () => With(f => { var r=f.Apply(); f.Provider.ThrowBeforeWrite=true; Equal("RecoveryBlocked",f.Engine.Recover(r.RunId).Status); Equal("RecoveryBlocked",f.Engine.History()[0].Status); f.Provider.ThrowBeforeWrite=false; Equal("Recovered",f.Engine.Recover(r.RunId).Status); })),
    ("Unknown current state prevents recovery writes", () => With(f => { var r=f.Apply(); f.Provider.ReadStatus=ObservationState.Unknown; int before=f.Provider.Writes; Equal("RecoveryBlocked",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes); })),
    ("Unknown journal target fails closed", () => With(f => { var r=f.Apply(); f.Sql("UPDATE actions SET action_id='evil.action'"); int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes); })),
    ("Tampered desired recovery value is rejected", () => With(f => { var r=f.Apply(); f.Sql("UPDATE actions SET desired_json='{\"exists\":true,\"kind\":\"DWord\",\"data\":\"999\"}'"); int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes); })),
    ("Malformed absent resource is blocked", () => With(f => { f.Provider.States["test.one"]=new(false,"DWord","1"); Equal(PlanItemStatus.Blocked,f.Plan().Items[0].Status); })),
    ("Invalid run identifiers cannot select files", () => With(f => Throws(()=>f.Engine.Recover("../outside")))),
    ("Provider writes see a committed Applying record", () => With(f => { f.Provider.AfterWrite=(_,_) => { using var c=new SqliteConnection($"Data Source={f.Database};Mode=ReadOnly;Pooling=False"); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="SELECT status FROM actions LIMIT 1"; Equal("Applying",(string)cmd.ExecuteScalar()!); }; Equal("Succeeded",f.Apply().Status); })),
    ("Catalog profile arrays are detached from caller", () => { var profiles=new[]{"Everyday"}; var c=new ActionCatalog("1",[Fixture.Definition("test.one") with { Profiles=profiles }]); profiles[0]="Changed"; Equal("Everyday",c.Actions[0].Profiles[0]); }),
    ("Unattempted actions preserve matching outside changes", () => With(f =>
    {
        var r=f.Apply("test.one","test.two");
        f.Sql("UPDATE runs SET status='Applying'; UPDATE actions SET status='Prepared' WHERE action_id='test.two'");
        int before=f.Provider.Writes;
        var recovered=f.Engine.Recover(r.RunId);
        Equal("Recovered",recovered.Status);
        Equal("NotChanged",recovered.Actions.Single(a=>a.ActionId=="test.two").Status);
        Equal(new ResourceState(true,"DWord","1"),f.Provider.States["test.two"]);
        Equal(before+1,f.Provider.Writes);
    })),
    ("Pre-mutation failure cannot undo another process's change", () => With(f =>
    {
        f.Provider.AfterWrite=(a,_) => { if(a.Id=="test.one") f.Provider.States["test.two"]=new(true,"DWord","1"); };
        var r=f.Apply("test.one","test.two");
        Equal("Failed",r.Status);
        Equal("NotRun",f.Engine.Details(r.RunId)!.Actions.Single(a=>a.ActionId=="test.two").Status);
        f.Provider.AfterWrite=null;
        Equal("Recovered",f.Engine.Recover(r.RunId).Status);
        Equal(new ResourceState(true,"DWord","1"),f.Provider.States["test.two"]);
    })),
    ("Recovery retry preserves changes to an already restored action", () => With(f =>
    {
        var r=f.Apply("test.one","test.two");
        f.Provider.States["test.one"]=new(true,"DWord","9");
        Equal("RecoveryBlocked",f.Engine.Recover(r.RunId).Status);
        Equal(ResourceState.Missing,f.Provider.States["test.two"]);
        f.Provider.States["test.two"]=new(true,"DWord","1");
        f.Provider.States["test.one"]=ResourceState.Missing;
        int before=f.Provider.Writes;
        Equal("Recovered",f.Engine.Recover(r.RunId).Status);
        Equal(before,f.Provider.Writes);
        Equal(new ResourceState(true,"DWord","1"),f.Provider.States["test.two"]);
    })),
    ("Completed recovery never rewrites later changes", () => With(f =>
    {
        var r=f.Apply(); f.Engine.Recover(r.RunId);
        f.Provider.States["test.one"]=new(true,"DWord","1"); int before=f.Provider.Writes;
        Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes);
    })),
    ("Tampered recovery original is rejected before writes", () => With(f =>
    {
        var r=f.Apply(); f.Sql("UPDATE actions SET before_json='{\"exists\":true,\"kind\":\"DWord\",\"data\":\"999\"}'");
        int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes);
    })),
    ("Deleted recovery action is detected before writes", () => With(f =>
    {
        var r=f.Apply("test.one","test.two"); f.Sql("DELETE FROM actions WHERE action_id='test.two'");
        int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes);
    })),
    ("Edited recovery title is detected before writes", () => With(f =>
    {
        var r=f.Apply(); f.Sql("UPDATE actions SET title='Misleading title'");
        int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes);
    })),
    ("Unsupported journal schema blocks all new writes", () => With(f =>
    {
        f.Apply(); f.Sql("UPDATE metadata SET schema_version=99"); int before=f.Provider.Writes;
        Throws(()=>f.Apply("test.two")); Equal(before,f.Provider.Writes); Throws(()=>f.Engine.History());
    })),
    ("Newer overlapping runs must be recovered first", () => With(f =>
    {
        var older=f.Apply(); f.Provider.States["test.one"]=ResourceState.Missing; var newer=f.Apply();
        int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(older.RunId)); Equal(before,f.Provider.Writes);
        Equal("Recovered",f.Engine.Recover(newer.RunId).Status); Equal("Recovered",f.Engine.Recover(older.RunId).Status);
    })),
    ("Overlap ordering does not depend on clock timestamps", () => With(f =>
    {
        var older=f.Apply(); f.Provider.States["test.one"]=ResourceState.Missing; var newer=f.Apply();
        f.Sql($"UPDATE runs SET started_utc='2000-01-01T00:00:00.0000000+00:00' WHERE run_id='{newer.RunId}'");
        int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(older.RunId)); Equal(before,f.Provider.Writes);
    })),
    ("Older unrelated runs may be recovered independently", () => With(f =>
    {
        var older=f.Apply(); f.Apply("test.two"); Equal("Recovered",f.Engine.Recover(older.RunId).Status);
        Equal(new ResourceState(true,"DWord","1"),f.Provider.States["test.two"]);
    })),
    ("Recovery checks every original before any writes", () => With(f =>
    {
        var r=f.Apply("test.one","test.two");
        f.Sql("UPDATE actions SET before_json='{\"exists\":false,\"kind\":\"DWord\",\"data\":\"99\"}' WHERE action_id='test.one'");
        int before=f.Provider.Writes; Throws(()=>f.Engine.Recover(r.RunId)); Equal(before,f.Provider.Writes);
    })),
    ("Failure recording verification stops later writes", () => With(f =>
    {
        f.Provider.AfterWrite=(_,_) => f.Sql("CREATE TRIGGER reject_verified BEFORE UPDATE ON actions WHEN NEW.status='Verified' BEGIN SELECT RAISE(FAIL,'Simulated persistence failure'); END");
        var r=f.Apply("test.one","test.two"); Equal("Failed",r.Status); Equal(1,f.Provider.Writes);
        Equal("NotRun",r.Actions.Single(a=>a.ActionId=="test.two").Status);
        f.Provider.AfterWrite=null; f.Sql("DROP TRIGGER reject_verified");
        Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(ResourceState.Missing,f.Provider.States["test.one"]);
    })),
    ("Failure recording completion remains recoverable", () => With(f =>
    {
        f.Provider.AfterWrite=(_,_) => f.Sql("CREATE TRIGGER reject_completion BEFORE UPDATE ON runs WHEN NEW.status='Succeeded' BEGIN SELECT RAISE(FAIL,'Simulated persistence failure'); END");
        var r=f.Apply(); Equal("Failed",r.Status); Equal("Applying",f.Engine.History()[0].Status);
        f.Provider.AfterWrite=null; int before=f.Provider.Writes; Throws(()=>f.Apply("test.two")); Equal(before,f.Provider.Writes);
        Equal("Recovered",f.Engine.Recover(r.RunId).Status);
    })),
    ("Failure recording restoration does not lose the original", () => With(f =>
    {
        var r=f.Apply();
        f.Sql("CREATE TRIGGER reject_restored BEFORE UPDATE ON actions WHEN NEW.status='Restored' BEGIN SELECT RAISE(FAIL,'Simulated persistence failure'); END");
        Equal("RecoveryBlocked",f.Engine.Recover(r.RunId).Status); Equal(ResourceState.Missing,f.Provider.States["test.one"]);
        f.Sql("DROP TRIGGER reject_restored"); int before=f.Provider.Writes;
        Equal("Recovered",f.Engine.Recover(r.RunId).Status); Equal(before,f.Provider.Writes);
    }))
};
int failures=0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch(Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}
Console.WriteLine($"{tests.Length-failures}/{tests.Length} core checks passed. No Windows provider was instantiated.");
return failures==0 ? 0 : 1;

static ChangePlan Rehash(ChangePlan p)=>p with { Digest=PlanIntegrity.ComputeDigest(p) };
static void With(Action<Fixture> action) { using var fixture=new Fixture(); action(fixture); }
static void Check(bool condition) { if(!condition) throw new Exception("Assertion failed."); }
static void Equal<T>(T expected,T actual) { if(!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}."); }
static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection."); }

sealed class Fixture : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"WindowsWorkbench-tests",Guid.NewGuid().ToString("N"));
    public string State=>Path.Combine(root,"state");
    public string Database=>Path.Combine(State,"journal.sqlite");
    public DeviceContext Context=new() { MachineId="test-machine",UserId="test-user",WindowsBuild=26100,Edition="Professional",Architecture="X64",IsManaged=false };
    public FakeProvider Provider { get; }=new();
    public ActionCatalog Catalog { get; }=new("test-v1",[Definition("test.one"),Definition("test.two"),Definition("test.guide") with { Mode=ActionMode.Guided }]);
    public Planner Planner=>new(Catalog,Provider);
    public ChangeEngine Engine=>new(Catalog,Provider,()=>Context,State);
    public Fixture() { Directory.CreateDirectory(root); }
    public ChangePlan Plan(params string[] ids)=>Planner.Create(Context,ids.Length==0?["test.one"]:ids);
    public RunResult Apply(params string[] ids) { var p=Plan(ids); return Engine.Apply(p,p.Digest); }
    public void Sql(string sql) { using var c=new SqliteConnection($"Data Source={Database};Pooling=False"); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText=sql; cmd.ExecuteNonQuery(); }
    public static ActionDefinition Definition(string id)=>new() { Id=id,Title=id,Category="Test",Summary="Test preference",Benefit="Test",Tradeoff="Test",Mode=ActionMode.Preference };
    public void Dispose()
    {
        // Only remove this fixture's unique directory under the dedicated test root.
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"WindowsWorkbench-tests"))+Path.DirectorySeparatorChar;
        if(Path.GetFullPath(root).StartsWith(allowed,StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root,true);
    }
}

sealed class FakeProvider : IActionProvider
{
    public Dictionary<string,ResourceState> States { get; }=new() { ["test.one"]=ResourceState.Missing,["test.two"]=ResourceState.Missing };
    public int Writes;
    public ObservationState ReadStatus=ObservationState.Known;
    public bool ThrowRead;
    public bool ThrowBeforeWrite;
    public string? Block;
    public Action<ActionDefinition,ResourceState>? AfterWrite;
    public Observation Read(ActionDefinition action) { if(ThrowRead) throw new IOException("Inventory failed"); return new(ReadStatus,ReadStatus==ObservationState.Known?States[action.Id]:null,"Simulated observation"); }
    public ResourceState DesiredState(ActionDefinition action)=>new(true,"DWord","1");
    public string? GetBlockReason(ActionDefinition action,DeviceContext context)=>Block;
    public void Write(ActionDefinition action,ResourceState state)
    {
        Writes++;
        if(ThrowBeforeWrite) throw new IOException("Simulated write denied");
        States[action.Id]=state;
        AfterWrite?.Invoke(action,state);
    }
}
