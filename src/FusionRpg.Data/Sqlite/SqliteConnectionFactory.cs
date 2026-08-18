using Microsoft.Data.Sqlite;

namespace FusionRpg.Data.Sqlite;

/// <summary>Opens SQLite with WAL pragmas on every connection.</summary>
public static class SqliteConnectionFactory
{
    public static SqliteConnection Open(string path, bool readOnly = false)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = full,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        var db = new SqliteConnection(cs);
        db.Open();
        ApplyWalPragmas(db);
        return db;
    }

    public static void ApplyWalPragmas(SqliteConnection db)
    {
        Exec(db, "PRAGMA busy_timeout=5000;");
        Exec(db, "PRAGMA synchronous=NORMAL;");
        Exec(db, "PRAGMA temp_store=MEMORY;");
        Exec(db, "PRAGMA journal_mode=WAL;");
        Exec(db, "PRAGMA wal_autocheckpoint=1000;");
    }

    static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
