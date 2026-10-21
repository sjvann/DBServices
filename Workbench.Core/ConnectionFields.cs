using System.Data.Common;
using Workbench.Contracts;

namespace Workbench.Core;

public sealed class ConnectionFields
{
    public string Place { get; set; } = "file";
    public string FilePath { get; set; } = "";
    public string OpenMode { get; set; } = "ReadWriteCreate";
    public string Host { get; set; } = "";
    public string Port { get; set; } = "";
    public string Database { get; set; } = "";
    public string Auth { get; set; } = "sql";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string Encrypt { get; set; } = "Mandatory";
    public string TrustCertificate { get; set; } = "False";
    public string SslMode { get; set; } = "";
    public string OracleKind { get; set; } = "service";
    public string OracleName { get; set; } = "";

    public static ConnectionFields For(string? serverType) => serverType?.Trim() switch
    {
        "SqlServer" => new ConnectionFields
        {
            Host = "localhost",
            Port = "1433",
            Auth = "sql",
            Encrypt = "Mandatory",
            TrustCertificate = "False",
        },
        "MySQL" => new ConnectionFields
        {
            Host = "localhost",
            Port = "3306",
            SslMode = "Preferred",
        },
        "Oracle" => new ConnectionFields
        {
            Host = "localhost",
            Port = "1521",
            OracleKind = "service",
        },
        "PostgreSQL" => new ConnectionFields
        {
            Host = "localhost",
            Port = "5432",
            SslMode = "Prefer",
        },
        _ => new ConnectionFields
        {
            Place = "file",
            OpenMode = "ReadWriteCreate",
        },
    };

    public OpResult<string> Compose(string? serverType) => serverType?.Trim() switch
    {
        "Sqlite" => Sqlite(),
        "SqlServer" => SqlServer(),
        "MySQL" => MySql(),
        "Oracle" => Oracle(),
        "PostgreSQL" => PostgreSql(),
        _ => Fail("請選擇資料庫種類。"),
    };

    private OpResult<string> Sqlite()
    {
        if (Place == "memory")
            return Ok(Build(("Data Source", ":memory:")));
        if (Place != "file")
            return Fail("請選擇位置。");
        var path = FilePath.Trim();
        if (path.Length == 0)
            return Fail("請填寫檔案路徑。");
        if (Broken(path))
            return Fail("檔案路徑不能換行。");
        if (OpenMode is not ("ReadWriteCreate" or "ReadWrite" or "ReadOnly"))
            return Fail("請選擇開啟模式。");
        return Ok(Build(("Data Source", path), ("Mode", OpenMode)));
    }

    private OpResult<string> SqlServer()
    {
        var endpoint = Endpoint();
        if (endpoint.Error is not null)
            return Fail(endpoint.Error);
        var database = Database.Trim();
        if (database.Length == 0)
            return Fail("請填寫資料庫。");
        if (HasSeparator(database))
            return Fail("資料庫名稱不能包含分號。");
        if (Encrypt is not ("Mandatory" or "Optional" or "Strict" or "False"))
            return Fail("請選擇加密。");
        if (TrustCertificate is not ("True" or "False"))
            return Fail("請選擇憑證處理。");

        var server = endpoint.Host.Contains(':') && !endpoint.Host.StartsWith('[')
            ? "[" + endpoint.Host + "]"
            : endpoint.Host;
        server += "," + endpoint.Port;

        if (Auth == "windows")
            return Ok(Build(
                ("Server", server),
                ("Database", database),
                ("Integrated Security", "True"),
                ("Encrypt", Encrypt),
                ("TrustServerCertificate", TrustCertificate)));
        if (Auth != "sql")
            return Fail("請選擇驗證方式。");
        var account = Account();
        if (account.Error is not null)
            return Fail(account.Error);
        return Ok(Build(
            ("Server", server),
            ("Database", database),
            ("User ID", account.User),
            ("Password", account.Password),
            ("Encrypt", Encrypt),
            ("TrustServerCertificate", TrustCertificate)));
    }

    private OpResult<string> MySql()
    {
        var endpoint = Endpoint();
        if (endpoint.Error is not null)
            return Fail(endpoint.Error);
        var database = Database.Trim();
        if (database.Length == 0)
            return Fail("請填寫資料庫。");
        if (HasSeparator(database))
            return Fail("資料庫名稱不能包含分號。");
        if (SslMode is not ("Preferred" or "Required" or "None" or "VerifyCA" or "VerifyFull"))
            return Fail("請選擇加密連線。");
        var account = Account();
        if (account.Error is not null)
            return Fail(account.Error);
        return Ok(Build(
            ("Server", endpoint.Host),
            ("Port", endpoint.Port),
            ("Database", database),
            ("User ID", account.User),
            ("Password", account.Password),
            ("SslMode", SslMode)));
    }

