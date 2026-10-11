using System.CommandLine;
using DbServices.Cli;
using DbServices.Core.Models.Interface;
using DbServices.Core.Session;
using DBServices;

var providerOption = new Option<string>("--provider", "-p")
{
    Description = "sqlite|sqlserver|mysql|oracle|postgresql",
    DefaultValueFactory = _ => "sqlite"
};
var connectionOption = new Option<string>("--connection", "-c")
{
    Description = "連線字串",
    Required = true
};
var queryOption = new Option<string?>("--query", "-q")
{
    Description = "單次 SQL"
};
var fnOption = new Option<string?>("--fn")
{
    Description = "單次函數名"
};
var argOption = new Option<string[]>("--arg")
{
    Description = "函數參數 key=value",
    DefaultValueFactory = _ => [],
    AllowMultipleArgumentsPerToken = true
};
var formatOption = new Option<string>("--format")
{
    Description = "table|json|csv",
    DefaultValueFactory = _ => "table"
};
var echoOption = new Option<bool>("--echo-hidden", "-E")
{
    Description = "顯示展開後的請求"
};

var root = new RootCommand("dbsql — DBServices SQL／函數 CLI")
{
    providerOption, connectionOption, queryOption, fnOption, argOption, formatOption, echoOption
};

root.SetAction(parse =>
{
    var provider = parse.GetValue(providerOption) ?? "sqlite";
    var connection = parse.GetValue(connectionOption) ?? "";
    var query = parse.GetValue(queryOption);
    var fn = parse.GetValue(fnOption);
    var fnArgs = parse.GetValue(argOption) ?? [];
    var format = ParseFormat(parse.GetValue(formatOption));
    var echoHidden = parse.GetValue(echoOption);

    var db = CreateDb(provider, connection);
    var session = db.GetSession();

    if (!string.IsNullOrWhiteSpace(query) && !string.IsNullOrWhiteSpace(fn))
    {
        Console.Error.WriteLine("單次模式不可同時使用 -q 與 --fn。");
        return 2;
    }

    if (!string.IsNullOrWhiteSpace(fn))
    {
        var arguments = ParseArgs(fnArgs);
        var request = new QueryRequest { Text = fn, Opcode = QueryOpcode.Call, Arguments = arguments, Format = format };
        if (echoHidden) Console.Error.WriteLine($"ECHO: CALL {fn}");
        Console.WriteLine(ResultFormatter.Format(session.Execute(request), format, false));
        return 0;
    }

    if (!string.IsNullOrWhiteSpace(query))
    {
        var expanded = MetaCommand.Parse(query);
        if (echoHidden && expanded.Echo != null) Console.Error.WriteLine($"ECHO: {expanded.Echo}");
        Console.WriteLine(ResultFormatter.Format(session.Execute(expanded.Request), format, false));
        return 0;
    }

    return RunRepl(session, format, echoHidden);
});

return root.Parse(args).Invoke();

static int RunRepl(IQuerySession session, ResultFormat format, bool echoHidden)
{
    var expandedView = false;
    Console.WriteLine("dbsql  輸入 SQL 或以 \\h 查看指令。");
    var buffer = "";
    while (true)
    {
        Console.Write(buffer.Length == 0 ? "dbsql> " : "    -> ");
        var line = Console.ReadLine();
        if (line == null) break;
        if (MetaCommand.IsMeta(line) && buffer.Length == 0)
        {
            var meta = MetaCommand.Parse(line);
            if (meta.Quit) return 0;
            if (meta.ToggleExpanded)
            {
                expandedView = !expandedView;
                Console.WriteLine(expandedView ? "展開列：開" : "展開列：關");
                continue;
            }
            if (meta.Help)
            {
                Console.WriteLine(MetaCommand.HelpText);
                continue;
            }
            if (echoHidden && meta.Echo != null) Console.Error.WriteLine($"ECHO: {meta.Echo}");
            Console.WriteLine(ResultFormatter.Format(session.Execute(meta.Request), format, expandedView));
            continue;
        }

        buffer += line + "\n";
        if (!buffer.Contains(';')) continue;
        var sql = buffer.Trim().TrimEnd(';');
        buffer = "";
        if (string.IsNullOrWhiteSpace(sql)) continue;
        var request = new QueryRequest { Text = sql, Opcode = QueryOpcode.Auto, Format = format };
        if (echoHidden) Console.Error.WriteLine($"ECHO: {sql}");
        Console.WriteLine(ResultFormatter.Format(session.Execute(request), format, expandedView));
    }
    return 0;
}

static IDbService CreateDb(string provider, string connectionString)
{
    return provider.Trim().ToLowerInvariant() switch
    {
        "sqlite" => MainService.UseSQLite(connectionString),
        "sqlserver" or "mssql" => MainService.UseMsSQL(connectionString),
        "mysql" => MainService.UseMySQL(connectionString),
        "oracle" => MainService.UseOracle(connectionString),
        "postgresql" or "postgres" => MainService.UsePostgreSQL(connectionString),
        _ => throw new ArgumentException($"不支援的 provider: {provider}")
    };
}

static ResultFormat ParseFormat(string? value) => value?.ToLowerInvariant() switch
{
    "json" => ResultFormat.Json,
    "csv" => ResultFormat.Csv,
    _ => ResultFormat.Table
};

static Dictionary<string, object?> ParseArgs(string[] args)
{
    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    foreach (var arg in args)
    {
        var kv = arg.Split('=', 2);
        if (kv.Length == 2)
            dict[kv[0]] = kv[1];
    }
    return dict;
}
