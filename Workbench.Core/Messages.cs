using Workbench.Contracts;

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
    public const string NoMatch = "語句對不到樣式。";
    public const string ManyMatches = "語句對到多筆樣式。";
    public const string MissingParameters = "缺少參數：";
    public const string UnreadableStyle = "不能讀取這個樣式。";
    public const string FrozenStructure = "這個檢視已被樣式引用，不能變更結構。";
    public const string FrozenDelete = "這個檢視已被樣式引用，不能刪除。";
    public const string FrozenUnpublish = "這個檢視已被樣式引用，不能取消發布。";
    public const string Disconnected = "來源連不起來。";
    public const string SecondAdmin = "不能建立另一個管理者。";
    public const string LeftUnsaved = "未保存就離開後不能匯出。";
}

public sealed class OpResult<T>
{
    public T? Value { get; init; }
    public ErrorDto? Error { get; init; }
    public bool Ok => Error is null;

    public static OpResult<T> Success(T value) => new() { Value = value };
    public static OpResult<T> Fail(ErrorDto error) => new() { Error = error };
}
