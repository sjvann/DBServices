namespace Workbench.Contracts;

public static class EntryNames
{
    public const string Analyst = "analyst";
    public const string Designer = "designer";
    public const string Terminal = "terminal";

    public static readonly string[] All = [Analyst, Designer, Terminal];

    public static string Label(string entry) => entry switch
    {
        Analyst => "系統分析師",
        Designer => "設計師",
        Terminal => "終端使用者",
        _ => entry,
    };

    public static string Home(string entry) => entry switch
    {
        Analyst => "/views",
        Designer => "/styles",
        Terminal => "/talk",
        _ => "/stations",
    };
}

public static class BlockKinds
{
    public const string Title = "title";
    public const string Text = "text";
    public const string Parameter = "parameter";
    public const string Table = "table";

    public static bool Known(string? type) => type is Title or Text or Parameter or Table;
}
