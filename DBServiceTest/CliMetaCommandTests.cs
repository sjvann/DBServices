using DbServices.Cli;
using DbServices.Core.Session;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServiceTest
{
    [TestClass]
    public class CliMetaCommandTests
    {
        [TestMethod]
        public void Dt_ExpandsToTablesCall()
        {
            var expanded = MetaCommand.Parse("\\dt");
            Assert.AreEqual(QueryOpcode.Call, expanded.Request.Opcode);
            Assert.AreEqual("tables", expanded.Request.Text);
        }

        [TestMethod]
        public void Describe_ExpandsToFieldsCall()
        {
            var expanded = MetaCommand.Parse("\\d Users");
            Assert.AreEqual("fields", expanded.Request.Text);
            Assert.AreEqual("Users", expanded.Request.Arguments["table"]);
        }

        [TestMethod]
        public void Call_ParsesPositionalTableAndId()
        {
            var expanded = MetaCommand.Parse("\\call get Users 1");
            Assert.AreEqual("get", expanded.Request.Text);
            Assert.AreEqual("Users", expanded.Request.Arguments["table"]);
            Assert.AreEqual("1", expanded.Request.Arguments["id"]);
        }

        [TestMethod]
        public void Format_Json_ContainsColumn()
        {
            var result = QueryResult.FromRows("SELECT", ["Name"], [["Ada"]]);
            var json = ResultFormatter.Format(result, ResultFormat.Json, false);
            StringAssert.Contains(json, "Ada");
        }
    }
}
