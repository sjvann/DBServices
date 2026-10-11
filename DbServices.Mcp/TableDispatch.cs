using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using DbServices.Api.Shared;
using DbServices.Core.Session;

namespace DbServices.Mcp;

/// <summary>
/// 依已登記連線建立服務，並執行 Session 的 tables、fields、get、query、count 或 exists。不參考 MCP。
/// </summary>
public static class TableDispatch
{
    public static TableDispatchResult Execute(ToolCatalog catalog, string connectionId, object? includeView)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var snapshot = catalog.Connections[connectionId];
        if (RefuseUnknownServer(snapshot) is { } refused)
            return refused;

        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        try
        {
            var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (includeView is not null)
                arguments["includeView"] = includeView;

            var result = service!.GetSession().Execute(new QueryRequest
            {
                Text = "tables",
                Opcode = QueryOpcode.Call,
                Arguments = arguments,
            });

            if (result.Kind == QueryKind.Error)
                return TableDispatchResult.ExecutionFailed();

            return TableDispatchResult.Rows(result.Columns, result.Rows);
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 同一服務先探表（includeView 為 true）。命中才把 table 送進 fields。
    /// 缺表名不建立服務。
    /// </summary>
    public static TableDispatchResult ExecuteFields(ToolCatalog catalog, string connectionId, string? table)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(table))
            return TableDispatchResult.InvalidArguments();

        var snapshot = catalog.Connections[connectionId];
        if (RefuseUnknownServer(snapshot) is { } refused)
            return refused;

        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        try
        {
            var session = service!.GetSession();
            var probe = session.Execute(new QueryRequest
            {
                Text = "tables",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["includeView"] = true,
                },
            });

