using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class MapParseTests
{
    [TestMethod]
    public void No_object_or_candidates_is_not_parsed()
    {
        Assert.IsFalse(MapParse.Map(null).Parsed);
        Assert.IsFalse(MapParse.Map("").Parsed);
        Assert.IsFalse(MapParse.Map("ready").Parsed);
        Assert.IsFalse(MapParse.Map("[]").Parsed);
        Assert.IsFalse(MapParse.Map("{\"status\":\"ready\"}").Parsed);
        Assert.IsFalse(MapParse.Map("{\"candidates\":\"inventory\"}").Parsed);
    }

    [TestMethod]
    public void Candidates_inside_an_object_are_mapped()
    {
        var parsed = MapParse.Map("{\"candidates\":[{\"connection\":\"inventory\",\"templateId\":\"t1\",\"parameters\":{\"Name\":\"one\"}}]}");
        Assert.IsTrue(parsed.Parsed);
        Assert.AreEqual(1, parsed.Candidates.Count);
        Assert.AreEqual("inventory", parsed.Candidates[0].Connection);
        Assert.AreEqual("t1", parsed.Candidates[0].TemplateId);
        Assert.AreEqual("one", parsed.Candidates[0].Parameters["Name"]);
    }
}
