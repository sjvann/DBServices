using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench;

namespace Workbench.Tests;

[TestClass]
public class McpProcessHostTests
{
    [TestMethod]
    public void Project_is_found_beside_the_workbench_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-mcp-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "McpService", "McpService.csproj");
        var workbench = Path.Combine(root, "Workbench");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        Directory.CreateDirectory(workbench);
        File.WriteAllText(project, "<Project />");
        try
        {
            Assert.AreEqual(project, McpProcessHost.FindProject(workbench));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Running_service_is_left_alone()
    {
        var host = new McpProcessHost("http://127.0.0.1:5291", Path.GetTempPath(), _ => Task.FromResult(true));

        var started = await host.StartAsync(CancellationToken.None);

        Assert.IsTrue(started.Ok);
        Assert.AreEqual("資料服務已在運行。", started.Message);
    }

    [TestMethod]
    public async Task Missing_program_is_reported_without_starting()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-no-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var host = new McpProcessHost("http://127.0.0.1:9", root, _ => Task.FromResult(false));
        try
        {
            var started = await host.StartAsync(CancellationToken.None);

            Assert.IsFalse(started.Ok);
            Assert.AreEqual("找不到資料服務程式，無法啟動。", started.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
