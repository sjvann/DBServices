using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Storage.Sqlite;

public sealed class SqliteDatabase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _path;
    private readonly object _gate = new();
    private bool _ready;

    public SqliteDatabase(string path) => _path = path;

    public static JsonSerializerOptions Serializer => Json;

    public SqliteConnection Open()
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            if (!_ready)
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS operators (
                      id TEXT PRIMARY KEY,
                      display_name TEXT NOT NULL UNIQUE,
                      password_verifier TEXT NOT NULL,
                      is_admin INTEGER NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS entries (
                      operator_id TEXT NOT NULL,
                      entry TEXT NOT NULL,
                      PRIMARY KEY (operator_id, entry)
                    );
                    CREATE TABLE IF NOT EXISTS styles (
                      id TEXT PRIMARY KEY,
                      name TEXT NOT NULL,
                      view_id TEXT NOT NULL,
                      owner_id TEXT NOT NULL,
                      shared INTEGER NOT NULL,
                      blocks_json TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS generation_records (
                      id TEXT PRIMARY KEY,
                      owner_id TEXT NOT NULL,
                      shared INTEGER NOT NULL,
                      blocks_json TEXT NOT NULL,
                      columns_json TEXT NOT NULL,
                      rows_json TEXT NOT NULL,
                      parameters_json TEXT NOT NULL,
                      created_utc TEXT NOT NULL
                    );
                    """;
                command.ExecuteNonQuery();
                _ready = true;
            }

            return connection;
        }
    }
}

public sealed class SqliteOperatorStore : IOperatorStore
{
    private readonly SqliteDatabase _database;

    public SqliteOperatorStore(SqliteDatabase database) => _database = database;

    public int Count()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM operators";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void Add(OperatorRecord record)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO operators (id, display_name, password_verifier, is_admin) VALUES ($id, $name, $verifier, $admin)";
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$name", record.DisplayName.Trim());
        command.Parameters.AddWithValue("$verifier", record.PasswordVerifier);
        command.Parameters.AddWithValue("$admin", record.IsAdmin ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public OperatorRecord? FindByName(string name) => Find("display_name", name.Trim());

    public OperatorRecord? FindById(string id) => Find("id", id);

    public IReadOnlyList<OperatorRecord> List()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, password_verifier, is_admin FROM operators ORDER BY display_name";
        using var reader = command.ExecuteReader();
        var list = new List<OperatorRecord>();
        while (reader.Read())
            list.Add(Read(reader));
        return list;
    }

    public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM entries WHERE operator_id = $id";
            delete.Parameters.AddWithValue("$id", operatorId);
            delete.ExecuteNonQuery();
        }

        foreach (var entry in entries)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO entries (operator_id, entry) VALUES ($id, $entry)";
            insert.Parameters.AddWithValue("$id", operatorId);
            insert.Parameters.AddWithValue("$entry", entry);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<string> EntriesFor(string operatorId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT entry FROM entries WHERE operator_id = $id ORDER BY entry";
        command.Parameters.AddWithValue("$id", operatorId);
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    private OperatorRecord? Find(string column, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, password_verifier, is_admin FROM operators WHERE " + column + " = $value";
        command.Parameters.AddWithValue("$value", value);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static OperatorRecord Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        DisplayName = reader.GetString(1),
        PasswordVerifier = reader.GetString(2),
        IsAdmin = reader.GetInt32(3) == 1,
    };
}

public sealed class SqliteStyleStore : IStyleStore
{
    private readonly SqliteDatabase _database;

    public SqliteStyleStore(SqliteDatabase database) => _database = database;

    public void Add(StyleRecord record)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO styles (id, name, view_id, owner_id, shared, blocks_json)
            VALUES ($id, $name, $view, $owner, $shared, $blocks)
            """;
        Bind(command, record);
        command.ExecuteNonQuery();
    }

    public bool TryUpdate(StyleRecord record)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE styles
            SET name = $name, view_id = $view, shared = $shared, blocks_json = $blocks
            WHERE id = $id AND owner_id = $owner
            """;
        Bind(command, record);
        return command.ExecuteNonQuery() == 1;
    }

    public StyleRecord? Find(string id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, view_id, owner_id, shared, blocks_json FROM styles WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<StyleRecord> ReadableBy(string operatorId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, view_id, owner_id, shared, blocks_json
            FROM styles
            WHERE shared = 1 OR owner_id = $owner
            ORDER BY name
            """;
        command.Parameters.AddWithValue("$owner", operatorId);
        using var reader = command.ExecuteReader();
        var list = new List<StyleRecord>();
        while (reader.Read())
            list.Add(Read(reader));
        return list;
    }

    public bool References(string viewId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM styles WHERE view_id = $view";
        command.Parameters.AddWithValue("$view", viewId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    public bool Delete(string id, string ownerId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM styles WHERE id = $id AND owner_id = $owner";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", ownerId);
        return command.ExecuteNonQuery() == 1;
    }

    private static void Bind(SqliteCommand command, StyleRecord record)
    {
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$name", record.Name);
        command.Parameters.AddWithValue("$view", record.ViewId);
        command.Parameters.AddWithValue("$owner", record.OwnerId);
        command.Parameters.AddWithValue("$shared", record.Shared ? 1 : 0);
        command.Parameters.AddWithValue("$blocks", JsonSerializer.Serialize(record.Blocks, SqliteDatabase.Serializer));
    }

    private static StyleRecord Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Name = reader.GetString(1),
        ViewId = reader.GetString(2),
        OwnerId = reader.GetString(3),
        Shared = reader.GetInt32(4) == 1,
        Blocks = JsonSerializer.Deserialize<List<StyleBlockDto>>(reader.GetString(5), SqliteDatabase.Serializer) ?? [],
    };
}

