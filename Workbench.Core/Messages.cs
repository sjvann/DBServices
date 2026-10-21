using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public static class WorkbenchMessages
{
    public static ErrorDto Unavailable() => new() { Code = "workbench_unavailable", Message = "工作台不提供功能。" };
    public static ErrorDto Unauthenticated() => new() { Code = "unauthenticated", Message = "尚未登入。" };
    public static ErrorDto Forbidden() => new() { Code = "forbidden", Message = "沒有權限。" };
    public static ErrorDto BlankName() => new() { Code = "blank_name", Message = "名稱不可空白。" };
    public static ErrorDto DuplicateName() => new() { Code = "duplicate_name", Message = "名稱已存在。" };
    public static ErrorDto UnknownColumn() => new() { Code = "forbidden", Message = "建議含有未知欄名，不能接受。" };
    public static ErrorDto UndecidedConnection() => new() { Code = "", Message = UndecidedConnectionText };
    public static ErrorDto Text(string code, string message) => new() { Code = code, Message = message };

    public const string UndecidedConnectionText = "無法決定連線，沒有讀取資料。";
    public const string ReportUnavailable = "這份報表現在產生不了。";
    public const string UnreadableStyle = "不能讀取這個樣式。";
    public const string FrozenStructure = "這個檢視已被樣式引用，不能變更結構。";
    public const string FrozenDelete = "這個檢視已被樣式引用，不能刪除。";
    public const string FrozenUnpublish = "這個檢視已被樣式引用，不能取消發布。";
    public const string Disconnected = "來源連不起來。";
    public const string SecondAdmin = "不能建立另一個管理者。";
    public const string LeftUnsaved = "未保存就離開後不能匯出。";

    public static string NoReport(IEnumerable<string> names)
    {
        var list = names.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).ToList();
        if (list.Count == 0)
            return "還沒有可以產生的報表。";
        return "沒有對上的報表。可以用的有：" + string.Join("、", list) + "。";
    }

    public static string SeveralReports(IEnumerable<string> names) =>
        "對上不只一份：" + string.Join("、", names) + "。請只留一份。";

    public static string MissingConditions(IEnumerable<string> names) =>
        "缺少條件。還缺" + string.Join("、", names) + "。";

    public static ErrorDto EnvironmentIncomplete(EnvironmentSnapshot snapshot)
    {
        var parts = new List<string>();
        if (!snapshot.DatabaseReady)
            parts.Add("資料庫連線尚未設定");
        if (!snapshot.ModelReady)
            parts.Add("AI 尚未設定");
        if (parts.Count == 0)
            parts.Add("環境設定尚未完成");
        return new ErrorDto { Code = "environment_incomplete", Message = "環境設定未完成：" + string.Join("，", parts) + "。" };
    }
}

public sealed class OpResult<T>
{
    public T? Value { get; init; }
    public ErrorDto? Error { get; init; }
    public bool Ok => Error is null;

    public static OpResult<T> Success(T value) => new() { Value = value };
    public static OpResult<T> Fail(ErrorDto error) => new() { Error = error };
}