    private OpResult<string> PostgreSql()
    {
        var endpoint = Endpoint();
        if (endpoint.Error is not null)
            return Fail(endpoint.Error);
        var database = Database.Trim();
        if (database.Length == 0)
            return Fail("請填寫資料庫。");
        if (HasSeparator(database))
            return Fail("資料庫名稱不能包含分號。");
        if (SslMode is not ("Prefer" or "Require" or "Disable" or "Allow" or "VerifyCA" or "VerifyFull"))
            return Fail("請選擇加密連線。");
        var account = Account();
        if (account.Error is not null)
            return Fail(account.Error);
        return Ok(Build(
            ("Host", endpoint.Host),
            ("Port", endpoint.Port),
            ("Database", database),
            ("Username", account.User),
            ("Password", account.Password),
            ("SSL Mode", SslMode)));
    }

    private OpResult<string> Oracle()
    {
        var endpoint = Endpoint();
        if (endpoint.Error is not null)
            return Fail(endpoint.Error);
        var name = OracleName.Trim();
        if (OracleKind is not ("service" or "sid"))
            return Fail("請選擇目標。");
        if (name.Length == 0)
            return OracleKind == "sid" ? Fail("請填寫 SID。") : Fail("請填寫服務名稱。");
        if (!PlainName(name))
            return OracleKind == "sid"
                ? Fail("SID 只填名稱，主機與連接埠用上面的欄位。")
                : Fail("服務名稱只填名稱，主機與連接埠用上面的欄位。");
        var account = Account();
        if (account.Error is not null)
            return Fail(account.Error);

        var dataSource = OracleKind == "sid"
            ? $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={endpoint.Host})(PORT={endpoint.Port}))(CONNECT_DATA=(SID={name})))"
            : $"{endpoint.Host}:{endpoint.Port}/{name}";
        return Ok(Build(
            ("User Id", account.User),
            ("Password", account.Password),
            ("Data Source", dataSource)));
    }

    private EndpointResult Endpoint()
    {
        var host = Host.Trim();
        if (host.Length == 0)
            return EndpointResult.Bad("請填寫主機。");
        if (!HostName(host))
            return EndpointResult.Bad("主機只填位址，連接埠請填在旁邊。");
        var port = Port.Trim();
        if (port.Length == 0)
            return EndpointResult.Bad("請填寫連接埠。");
        if (!int.TryParse(port, out var number) || number is < 1 or > 65535)
            return EndpointResult.Bad("連接埠必須是 1 到 65535。");
        return EndpointResult.Ready(host, port);
    }

    private AccountResult Account()
    {
        var user = User.Trim();
        if (user.Length == 0)
            return AccountResult.Bad("請填寫帳號。");
        if (HasSeparator(user))
            return AccountResult.Bad("帳號不能包含分號。");
        if (string.IsNullOrEmpty(Password))
            return AccountResult.Bad("請填寫密碼。");
        if (Broken(Password))
            return AccountResult.Bad("密碼不能換行。");
        return AccountResult.Ready(user, Password);
    }

    private static string Build(params (string Key, string Value)[] pairs)
    {
        var builder = new DbConnectionStringBuilder();
        foreach (var (key, value) in pairs)
            builder[key] = value;
        return builder.ConnectionString;
    }

    private static bool HostName(string value)
    {
        if (value.Length > 253)
            return false;
        foreach (var character in value)
        {
            if (character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '-' or ':' or '[' or ']')
                continue;
            return false;
        }

        return true;
    }

    private static bool PlainName(string value)
    {
        if (value.Length > 128)
            return false;
        foreach (var character in value)
        {
            if (character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '.' or '-' or '$' or '#')
                continue;
            return false;
        }

        return true;
    }

    private static bool HasSeparator(string value) => value.Contains(';') || Broken(value);

    private static bool Broken(string value) => value.IndexOfAny(['\r', '\n', '\0']) >= 0;

    private static OpResult<string> Ok(string connectionString) => OpResult<string>.Success(connectionString);

    private static OpResult<string> Fail(string message) =>
        OpResult<string>.Fail(WorkbenchMessages.Text("invalid_arguments", message));

    private readonly struct EndpointResult
    {
        public string Host { get; private init; }
        public string Port { get; private init; }
        public string? Error { get; private init; }
        public static EndpointResult Ready(string host, string port) => new() { Host = host, Port = port };
        public static EndpointResult Bad(string error) => new() { Error = error };
    }

    private readonly struct AccountResult
    {
        public string User { get; private init; }
        public string Password { get; private init; }
        public string? Error { get; private init; }
        public static AccountResult Ready(string user, string password) => new() { User = user, Password = password };
        public static AccountResult Bad(string error) => new() { Error = error };
    }
}
