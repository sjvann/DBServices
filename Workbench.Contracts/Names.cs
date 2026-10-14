namespace Workbench.Contracts;

public static class WorkRoles
{
    public const string Planner = "planner";
    public const string Editor = "editor";
    public const string Terminal = "terminal";
}

public static class GroupRoles
{
    public const string Design = "design";
    public const string View = "view";
}

public static class SurfaceNames
{
    public const string Mcp = "mcp";
    public const string Web = "web";
}

public static class ComponentKinds
{
    public const string Title = "title";
    public const string Text = "text";
    public const string Parameter = "parameter";
    public const string Table = "table";
    public const string Count = "count";
}

public static class IntentStatuses
{
    public const string Ready = "ready";
    public const string Ambiguous = "ambiguous";
    public const string Incomplete = "incomplete";
}

public static class KeywordSources
{
    public const string Human = "human";
    public const string Ai = "ai";
}

public static class ModelCodes
{
    public const string Ok = "model_ok";
    public const string InvalidSettings = "model_invalid_settings";
    public const string Unreachable = "model_unreachable";
    public const string Timeout = "model_timeout";
}
