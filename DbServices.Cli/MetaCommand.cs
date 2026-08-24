using DbServices.Core.Session;

namespace DbServices.Cli
{
    public sealed class ExpandedRequest
    {
        public required QueryRequest Request { get; init; }
        public bool Quit { get; init; }
        public bool ToggleExpanded { get; init; }
        public bool Help { get; init; }
        public string? Echo { get; init; }
    }

    public static class MetaCommand
    {
        public static bool IsMeta(string text)
            => text.TrimStart().StartsWith('\\');

        public static ExpandedRequest Parse(string text)
        {
            var line = text.Trim();
            if (!line.StartsWith('\\'))
            {
                return new ExpandedRequest
                {
                    Request = new QueryRequest { Text = line.TrimEnd(';'), Opcode = QueryOpcode.Auto }
                };
            }

            var parts = line[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts.Length == 0 ? "" : parts[0];
            return cmd switch
            {
                "q" or "quit" => new ExpandedRequest { Request = new QueryRequest(), Quit = true },
                "x" => new ExpandedRequest { Request = new QueryRequest(), ToggleExpanded = true },
                "h" or "help" => new ExpandedRequest { Request = new QueryRequest(), Help = true },
                "dt" => Call("tables", new Dictionary<string, object?>(), "CALL tables"),
                "d" when parts.Length >= 2 => Call("fields", new Dictionary<string, object?> { ["table"] = parts[1] }, $"CALL fields table={parts[1]}"),
                "d" => Call("tables", new Dictionary<string, object?>(), "CALL tables"),
                "call" or "fn" => ParseCall(parts.Skip(1).ToArray()),
                _ => new ExpandedRequest
                {
                    Request = new QueryRequest { Text = cmd, Opcode = QueryOpcode.Auto },
                    Echo = cmd
                }
            };
        }

        private static ExpandedRequest ParseCall(string[] tokens)
        {
            if (tokens.Length == 0)
                return new ExpandedRequest { Request = new QueryRequest { Text = "tables", Opcode = QueryOpcode.Call } };

            var name = tokens[0];
            var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (token.StartsWith("--") && token.Contains('='))
                {
                    var kv = token[2..].Split('=', 2);
                    args[kv[0]] = kv[1];
                }
                else if (token.StartsWith("--") && i + 1 < tokens.Length)
                {
                    args[token[2..]] = tokens[++i];
                }
                else if (!args.ContainsKey("table"))
                    args["table"] = token;
                else if (!args.ContainsKey("id"))
                    args["id"] = token;
            }

            var echo = $"CALL {name} " + string.Join(" ", args.Select(kv => $"{kv.Key}={kv.Value}"));
            return Call(name, args, echo);
        }

        private static ExpandedRequest Call(string name, Dictionary<string, object?> args, string echo)
            => new()
            {
                Request = new QueryRequest { Text = name, Opcode = QueryOpcode.Call, Arguments = args },
                Echo = echo
            };

        public static string HelpText =>
            """
            \dt                  列出資料表
            \d [table]           描述資料表欄位
            \call name args      呼叫已註冊函數
            \fn name --k v       同上
            \x                   切換展開列
            \h                   說明
            \q                   離開
            SQL 以 ; 結束後送出。
            """;
    }
}
