using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Optimizer.Core;

internal sealed record JournalAction(int Sequence, string Id, string Title, string Status, ResourceState Before, ResourceState Desired, string Detail);
internal sealed record JournalRun(string Id, string PlanId, string CatalogVersion, DateTimeOffset StartedUtc, string Status, DeviceContext Context, IReadOnlyList<JournalAction> Actions);

internal sealed class Journal : IDisposable
{
    private readonly SqliteConnection connection;
    internal Journal(string path, bool readOnly)
    {
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2
        }.ToString());
        try
        {
            connection.Open();
            Execute("PRAGMA foreign_keys=ON; PRAGMA trusted_schema=OFF;");
            if (!readOnly)
            {
                Execute("PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;");
                Execute("""
                    CREATE TABLE IF NOT EXISTS metadata (schema_version INTEGER NOT NULL);
                    INSERT INTO metadata(schema_version) SELECT 2 WHERE NOT EXISTS(SELECT 1 FROM metadata);
                    CREATE TABLE IF NOT EXISTS runs (
                      run_id TEXT PRIMARY KEY, plan_id TEXT NOT NULL UNIQUE, catalog_version TEXT NOT NULL,
                      started_utc TEXT NOT NULL, status TEXT NOT NULL, context_json TEXT NOT NULL,
                      snapshot_hash TEXT NOT NULL);
                    CREATE TABLE IF NOT EXISTS actions (
                      run_id TEXT NOT NULL REFERENCES runs(run_id), sequence INTEGER NOT NULL,
                      action_id TEXT NOT NULL, title TEXT NOT NULL, status TEXT NOT NULL,
                      before_json TEXT NOT NULL, desired_json TEXT NOT NULL, detail TEXT NOT NULL,
                      PRIMARY KEY(run_id,sequence), UNIQUE(run_id,action_id));
                    """);
            }
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_version FROM metadata";
            using var reader = command.ExecuteReader();
            if (!reader.Read() || reader.GetInt32(0) != 2 || reader.Read()) throw new InvalidDataException("Unsupported recovery journal schema.");
        }
        catch { connection.Dispose(); throw; }
    }

    internal IReadOnlyList<RunSummary> History()
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT r.run_id,r.started_utc,r.status,COUNT(a.action_id) FROM runs r LEFT JOIN actions a ON r.run_id=a.run_id GROUP BY r.run_id ORDER BY r.started_utc DESC,r.run_id";
        using var reader = command.ExecuteReader();
        var results = new List<RunSummary>();
        while (reader.Read()) results.Add(new(reader.GetString(0), DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture), reader.GetString(2), reader.GetInt32(3)));
        return results.AsReadOnly();
    }

    internal bool ContainsPlan(string planId) => Scalar("SELECT COUNT(*) FROM runs WHERE plan_id=$id", ("$id", planId)) != 0;
    internal bool HasPending() => Scalar("SELECT COUNT(*) FROM runs WHERE status NOT IN ('Succeeded','Recovered')") != 0;
    internal bool HasLaterOverlap(JournalRun run) => Scalar("""
        SELECT COUNT(*) FROM actions newer JOIN runs r ON newer.run_id=r.run_id
        WHERE r.rowid>(SELECT rowid FROM runs WHERE run_id=$id) AND r.status<>'Recovered' AND newer.action_id IN
          (SELECT action_id FROM actions WHERE run_id=$id)
        """, ("$id", run.Id)) != 0;

    internal void Prepare(string id, ChangePlan plan)
    {
        var startedUtc = DateTimeOffset.UtcNow;
        var prepared = plan.Items.Where(i => i.Status == PlanItemStatus.Ready)
            .Select((i, sequence) => new JournalAction(sequence, i.ActionId, i.Title, "Prepared", i.Before!, i.Desired!, "Prepared before mutation.")).ToArray();
        var snapshot = new JournalRun(id, plan.PlanId, plan.CatalogVersion, startedUtc, "Prepared", plan.Context, prepared);
        using var transaction = connection.BeginTransaction();
        using var run = connection.CreateCommand();
        run.Transaction = transaction;
        run.CommandText = "INSERT INTO runs(run_id,plan_id,catalog_version,started_utc,status,context_json,snapshot_hash) VALUES($id,$plan,$version,$time,'Prepared',$context,$hash)";
        Add(run, ("$id", id), ("$plan", plan.PlanId), ("$version", plan.CatalogVersion), ("$time", startedUtc.ToString("O")),
            ("$context", JsonSerializer.Serialize(plan.Context, ActionCatalog.Json)), ("$hash", SnapshotHash(snapshot)));
        run.ExecuteNonQuery();
        int sequence = 0;
        foreach (var item in plan.Items.Where(i => i.Status == PlanItemStatus.Ready))
        {
            using var action = connection.CreateCommand();
            action.Transaction = transaction;
            action.CommandText = "INSERT INTO actions(run_id,sequence,action_id,title,status,before_json,desired_json,detail) VALUES($run,$sequence,$id,$title,'Prepared',$before,$desired,'Prepared before mutation.')";
            Add(action, ("$run", id), ("$sequence", sequence++), ("$id", item.ActionId), ("$title", item.Title),
                ("$before", JsonSerializer.Serialize(item.Before, ActionCatalog.Json)), ("$desired", JsonSerializer.Serialize(item.Desired, ActionCatalog.Json)));
            action.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    internal JournalRun? Read(string id)
    {
        string planId, version, status, snapshotHash; DateTimeOffset time; DeviceContext context;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT plan_id,catalog_version,started_utc,status,context_json,snapshot_hash FROM runs WHERE run_id=$id";
            Add(command, ("$id", id));
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            planId = reader.GetString(0); version = reader.GetString(1);
            time = DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture);
            status = reader.GetString(3);
            context = JsonSerializer.Deserialize<DeviceContext>(reader.GetString(4), ActionCatalog.Json) ?? throw new InvalidDataException("Missing journal context.");
            snapshotHash = reader.GetString(5);
        }
        var actions = new List<JournalAction>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sequence,action_id,title,status,before_json,desired_json,detail FROM actions WHERE run_id=$id ORDER BY sequence";
            Add(command, ("$id", id));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var before = JsonSerializer.Deserialize<ResourceState>(reader.GetString(4), ActionCatalog.Json) ?? throw new InvalidDataException("Missing original state.");
                var desired = JsonSerializer.Deserialize<ResourceState>(reader.GetString(5), ActionCatalog.Json) ?? throw new InvalidDataException("Missing requested state.");
                StateValidation.Validate(before); StateValidation.Validate(desired);
                if (reader.GetInt32(0) != actions.Count || actions.Count >= 1000) throw new InvalidDataException("Invalid journal action sequence.");
                actions.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), before, desired, reader.GetString(6)));
            }
        }
        var result = new JournalRun(id, planId, version, time, status, context, actions.AsReadOnly());
        if (actions.Count == 0 || !PlanIntegrity.Matches(snapshotHash, SnapshotHash(result)))
            throw new InvalidDataException("Recovery originals or metadata are damaged or were edited. No settings were changed.");
        return result;
    }

    // Detects corruption and ordinary manual edits. This is not authentication
    // against a program with the same user's access to this journal.
    private static string SnapshotHash(JournalRun run)
    {
        var immutable = new
        {
            run.Id, run.PlanId, run.CatalogVersion, run.StartedUtc, run.Context,
            Actions = run.Actions.Select(a => new { a.Sequence, a.Id, a.Title, a.Before, a.Desired }).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(immutable, ActionCatalog.Json))));
    }

    internal void SetRun(string id, string status)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE runs SET status=$status WHERE run_id=$id";
        Add(command, ("$id", id), ("$status", status));
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Recovery run disappeared.");
    }
    internal void SetAction(string runId, int sequence, string status, string detail)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE actions SET status=$status,detail=$detail WHERE run_id=$id AND sequence=$sequence";
        Add(command, ("$id", runId), ("$sequence", sequence), ("$status", status), ("$detail", detail));
        if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Recovery action disappeared.");
    }
    private long Scalar(string sql, params (string Key, object Value)[] values)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; Add(command, values);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
    private void Execute(string sql) { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private static void Add(SqliteCommand command, params (string Key, object Value)[] values)
    { foreach (var pair in values) command.Parameters.AddWithValue(pair.Key, pair.Value); }
    public void Dispose() => connection.Dispose();
}

internal static class SafeStorage
{
    internal static string ValidateDirectory(string directory)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("Recovery storage must have an absolute local path.");
        var full = Path.GetFullPath(directory);
        if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal)) throw new InvalidOperationException("Network recovery storage is unsupported.");
        CheckPath(full);
        foreach (var suffix in new[] { "journal.sqlite", "journal.sqlite-journal", "journal.sqlite-wal", "journal.sqlite-shm", "writer.lock" }) CheckPath(Path.Combine(full, suffix));
        return full;
    }
    private static void CheckPath(string path)
    {
        for (string? candidate = path; !string.IsNullOrEmpty(candidate); candidate = Path.GetDirectoryName(candidate))
        {
            try
            {
                if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Recovery storage cannot contain symbolic links or reparse points.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    internal static FileStream Acquire(string directory)
    {
        ValidateDirectory(directory);
        Directory.CreateDirectory(directory);
        ValidateDirectory(directory);
        try { return new FileStream(Path.Combine(directory, "writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException("Another change or recovery may be running, or recovery storage is unavailable.", ex); }
    }
}