public sealed class SqliteRecordStore : IRecordStore
{
    private readonly SqliteDatabase _database;

    public SqliteRecordStore(SqliteDatabase database) => _database = database;

    public void Add(GenerationRecord record)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO generation_records
              (id, owner_id, shared, blocks_json, columns_json, rows_json, parameters_json, created_utc)
            VALUES ($id, $owner, $shared, $blocks, $columns, $rows, $parameters, $created)
            """;
        Bind(command, record);
        command.ExecuteNonQuery();
    }

    public GenerationRecord? Find(string id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, owner_id, shared, blocks_json, columns_json, rows_json, parameters_json, created_utc
            FROM generation_records WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<GenerationRecord> ReadableBy(string operatorId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, owner_id, shared, blocks_json, columns_json, rows_json, parameters_json, created_utc
            FROM generation_records
            WHERE shared = 1 OR owner_id = $owner
            ORDER BY created_utc
            """;
        command.Parameters.AddWithValue("$owner", operatorId);
        using var reader = command.ExecuteReader();
        var list = new List<GenerationRecord>();
        while (reader.Read())
            list.Add(Read(reader));
        return list;
    }

    public bool TryUpdateShared(string id, string ownerId, bool shared)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE generation_records SET shared = $shared WHERE id = $id AND owner_id = $owner";
        command.Parameters.AddWithValue("$shared", shared ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", ownerId);
        return command.ExecuteNonQuery() == 1;
    }

    public bool Delete(string id, string ownerId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM generation_records WHERE id = $id AND owner_id = $owner";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", ownerId);
        return command.ExecuteNonQuery() == 1;
    }

    private static void Bind(SqliteCommand command, GenerationRecord record)
    {
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$owner", record.OwnerId);
        command.Parameters.AddWithValue("$shared", record.Shared ? 1 : 0);
        command.Parameters.AddWithValue("$blocks", JsonSerializer.Serialize(record.Blocks, SqliteDatabase.Serializer));
        command.Parameters.AddWithValue("$columns", JsonSerializer.Serialize(record.Columns, SqliteDatabase.Serializer));
        command.Parameters.AddWithValue("$rows", JsonSerializer.Serialize(record.Rows, SqliteDatabase.Serializer));
        command.Parameters.AddWithValue("$parameters", JsonSerializer.Serialize(record.Parameters, SqliteDatabase.Serializer));
        command.Parameters.AddWithValue("$created", record.CreatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static GenerationRecord Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        OwnerId = reader.GetString(1),
        Shared = reader.GetInt32(2) == 1,
        Blocks = JsonSerializer.Deserialize<List<StyleBlockDto>>(reader.GetString(3), SqliteDatabase.Serializer) ?? [],
        Columns = JsonSerializer.Deserialize<List<string>>(reader.GetString(4), SqliteDatabase.Serializer) ?? [],
        Rows = JsonSerializer.Deserialize<List<List<string>>>(reader.GetString(5), SqliteDatabase.Serializer) ?? [],
        Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6), SqliteDatabase.Serializer)
            ?? new Dictionary<string, string>(StringComparer.Ordinal),
        CreatedUtc = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };
}
