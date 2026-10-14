using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Workbench.Tests;

[TestClass]
public class ShapeTests
{
    [TestMethod]
    public void Sources_do_not_use_decompiled_shapes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) == false
                && path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) == false)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Workbench.Tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Workbench", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}DbServices.ModelSidecar{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.IsFalse(text.Contains("goto IL_", StringComparison.Ordinal), file);
            Assert.IsFalse(text.Contains("if (1 == 0)", StringComparison.Ordinal), file);
            Assert.IsFalse(text.Contains("_003C_003Ez__ReadOnlySingleElementList", StringComparison.Ordinal), file);
        }
    }
}