            return FieldsAfterTables(probe, table, () => session.Execute(new QueryRequest
            {
                Text = "fields",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["table"] = table,
                },
            }));
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 直接執行 Session get。缺表名、空白表名或非整數 id 不建立服務，也不探表。
    /// </summary>
    public static TableDispatchResult ExecuteGet(ToolCatalog catalog, string connectionId, string? table, JsonElement? id)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(table) || !TryReadInt64(id, out var parsedId))
            return TableDispatchResult.InvalidGetArguments();

        var snapshot = catalog.Connections[connectionId];
        if (RefuseUnknownServer(snapshot) is { } refused)
            return refused;

        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        try
        {
            var result = service!.GetSession().Execute(new QueryRequest
            {
                Text = "get",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["table"] = table,
                    ["id"] = parsedId,
                },
            });

            return FromGet(result);
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 直接執行 Session query。缺表名或空白表名不建立服務，也不探表。
    /// </summary>
    public static TableDispatchResult ExecuteQuery(ToolCatalog catalog, string connectionId, string? table, JsonElement? filters)
        => ExecuteDirect(catalog, connectionId, "query", table, filters, FromQuery);

    /// <summary>
    /// 直接執行 Session count。缺表名或空白表名不建立服務，也不探表。
    /// </summary>
    public static TableDispatchResult ExecuteCount(ToolCatalog catalog, string connectionId, string? table, JsonElement? filters)
        => ExecuteDirect(catalog, connectionId, "count", table, filters, FromCount);

    /// <summary>
    /// 直接執行 Session exists。缺表名或空白表名不建立服務，也不探表。
    /// </summary>
    public static TableDispatchResult ExecuteExists(ToolCatalog catalog, string connectionId, string? table, JsonElement? filters)
        => ExecuteDirect(catalog, connectionId, "exists", table, filters, FromExists);

    /// <summary>
    /// 依送入的 Session query 結果回應。不探表，也不把缺表改成 unknown_table。
    /// </summary>
    public static TableDispatchResult FromQuery(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind == QueryKind.Error)
            return TableDispatchResult.QueryExecutionFailed();

        return TableDispatchResult.Rows(result.Columns, result.Rows);
    }

    /// <summary>
    /// 依送入的 Session count 結果回應。文字是那個儲存格的 JSON 數字；沒有儲存格時是 0。
    /// </summary>
    public static TableDispatchResult FromCount(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind == QueryKind.Error)
            return TableDispatchResult.CountExecutionFailed();

        return TableDispatchResult.Scalar(CellOr(result, 0L));
    }

    /// <summary>
    /// 依送入的 Session exists 結果回應。文字是那個儲存格的 JSON 布林；沒有儲存格時是 false。
    /// </summary>
    public static TableDispatchResult FromExists(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind == QueryKind.Error)
            return TableDispatchResult.ExistsExecutionFailed();

        return TableDispatchResult.Scalar(CellOr(result, false));
    }

    /// <summary>
    /// 名稱不是五個精確字串時，在建立服務前回「無法建立連線。」
    /// </summary>
    private static TableDispatchResult? RefuseUnknownServer(ConnectionSnapshot snapshot)
    {
        if (snapshot.ServerType is "Sqlite" or "SqlServer" or "MySQL" or "Oracle" or "PostgreSQL")
            return null;

        return TableDispatchResult.ConnectionFailed();
    }

    private static TableDispatchResult ExecuteDirect(
        ToolCatalog catalog,
        string connectionId,
        string tool,
        string? table,
        JsonElement? filters,
        Func<QueryResult, TableDispatchResult> map)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(table))
            return TableDispatchResult.InvalidArguments();

        var snapshot = catalog.Connections[connectionId];
        if (RefuseUnknownServer(snapshot) is { } refused)
            return refused;

        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        try
        {
            var result = service!.GetSession().Execute(new QueryRequest
            {
                Text = tool,
                Opcode = QueryOpcode.Call,
                Arguments = ArgumentsFor(table, filters),
            });
            return map(result);
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static Dictionary<string, object?> ArgumentsFor(string table, JsonElement? filters)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["table"] = table,
        };

        if (filters is not { } element || element.ValueKind != JsonValueKind.Object)
            return arguments;

        foreach (var property in element.EnumerateObject())
        {
            if (TryReadScalar(property.Value, out var scalar))
                arguments[property.Name] = scalar;
        }

        return arguments;
    }

    private static bool TryReadScalar(JsonElement value, out object? scalar)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                scalar = null;
                return true;
            case JsonValueKind.True:
                scalar = true;
                return true;
            case JsonValueKind.False:
                scalar = false;
                return true;
            case JsonValueKind.String:
                scalar = value.GetString();
                return true;
            case JsonValueKind.Number:
                scalar = ReadNumber(value);
                return true;
            default:
                scalar = null;
                return false;
        }
    }

    private static object ReadNumber(JsonElement element)
    {
        if (TryReadInt64(element, out var integer))
            return integer;

        var raw = element.GetRawText();
        if (TryExactDecimal(raw, out var exact))
            return exact;
        if (TryPreservingDouble(raw, out var approximate))
            return approximate;
        return Encoding.UTF8.GetBytes(raw);
    }

    private static bool TryExactDecimal(string raw, out decimal value)
    {
        value = 0;
        if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;
        if (!TryNormalizeLiteral(raw, out var negative, out var digits, out var exponent)
            || !SameDecimal(parsed, negative, digits, exponent))
            return false;

        value = parsed;
        return true;
    }

    private static bool TryPreservingDouble(string raw, out double value)
    {
        value = 0;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed))
            return false;

        var integerValued = parsed == Math.Truncate(parsed);
        if (integerValued && !LiteralIsMathematicalInteger(raw))
            return false;

        value = parsed;
        return true;
    }

    private static bool SameDecimal(decimal parsed, bool negative, string digits, long exponent)
    {
        var bits = decimal.GetBits(parsed);
        var parsedNegative = (bits[3] & int.MinValue) != 0;
        var scale = (bits[3] >> 16) & 0xFF;
        var significand = (new BigInteger((uint)bits[2]) << 64)
            + (new BigInteger((uint)bits[1]) << 32)
            + new BigInteger((uint)bits[0]);
        if (significand.IsZero)
            return digits == "0" && exponent == 0 && !negative;

        var parsedDigits = significand.ToString(CultureInfo.InvariantCulture);
        long parsedExponent = -scale;
        parsedDigits = StripTrailingZeros(parsedDigits, ref parsedExponent);
        return parsedNegative == negative && parsedDigits == digits && parsedExponent == exponent;
    }

    private static bool LiteralIsMathematicalInteger(string raw)
        => TryNormalizeLiteral(raw, out _, out _, out var exponent) && exponent >= 0;

    private static bool TryNormalizeLiteral(string raw, out bool negative, out string digits, out long exponent)
    {
        negative = false;
        digits = "";
        exponent = 0;
        if (raw.Length == 0)
            return false;

        var index = 0;
        if (raw[0] == '-')
        {
            negative = true;
            index++;
        }

        var intStart = index;
        while (index < raw.Length && IsDigit(raw[index]))
            index++;
        if (index == intStart)
            return false;
        var intEnd = index;

        var fracStart = index;
        var fracEnd = index;
        if (index < raw.Length && raw[index] == '.')
        {
            index++;
            fracStart = index;
            while (index < raw.Length && IsDigit(raw[index]))
                index++;
            fracEnd = index;
            if (fracStart == fracEnd)
                return false;
        }

        long exp = 0;
        if (index < raw.Length && raw[index] is 'e' or 'E')
        {
            index++;
            var expNegative = false;
            if (index < raw.Length && raw[index] is '+' or '-')
            {
                expNegative = raw[index] == '-';
                index++;
            }

            var expStart = index;
            while (index < raw.Length && IsDigit(raw[index]))
                index++;
            if (index == expStart || index != raw.Length || !TryParseInt64(raw, expStart, index, out exp))
                return false;
            if (expNegative)
                exp = -exp;
        }
        else if (index != raw.Length)
        {
            return false;
        }

        var all = string.Concat(raw.AsSpan(intStart, intEnd - intStart), raw.AsSpan(fracStart, fracEnd - fracStart));
        var start = 0;
        while (start < all.Length - 1 && all[start] == '0')
            start++;

        var onlyZero = true;
        for (var cursor = start; cursor < all.Length; cursor++)
        {
            if (all[cursor] != '0')
            {
                onlyZero = false;
                break;
            }
        }

        if (onlyZero)
        {
            negative = false;
            digits = "0";
            exponent = 0;
            return true;
        }

        exponent = exp - (fracEnd - fracStart);
        digits = StripTrailingZeros(all[start..], ref exponent);
        return true;
    }

    private static bool TryParseInt64(string raw, int start, int end, out long value)
    {
        value = 0;
        for (var index = start; index < end; index++)
        {
            var digit = raw[index] - '0';
            if (value > (long.MaxValue - digit) / 10)
                return false;
            value = value * 10 + digit;
        }

        return true;
    }

    private static string StripTrailingZeros(string digits, ref long exponent)
    {
        var end = digits.Length;
        while (end > 1 && digits[end - 1] == '0')
        {
            end--;
            exponent++;
        }

        return end == digits.Length ? digits : digits[..end];
    }

    private static object? CellOr(QueryResult result, object? fallback)
    {
        if (result.Rows.Count == 0 || result.Rows[0].Count == 0)
            return fallback;
        return result.Rows[0][0];
    }

    /// <summary>
    /// 依送入的 Session get 結果回應。不探表，也不把缺表改成 unknown_table。
    /// </summary>
    public static TableDispatchResult FromGet(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind == QueryKind.Error)
            return TableDispatchResult.GetExecutionFailed();

        return TableDispatchResult.Rows(result.Columns, result.Rows);
    }

    private static bool TryReadInt64(JsonElement? id, out long value)
    {
        value = 0;
        if (id is not { } element || element.ValueKind != JsonValueKind.Number)
            return false;

        if (element.TryGetInt64(out value))
            return true;

        return TryExactInt64(element.GetRawText(), out value);
    }

    /// <summary>
    /// 用字面位數判斷，不經過會把 1e-29 收成 0、把超長小數收成 1 的 decimal。
    /// </summary>
    private static bool TryExactInt64(string raw, out long value)
    {
        value = 0;
        if (raw.Length == 0)
            return false;

        var index = 0;
        var negative = raw[0] == '-';
        if (negative)
            index++;

        var intStart = index;
        while (index < raw.Length && IsDigit(raw[index]))
            index++;
        if (index == intStart)
            return false;
        var intEnd = index;

        var fracStart = index;
        var fracEnd = index;
        if (index < raw.Length && raw[index] == '.')
        {
            index++;
            fracStart = index;
            while (index < raw.Length && IsDigit(raw[index]))
                index++;
            fracEnd = index;
            if (fracStart == fracEnd)
                return false;
        }

        var exponent = 0;
        if (index < raw.Length && raw[index] is 'e' or 'E')
        {
            index++;
            var expNegative = false;
            if (index < raw.Length && raw[index] is '+' or '-')
            {
                expNegative = raw[index] == '-';
                index++;
            }

            var expStart = index;
            while (index < raw.Length && IsDigit(raw[index]))
                index++;
            if (index == expStart || index != raw.Length)
                return false;
            if (!TrySmallExponent(raw, expStart, index, out exponent))
                return ZeroIfSignificandIsZero(raw, intStart, intEnd, fracStart, fracEnd, out value);
            if (expNegative)
                exponent = -exponent;
        }
        else if (index != raw.Length)
        {
            return false;
        }

        var fractionLength = fracEnd - fracStart;
        var scale = (long)exponent - fractionLength;
        var total = (intEnd - intStart) + fractionLength;
        var kept = total;
        if (scale < 0)
        {
            var drop = -scale;
            if (drop > total)
                return ZeroIfSignificandIsZero(raw, intStart, intEnd, fracStart, fracEnd, out value);
            if (!TailIsZero(raw, intStart, intEnd, fracStart, total, (int)drop))
                return false;
            kept = total - (int)drop;
            scale = 0;
        }

        ulong magnitude = 0;
        var significant = 0;
        for (var logical = 0; logical < kept; logical++)
        {
            var digit = DigitAt(raw, intStart, intEnd, fracStart, logical);
            if (significant == 0 && digit == '0')
                continue;

            significant++;
            if (significant + scale > 19)
                return false;
            magnitude = magnitude * 10UL + (ulong)(digit - '0');
        }

        if (magnitude == 0)
        {
            value = 0;
            return true;
        }

        if (scale > 19)
            return false;

        for (var power = 0; power < scale; power++)
            magnitude *= 10UL;

        if (negative)
        {
            if (magnitude > (ulong)long.MaxValue + 1UL)
                return false;
            value = magnitude == (ulong)long.MaxValue + 1UL ? long.MinValue : -(long)magnitude;
            return true;
        }

        if (magnitude > (ulong)long.MaxValue)
            return false;
        value = (long)magnitude;
        return true;
    }

    private static bool TrySmallExponent(string raw, int start, int end, out int exponent)
    {
        exponent = 0;
        while (start < end && raw[start] == '0')
            start++;
        if (end - start > 9)
            return false;

        for (var index = start; index < end; index++)
            exponent = exponent * 10 + (raw[index] - '0');
        return true;
    }

    private static bool ZeroIfSignificandIsZero(
        string raw, int intStart, int intEnd, int fracStart, int fracEnd, out long value)
    {
        value = 0;
        for (var index = intStart; index < intEnd; index++)
        {
            if (raw[index] != '0')
                return false;
        }

        for (var index = fracStart; index < fracEnd; index++)
        {
            if (raw[index] != '0')
                return false;
        }

        return true;
    }

    private static bool TailIsZero(string raw, int intStart, int intEnd, int fracStart, int total, int drop)
    {
        for (var logical = total - drop; logical < total; logical++)
        {
            if (DigitAt(raw, intStart, intEnd, fracStart, logical) != '0')
                return false;
        }

        return true;
    }

    private static char DigitAt(string raw, int intStart, int intEnd, int fracStart, int logical)
    {
        var intLength = intEnd - intStart;
        return logical < intLength ? raw[intStart + logical] : raw[fracStart + (logical - intLength)];
    }

    private static bool IsDigit(char value) => value is >= '0' and <= '9';

    /// <summary>
    /// 依送入的探表結果決定是否執行 fields。探表失敗或未命中都不呼叫 fields。
    /// </summary>
    public static TableDispatchResult FieldsAfterTables(QueryResult tables, string table, Func<QueryResult> fields)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(fields);

        if (tables.Kind == QueryKind.Error)
            return TableDispatchResult.FieldsExecutionFailed();

        if (!ContainsTable(tables, table))
            return TableDispatchResult.UnknownTable();

        var result = fields();
        if (result.Kind == QueryKind.Error)
            return TableDispatchResult.FieldsExecutionFailed();

        return TableDispatchResult.Rows(result.Columns, result.Rows);
    }

    private static bool ContainsTable(QueryResult tables, string table)
    {
        var nameIndex = -1;
        for (var index = 0; index < tables.Columns.Count; index++)
        {
            if (string.Equals(tables.Columns[index], "name", StringComparison.OrdinalIgnoreCase))
            {
                nameIndex = index;
                break;
            }
        }

        if (nameIndex < 0)
            return false;

        foreach (var row in tables.Rows)
        {
            if (nameIndex >= row.Count || row[nameIndex] is null)
                continue;

            if (string.Equals(Convert.ToString(row[nameIndex]), table, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// 呼叫結果。查列成功是 columns 與 rows；計數與存在是單一 JSON 值；失敗是 code 與 message。
/// </summary>
public sealed class TableDispatchResult
{
    private TableDispatchResult(bool isError, string json)
    {
        IsError = isError;
        Json = json;
    }

    public bool IsError { get; }

    public string Json { get; }

    public static TableDispatchResult Rows(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows)
        => new(false, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["columns"] = columns,
            ["rows"] = rows.Select(static row => row.ToArray()).ToArray(),
        }));

    public static TableDispatchResult ExecutionFailed()
        => Error("execution_failed", "列出資料表失敗。");

    public static TableDispatchResult ConnectionFailed()
        => Error("connection_failed", "無法建立連線。");

    public static TableDispatchResult FieldsExecutionFailed()
        => Error("execution_failed", "列出欄位失敗。");

    public static TableDispatchResult UnknownTable()
        => Error("unknown_table", "找不到資料表。");

    public static TableDispatchResult InvalidArguments()
        => Error("invalid_arguments", "未提供資料表名稱。");

    public static TableDispatchResult InvalidGetArguments()
        => Error("invalid_arguments", "未提供資料表名稱或整數 Id。");

    public static TableDispatchResult GetExecutionFailed()
        => Error("execution_failed", "依 Id 取一列失敗。");

    public static TableDispatchResult QueryExecutionFailed()
        => Error("execution_failed", "鍵值查詢失敗。");

    public static TableDispatchResult CountExecutionFailed()
        => Error("execution_failed", "計數失敗。");

    public static TableDispatchResult ExistsExecutionFailed()
        => Error("execution_failed", "是否存在失敗。");

    public static TableDispatchResult Scalar(object? value)
        => new(false, JsonSerializer.Serialize(value));

    private static TableDispatchResult Error(string code, string message)
        => new(true, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["code"] = code,
            ["message"] = message,
        }));
}
