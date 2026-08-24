using System.Diagnostics;
using DBServices;

namespace DBServiceTest;

[TestClass]
public class CliProcessTests
{
    [TestMethod]
    public void Dbsql_query_prints_selected_rows()
    {
        var exe = FindDbsql();
        if (exe is null)
            Assert.Inconclusive("找不到 dbsql 執行檔。");

        var dbPath = Path.Combine(Path.GetTempPath(), $"dbsql-it-{Guid.NewGuid():N}.db");
        try
        {
            var db = MainService.UseSQLite($"Data Source={dbPath}");
            db.ExecuteSql("CREATE TABLE Users (Id INTEGER PRIMARY KEY, Name TEXT)");
            db.ExecuteSql("INSERT INTO Users (Id, Name) VALUES (1, 'Ada')");

            var psi = new ProcessStartInfo(exe)
            {
                Arguments = $"-p sqlite -c \"Data Source={dbPath}\" -q \"SELECT Name FROM Users WHERE Id = 1\" --format csv",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            Assert.IsNotNull(process);
            var stdout = process!.StandardOutput.ReadToEnd();
            process.WaitForExit(15_000);
            Assert.AreEqual(0, process.ExitCode, process.StandardError.ReadToEnd());
            StringAssert.Contains(stdout, "Ada");
        }
        finally
        {
            try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { /* ignore */ }
        }
    }

    private static string? FindDbsql()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "dbsql.exe", "dbsql" } : ["dbsql"];
        foreach (var name in names)
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(candidate))
                return candidate;
        }

        var sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DbServices.Cli", "bin"));
        if (!Directory.Exists(sibling))
            return null;
        return Directory.EnumerateFiles(sibling, "dbsql*", SearchOption.AllDirectories).FirstOrDefault();
    }
}
