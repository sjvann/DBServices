using Workbench.Contracts;

namespace Workbench.Core;

public static class WorkbenchMessages
{
    public static ErrorDto Unavailable() => new() { Code = "workbench_unavailable", Message = "工作台不提供功能。" };
    public static ErrorDto Unauthenticated() => new() { Code = "unauthenticated", Message = "尚未登入。" };
    public static ErrorDto Forbidden() => new() { Code = "forbidden", Message = "沒有權限。" };
    public static ErrorDto SingleKey() => new() { Code = "single_key_only", Message = "此表面只接受單鍵" };
    public static ErrorDto SuggestionRejected() => new() { Code = "suggestion_rejected", Message = "建議含有未知欄名，不能接受。" };
    public static ErrorDto IntentNotReady() => new() { Code = "intent_not_ready", Message = "語句尚未就緒。" };
    public static ErrorDto PlanMismatch() => new() { Code = "plan_mismatch", Message = "確認清單與目前的範本或角色不一致。" };
    public static ErrorDto PlanUsed() => new() { Code = "plan_mismatch", Message = "這份清單已經確認過。" };
    public static ErrorDto SnapshotGone() => new() { Code = "snapshot_gone", Message = "這次快照已經不在。" };
    public static ErrorDto AlignmentFailed(string difference) => new() { Code = "alignment_failed", Message = difference };
    public static ErrorDto BlankName() => new() { Code = "blank_name", Message = "名稱不可空白。" };
    public static ErrorDto DuplicateName() => new() { Code = "duplicate_name", Message = "名稱已存在。" };
    public static ErrorDto GroupNotEmpty() => new() { Code = "group_not_empty", Message = "群組內仍有範本。" };
    public static ErrorDto WebCount() => new() { Code = "forbidden", Message = "此表面沒有計數動作。" };
    public static ErrorDto MissingColumn() => new() { Code = "plan_mismatch", Message = "範本欄位已不在資料表上。" };
    public static ErrorDto BlankPhrase() => new() { Code = "blank_name", Message = "說法不可空白。" };
    public static ErrorDto Model(string code, string message) => new() { Code = code, Message = message };

    public const string BeforeConfirm = "確認之前，這裡沒有業務列。";
    public const string NoTables = "沒有資料表。";
    public const string NoRows = "沒有符合的列。";
    public const string NoWorkRole = "尚未指派工作角色。";
    public const string ChooseTable = "請先選擇資料表。";
    public const string NotReady = "尚未就緒，不能確認。";
    public const string Ambiguous = "有多個候選，請改到只剩一個連線與一個範本。";
    public const string Incomplete = "還缺連線、範本或參數值。";
}

public sealed class OpResult<T>
{
    public T? Value { get; init; }
    public ErrorDto? Error { get; init; }
    public bool Ok => Error is null;

    public static OpResult<T> Success(T value) => new() { Value = value };
    public static OpResult<T> Fail(ErrorDto error) => new() { Error = error };
}

public sealed class Profile
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public bool IsAdmin { get; init; }
    public IReadOnlySet<string> WorkRoles { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlyList<Ports.StoredGroupRole> Groups { get; init; } = [];

    public bool HasWork(string role) => IsAdmin || WorkRoles.Contains(role);
    public bool CanViewGroup(string groupId) =>
        IsAdmin || Groups.Any(item => item.GroupId == groupId && item.Role is GroupRoles.Design or GroupRoles.View);
    public bool CanDesignGroup(string groupId) =>
        Groups.Any(item => item.GroupId == groupId && item.Role == GroupRoles.Design);
    public bool IsTerminal => IsAdmin || WorkRoles.Contains(Workbench.Contracts.WorkRoles.Terminal);
}
