using System.Text.Json;
using Microsoft.Data.Sqlite;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Storage.Sqlite;

public sealed class SqliteDatabase
{
    private readonly string _path;
    private readonly object _gate = new();
    private bool _ready;

    public SqliteDatabase(string path) => _path = path;

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
                    CREATE TABLE IF NOT EXISTS groups (
                      id TEXT PRIMARY KEY,
                      name TEXT NOT NULL UNIQUE
                    );
                    CREATE TABLE IF NOT EXISTS work_roles (
                      operator_id TEXT NOT NULL,
                      role TEXT NOT NULL,
                      PRIMARY KEY (operator_id, role)
                    );
                    CREATE TABLE IF NOT EXISTS group_roles (
                      operator_id TEXT NOT NULL,
                      group_id TEXT NOT NULL,
                      role TEXT NOT NULL,
                      PRIMARY KEY (operator_id, group_id)
                    );
                    CREATE TABLE IF NOT EXISTS templates (
                      id TEXT PRIMARY KEY,
                      group_id TEXT NOT NULL,
                      name TEXT NOT NULL,
                      components_json TEXT NOT NULL,
                      UNIQUE (group_id, name)
                    );
                    CREATE TABLE IF NOT EXISTS favorites (
                      id TEXT PRIMARY KEY,
                      name TEXT NOT NULL,
                      template_id TEXT NOT NULL,
                      surface TEXT NOT NULL,
                      connection_name TEXT NOT NULL,
                      parameters_json TEXT NOT NULL,
                      owner_id TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS keyword_rules (
                      id TEXT PRIMARY KEY,
                      phrase TEXT NOT NULL,
                      column_names_json TEXT NOT NULL,
                      source TEXT NOT NULL,
                      template_id TEXT,
                      owner_id TEXT NOT NULL
                    );
                    """;
                command.ExecuteNonQuery();
                _ready = true;
            }

            return connection;
        }
    }
}

public sealed class SqliteDirectory : IDirectory
{
    private readonly SqliteDatabase _database;

    public SqliteDirectory(SqliteDatabase database) => _database = database;

    public int OperatorCount()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM operators";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public void AddOperator(OperatorRecord record)
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

    public OperatorRecord? FindOperatorByName(string name) => Find("display_name", name.Trim());

    public OperatorRecord? FindOperatorById(string id) => Find("id", id);

    public IReadOnlyList<OperatorRecord> ListOperators()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, password_verifier, is_admin FROM operators ORDER BY display_name";
        using var reader = command.ExecuteReader();
        var list = new List<OperatorRecord>();
        while (reader.Read())
            list.Add(ReadOperator(reader));
        return list;
    }

    public bool TryAddGroup(string id, string name, out string? errorCode)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            errorCode = "blank_name";
            return false;
        }

        try
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO groups (id, name) VALUES ($id, $name)";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$name", trimmed);
            command.ExecuteNonQuery();
            errorCode = null;
            return true;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            errorCode = "duplicate_name";
            return false;
        }
    }

    public bool TryRenameGroup(string id, string name, out string? errorCode)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            errorCode = "blank_name";
            return false;
        }

        try
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE groups SET name = $name WHERE id = $id";
            command.Parameters.AddWithValue("$name", trimmed);
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() == 0)
            {
                errorCode = "blank_name";
                return false;
            }

            errorCode = null;
            return true;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            errorCode = "duplicate_name";
            return false;
        }
    }

    public bool TryDeleteGroup(string id, out string? errorCode)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using (var roles = connection.CreateCommand())
        {
            roles.Transaction = transaction;
            roles.CommandText = "DELETE FROM group_roles WHERE group_id = $id";
            roles.Parameters.AddWithValue("$id", id);
            roles.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        var deleted = command.ExecuteNonQuery() > 0;
        transaction.Commit();
        errorCode = deleted ? null : "blank_name";
        return deleted;
    }

    public IReadOnlyList<GroupRecord> ListGroups()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM groups ORDER BY name";
        using var reader = command.ExecuteReader();
        var list = new List<GroupRecord>();
        while (reader.Read())
            list.Add(new GroupRecord { Id = reader.GetString(0), Name = reader.GetString(1) });
        return list;
    }

    public GroupRecord? FindGroup(string id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name FROM groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new GroupRecord { Id = reader.GetString(0), Name = reader.GetString(1) };
    }

    public void ReplaceAssignments(string operatorId, IReadOnlyCollection<string> workRoles, IReadOnlyList<StoredGroupRole> groups)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using (var clearWork = connection.CreateCommand())
        {
            clearWork.Transaction = transaction;
            clearWork.CommandText = "DELETE FROM work_roles WHERE operator_id = $id";
            clearWork.Parameters.AddWithValue("$id", operatorId);
            clearWork.ExecuteNonQuery();
        }

        using (var clearGroups = connection.CreateCommand())
        {
            clearGroups.Transaction = transaction;
            clearGroups.CommandText = "DELETE FROM group_roles WHERE operator_id = $id";
            clearGroups.Parameters.AddWithValue("$id", operatorId);
            clearGroups.ExecuteNonQuery();
        }

        foreach (var role in workRoles.Distinct(StringComparer.Ordinal))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO work_roles (operator_id, role) VALUES ($id, $role)";
            insert.Parameters.AddWithValue("$id", operatorId);
            insert.Parameters.AddWithValue("$role", role);
            insert.ExecuteNonQuery();
        }

        foreach (var group in groups)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO group_roles (operator_id, group_id, role) VALUES ($id, $group, $role)";
            insert.Parameters.AddWithValue("$id", operatorId);
            insert.Parameters.AddWithValue("$group", group.GroupId);
            insert.Parameters.AddWithValue("$role", group.Role);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyCollection<string> WorkRolesFor(string operatorId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT role FROM work_roles WHERE operator_id = $id";
        command.Parameters.AddWithValue("$id", operatorId);
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    public IReadOnlyList<StoredGroupRole> GroupRolesFor(string operatorId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_id, role FROM group_roles WHERE operator_id = $id";
        command.Parameters.AddWithValue("$id", operatorId);
        using var reader = command.ExecuteReader();
        var list = new List<StoredGroupRole>();
        while (reader.Read())
            list.Add(new StoredGroupRole { GroupId = reader.GetString(0), Role = reader.GetString(1) });
        return list;
    }

    private OperatorRecord? Find(string column, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, password_verifier, is_admin FROM operators WHERE " + column + " = $value";
        command.Parameters.AddWithValue("$value", value);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadOperator(reader) : null;
    }

    private static OperatorRecord ReadOperator(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        DisplayName = reader.GetString(1),
        PasswordVerifier = reader.GetString(2),
        IsAdmin = reader.GetInt32(3) == 1,
    };
}

public sealed class SqliteTemplateStore : ITemplateStore
{
    private readonly SqliteDatabase _database;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteTemplateStore(SqliteDatabase database) => _database = database;

    public bool TrySave(TemplateRecord record, out string? errorCode)
    {
        var name = record.Name.Trim();
        if (name.Length == 0)
        {
            errorCode = "blank_name";
            return false;
        }

        var payload = JsonSerializer.Serialize(record.Components, Json);
        try
        {
            using var connection = _database.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO templates (id, group_id, name, components_json) VALUES ($id, $group, $name, $json)
                ON CONFLICT(id) DO UPDATE SET group_id = $group, name = $name, components_json = $json
                """;
            command.Parameters.AddWithValue("$id", record.Id);
            command.Parameters.AddWithValue("$group", record.GroupId);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$json", payload);
            command.ExecuteNonQuery();
            record.Name = name;
            errorCode = null;
            return true;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            errorCode = "duplicate_name";
            return false;
        }
    }

    public TemplateRecord? Find(string id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_id, name, components_json FROM templates WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<TemplateRecord> List()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_id, name, components_json FROM templates ORDER BY name";
        using var reader = command.ExecuteReader();
        var list = new List<TemplateRecord>();
        while (reader.Read())
        {
            var row = Read(reader);
            if (row is not null)
                list.Add(row);
        }

        return list;
    }

    public bool Delete(string id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM templates WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() > 0;
    }

    public bool GroupHasTemplates(string groupId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM templates WHERE group_id = $id";
        command.Parameters.AddWithValue("$id", groupId);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static TemplateRecord? Read(SqliteDataReader reader)
    {
        try
        {
            var components = JsonSerializer.Deserialize<List<ComponentDto>>(reader.GetString(3), Json) ?? [];
            return new TemplateRecord
            {
                Id = reader.GetString(0),
                GroupId = reader.GetString(1),
                Name = reader.GetString(2),
                Components = components,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class SqliteFavoriteStore : IFavoriteStore
{
    private readonly SqliteDatabase _database;

    public SqliteFavoriteStore(SqliteDatabase database) => _database = database;

    public bool TrySave(FavoriteRecord record, out string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(record.Name))
        {
            errorCode = "blank_name";
            return false;
        }

        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO favorites (id, name, template_id, surface, connection_name, parameters_json, owner_id)
            VALUES ($id, $name, $template, $surface, $connection, $json, $owner)
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$name", record.Name.Trim());
        command.Parameters.AddWithValue("$template", record.TemplateId);
        command.Parameters.AddWithValue("$surface", record.Surface);
        command.Parameters.AddWithValue("$connection", record.ConnectionName);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(record.Parameters));
        command.Parameters.AddWithValue("$owner", record.OwnerId);
        command.ExecuteNonQuery();
        errorCode = null;
        return true;
    }

    public FavoriteRecord? Find(string id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, template_id, surface, connection_name, parameters_json, owner_id FROM favorites WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<FavoriteRecord> ListByOwner(string ownerId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, template_id, surface, connection_name, parameters_json, owner_id FROM favorites WHERE owner_id = $owner ORDER BY name";
        command.Parameters.AddWithValue("$owner", ownerId);
        using var reader = command.ExecuteReader();
        var list = new List<FavoriteRecord>();
        while (reader.Read())
        {
            var row = Read(reader);
            if (row is not null)
                list.Add(row);
        }

        return list;
    }

    private static FavoriteRecord? Read(SqliteDataReader reader)
    {
        try
        {
            return new FavoriteRecord
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                TemplateId = reader.GetString(2),
                Surface = reader.GetString(3),
                ConnectionName = reader.GetString(4),
                Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(5)) ?? new Dictionary<string, string>(StringComparer.Ordinal),
                OwnerId = reader.GetString(6),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class SqliteKeywordStore : IKeywordStore
{
    private readonly SqliteDatabase _database;

    public SqliteKeywordStore(SqliteDatabase database) => _database = database;

    public void Add(KeywordRecord record)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO keyword_rules (id, phrase, column_names_json, source, template_id, owner_id)
            VALUES ($id, $phrase, $columns, $source, $template, $owner)
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$phrase", record.Phrase);
        command.Parameters.AddWithValue("$columns", JsonSerializer.Serialize(record.ColumnNames));
        command.Parameters.AddWithValue("$source", record.Source);
        command.Parameters.AddWithValue("$template", (object?)record.TemplateId ?? DBNull.Value);
        command.Parameters.AddWithValue("$owner", record.OwnerId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<KeywordRecord> List()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, phrase, column_names_json, source, template_id, owner_id FROM keyword_rules";
        using var reader = command.ExecuteReader();
        var list = new List<KeywordRecord>();
        while (reader.Read())
        {
            try
            {
                list.Add(new KeywordRecord
                {
                    Id = reader.GetString(0),
                    Phrase = reader.GetString(1),
                    ColumnNames = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? [],
                    Source = reader.GetString(3),
                    TemplateId = reader.IsDBNull(4) ? null : reader.GetString(4),
                    OwnerId = reader.GetString(5),
                });
            }
            catch (JsonException)
            {
            }
        }

        return list;
    }
}
