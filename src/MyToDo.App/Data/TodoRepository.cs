using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MyToDo.App.Domain;

namespace MyToDo.App.Data;

public sealed class TodoRepository : ITodoRepository
{
    private readonly string _connectionString;

    public TodoRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("Database path is required.", nameof(databasePath));
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL)", cancellationToken);
        await ExecuteAsync(connection, transaction, "CREATE TABLE IF NOT EXISTS todos (id INTEGER PRIMARY KEY AUTOINCREMENT, device_name TEXT NOT NULL, windows_username TEXT NOT NULL, content TEXT NOT NULL, created_at_utc TEXT NOT NULL, updated_at_utc TEXT NULL, deleted_at_utc TEXT NULL, restored_at_utc TEXT NULL, completed_at_utc TEXT NULL, status TEXT NOT NULL CHECK (status IN ('todo','completed','deleted')))", cancellationToken);
        await ExecuteAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS ix_todos_status_created ON todos(status, created_at_utc DESC)", cancellationToken);
        await ExecuteAsync(connection, transaction, "CREATE INDEX IF NOT EXISTS ix_todos_status_completed ON todos(status, completed_at_utc DESC)", cancellationToken);
        var version = connection.CreateCommand(); version.Transaction = transaction; version.CommandText = "SELECT COUNT(*) FROM schema_info";
        if (Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            await ExecuteAsync(connection, transaction, "INSERT INTO schema_info(version) VALUES (1)", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TodoItem>> QueryAsync(TodoStatus status, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, device_name, windows_username, content, created_at_utc, updated_at_utc, deleted_at_utc, restored_at_utc, completed_at_utc, status FROM todos WHERE status = $status ORDER BY CASE WHEN status = 'completed' THEN completed_at_utc ELSE created_at_utc END DESC";
        command.Parameters.AddWithValue("$status", ToStorage(status));
        var result = new List<TodoItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadItem(reader));
        return result;
    }

    public async Task<TodoItem> CreateAsync(string deviceName, string windowsUsername, string content, CancellationToken cancellationToken = default)
    {
        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) throw new ArgumentException("Todo content cannot be empty.", nameof(content));
        var now = UtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO todos(device_name,windows_username,content,created_at_utc,status) VALUES($device,$user,$content,$created,'todo'); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$device", deviceName ?? string.Empty); command.Parameters.AddWithValue("$user", windowsUsername ?? string.Empty); command.Parameters.AddWithValue("$content", trimmed); command.Parameters.AddWithValue("$created", now);
        var id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return new TodoItem(id, deviceName ?? string.Empty, windowsUsername ?? string.Empty, trimmed, Parse(now), null, null, null, null, TodoStatus.Todo);
    }

    public Task RenameAsync(long id, string content, CancellationToken cancellationToken = default) => UpdateAsync(id, "content = $content, updated_at_utc = $now", new Dictionary<string, object?> { ["$content"] = (content ?? string.Empty).Trim() }, cancellationToken, true);
    public Task CompleteAsync(long id, CancellationToken cancellationToken = default) => UpdateAsync(id, "status = 'completed', completed_at_utc = $now", null, cancellationToken);
    public Task RestoreAsync(long id, CancellationToken cancellationToken = default) => UpdateAsync(id, "status = 'todo', restored_at_utc = $now", null, cancellationToken);
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => UpdateAsync(id, "status = 'deleted', deleted_at_utc = $now", null, cancellationToken);

    public async Task ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "UPDATE todos SET status='deleted', deleted_at_utc=$now WHERE status='completed'"; command.Parameters.AddWithValue("$now", UtcNow()); await command.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpdateAsync(long id, string setClause, Dictionary<string, object?>? values, CancellationToken cancellationToken, bool validateContent = false)
    {
        if (validateContent && (values!["$content"] as string)!.Length == 0) throw new ArgumentException("Todo content cannot be empty.", "content");
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"UPDATE todos SET {setClause} WHERE id=$id"; command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$now", UtcNow()); if (values != null) foreach (var value in values) command.Parameters.AddWithValue(value.Key, value.Value ?? DBNull.Value);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new InvalidOperationException($"Todo item {id} was not found."); await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken token) { var c = new SqliteConnection(_connectionString); await c.OpenAsync(token); return c; }
    private static async Task ExecuteAsync(SqliteConnection c, SqliteTransaction t, string sql, CancellationToken token) { await using var command = c.CreateCommand(); command.Transaction = t; command.CommandText = sql; await command.ExecuteNonQueryAsync(token); }
    private static string UtcNow() => DateTimeOffset.UtcNow.ToString("O");
    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    private static string? ReadNullable(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static TodoItem ReadItem(SqliteDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), Parse(r.GetString(4)), ReadNullable(r,5) is { } a ? Parse(a) : null, ReadNullable(r,6) is { } b ? Parse(b) : null, ReadNullable(r,7) is { } c ? Parse(c) : null, ReadNullable(r,8) is { } d ? Parse(d) : null, Enum.Parse<TodoStatus>(r.GetString(9), true));
    private static string ToStorage(TodoStatus s) => s switch { TodoStatus.Todo => "todo", TodoStatus.Completed => "completed", TodoStatus.Deleted => "deleted", _ => throw new ArgumentOutOfRangeException(nameof(s)) };
}
