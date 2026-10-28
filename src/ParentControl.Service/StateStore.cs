using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace ParentControl.Service;

public interface IStateStore
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<ControlState> GetStateAsync(CancellationToken ct = default);
    Task<ControlState> SetStateAsync(bool locked, DateTimeOffset? until, string reason, CancellationToken ct = default);
    Task<bool> HasParentAsync(CancellationToken ct = default);
    Task CreateParentAsync(string user, string password, CancellationToken ct = default);
    Task<bool> VerifyParentAsync(string user, string password, CancellationToken ct = default);
    Task AddAuditAsync(string action, string detail, string remote, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleRule>> GetScheduleAsync(CancellationToken ct = default);
    Task SaveScheduleAsync(IEnumerable<ScheduleRule> rules, CancellationToken ct = default);
}

public sealed class SqliteStateStore : IStateStore
{
    private readonly string _connectionString;
    public SqliteStateStore(IHostEnvironment env)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ParentControlRemote");
        Directory.CreateDirectory(root);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "parentcontrol.db"), Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(_connectionString); c.Open(); return c; }
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var c = Open();
        var sql = """
        PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;
        CREATE TABLE IF NOT EXISTS state(id INTEGER PRIMARY KEY CHECK(id=1),locked INTEGER NOT NULL,locked_until TEXT,reason TEXT NOT NULL,updated_at TEXT NOT NULL,last_lock_at TEXT,revision INTEGER NOT NULL);
        INSERT OR IGNORE INTO state VALUES(1,0,NULL,'Initial',datetime('now'),NULL,0);
        CREATE TABLE IF NOT EXISTS parents(id INTEGER PRIMARY KEY,user_name TEXT NOT NULL UNIQUE,salt BLOB NOT NULL,hash BLOB NOT NULL,iterations INTEGER NOT NULL,created_at TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY AUTOINCREMENT,at TEXT NOT NULL,action TEXT NOT NULL,detail TEXT NOT NULL,remote TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS schedule(id INTEGER PRIMARY KEY AUTOINCREMENT,day INTEGER NOT NULL,start TEXT NOT NULL,end TEXT NOT NULL,locked INTEGER NOT NULL,mode TEXT NOT NULL);
        """;
        await new SqliteCommand(sql, c).ExecuteNonQueryAsync(ct);
    }
    public async Task<ControlState> GetStateAsync(CancellationToken ct = default)
    {
        await using var c = Open(); await using var r = await new SqliteCommand("SELECT locked,locked_until,reason,updated_at,last_lock_at,revision FROM state WHERE id=1", c).ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Status fehlt.");
        return new(r.GetBoolean(0), Parse(r,1), r.GetString(2), DateTimeOffset.Parse(r.GetString(3)), Parse(r,4), r.GetInt64(5));
    }
    public async Task<ControlState> SetStateAsync(bool locked, DateTimeOffset? until, string reason, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow; await using var c = Open();
        var q = new SqliteCommand("UPDATE state SET locked=$l,locked_until=$u,reason=$r,updated_at=$n,last_lock_at=CASE WHEN $l=1 THEN $n ELSE last_lock_at END,revision=revision+1 WHERE id=1", c);
        q.Parameters.AddWithValue("$l", locked); q.Parameters.AddWithValue("$u", (object?)until?.ToString("O") ?? DBNull.Value); q.Parameters.AddWithValue("$r", reason); q.Parameters.AddWithValue("$n", now.ToString("O"));
        await q.ExecuteNonQueryAsync(ct); return await GetStateAsync(ct);
    }
    public async Task<bool> HasParentAsync(CancellationToken ct=default) { await using var c=Open(); return Convert.ToInt64(await new SqliteCommand("SELECT COUNT(*) FROM parents",c).ExecuteScalarAsync(ct))>0; }
    public async Task CreateParentAsync(string user,string password,CancellationToken ct=default)
    {
        if (user.Length is < 3 or > 64 || password.Length < 12) throw new ArgumentException("Benutzername mindestens 3, Passwort mindestens 12 Zeichen.");
        var salt=RandomNumberGenerator.GetBytes(32); const int iterations=310_000; var hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,iterations,HashAlgorithmName.SHA256,32);
        await using var c=Open(); var q=new SqliteCommand("INSERT INTO parents(user_name,salt,hash,iterations,created_at) VALUES($u,$s,$h,$i,$at)",c);
        q.Parameters.AddWithValue("$u",user); q.Parameters.AddWithValue("$s",salt); q.Parameters.AddWithValue("$h",hash); q.Parameters.AddWithValue("$i",iterations); q.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O")); await q.ExecuteNonQueryAsync(ct);
    }
    public async Task<bool> VerifyParentAsync(string user,string password,CancellationToken ct=default)
    {
        await using var c=Open(); var q=new SqliteCommand("SELECT salt,hash,iterations FROM parents WHERE user_name=$u",c);q.Parameters.AddWithValue("$u",user);await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return false;
        var actual=Rfc2898DeriveBytes.Pbkdf2(password,(byte[])r[0],r.GetInt32(2),HashAlgorithmName.SHA256,32);return CryptographicOperations.FixedTimeEquals(actual,(byte[])r[1]);
    }
    public async Task AddAuditAsync(string action,string detail,string remote,CancellationToken ct=default){await using var c=Open();var q=new SqliteCommand("INSERT INTO audit(at,action,detail,remote) VALUES($a,$x,$d,$r)",c);q.Parameters.AddWithValue("$a",DateTimeOffset.UtcNow.ToString("O"));q.Parameters.AddWithValue("$x",action);q.Parameters.AddWithValue("$d",detail);q.Parameters.AddWithValue("$r",remote);await q.ExecuteNonQueryAsync(ct);}
    public async Task<IReadOnlyList<ScheduleRule>> GetScheduleAsync(CancellationToken ct=default){var list=new List<ScheduleRule>();await using var c=Open();await using var r=await new SqliteCommand("SELECT day,start,end,locked,mode FROM schedule ORDER BY day,start",c).ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))list.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetBoolean(3),r.GetString(4)));return list;}
    public async Task SaveScheduleAsync(IEnumerable<ScheduleRule> rules,CancellationToken ct=default){await using var c=Open();await using var tx=await c.BeginTransactionAsync(ct);await new SqliteCommand("DELETE FROM schedule",c,(SqliteTransaction)tx).ExecuteNonQueryAsync(ct);foreach(var r in rules){if(r.DayOfWeek is <0 or >6||!TimeOnly.TryParse(r.Start,out _)||!TimeOnly.TryParse(r.End,out _))throw new ArgumentException("Ungültiger Zeitplan.");var q=new SqliteCommand("INSERT INTO schedule(day,start,end,locked,mode) VALUES($d,$s,$e,$l,$m)",c,(SqliteTransaction)tx);q.Parameters.AddWithValue("$d",r.DayOfWeek);q.Parameters.AddWithValue("$s",r.Start);q.Parameters.AddWithValue("$e",r.End);q.Parameters.AddWithValue("$l",r.Locked);q.Parameters.AddWithValue("$m",r.Mode);await q.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);}
    private static DateTimeOffset? Parse(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:DateTimeOffset.Parse(r.GetString(i));
}
