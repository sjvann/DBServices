using System.Data.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ConnectionFormTests
{
    [TestMethod]
    public void Sqlite_memory_uses_the_built_in_source()
    {
        var fields = ConnectionFields.For("Sqlite");
        fields.Place = "memory";

        var parsed = Parse(fields.Compose("Sqlite"));

        Assert.AreEqual(":memory:", parsed["Data Source"]);
        Assert.IsFalse(parsed.ContainsKey("Mode"));
    }

    [TestMethod]
    public void Sqlite_file_keeps_path_and_open_mode()
    {
        var fields = ConnectionFields.For("Sqlite");
        fields.FilePath = @"D:\data\warehouse.db";
        fields.OpenMode = "ReadOnly";

        var parsed = Parse(fields.Compose("Sqlite"));

        Assert.AreEqual(@"D:\data\warehouse.db", parsed["Data Source"]);
        Assert.AreEqual("ReadOnly", parsed["Mode"]);
    }

    [TestMethod]
    public void Sqlite_file_requires_a_path()
    {
        var result = ConnectionFields.For("Sqlite").Compose("Sqlite");

        Assert.IsFalse(result.Ok);
        Assert.AreEqual("請填寫檔案路徑。", result.Error!.Message);
    }

    [TestMethod]
    public void SqlServer_password_login_quotes_special_characters()
    {
        var fields = ConnectionFields.For("SqlServer");
        fields.Database = "app";
        fields.User = "sa";
        fields.Password = "p;a=ss";
        fields.TrustCertificate = "True";

        var parsed = Parse(fields.Compose("SqlServer"));

        Assert.AreEqual("localhost,1433", parsed["Server"]);
        Assert.AreEqual("app", parsed["Database"]);
        Assert.AreEqual("sa", parsed["User ID"]);
        Assert.AreEqual("p;a=ss", parsed["Password"]);
        Assert.AreEqual("Mandatory", parsed["Encrypt"]);
        Assert.AreEqual("True", parsed["TrustServerCertificate"]);
        Assert.IsFalse(parsed.ContainsKey("Integrated Security"));
    }

    [TestMethod]
    public void SqlServer_windows_login_omits_the_password()
    {
        var fields = ConnectionFields.For("SqlServer");
        fields.Database = "app";
        fields.Auth = "windows";
        fields.User = "sa";
        fields.Password = "secret";

        var parsed = Parse(fields.Compose("SqlServer"));

        Assert.AreEqual("True", parsed["Integrated Security"]);
        Assert.IsFalse(parsed.ContainsKey("Password"));
        Assert.IsFalse(parsed.ContainsKey("User ID"));
    }

    [TestMethod]
    public void MySql_uses_port_and_ssl_defaults()
    {
        var fields = ConnectionFields.For("MySQL");
        fields.Database = "app";
        fields.User = "root";
        fields.Password = "secret";

        var parsed = Parse(fields.Compose("MySQL"));

        Assert.AreEqual("localhost", parsed["Server"]);
        Assert.AreEqual("3306", parsed["Port"]);
        Assert.AreEqual("Preferred", parsed["SslMode"]);
        Assert.AreEqual("root", parsed["User ID"]);
    }

    [TestMethod]
    public void PostgreSql_uses_host_port_and_ssl_mode()
    {
        var fields = ConnectionFields.For("PostgreSQL");
        fields.Database = "app";
        fields.User = "postgres";
        fields.Password = "secret";

        var parsed = Parse(fields.Compose("PostgreSQL"));

        Assert.AreEqual("localhost", parsed["Host"]);
        Assert.AreEqual("5432", parsed["Port"]);
        Assert.AreEqual("postgres", parsed["Username"]);
        Assert.AreEqual("Prefer", parsed["SSL Mode"]);
    }

    [TestMethod]
    public void Oracle_service_uses_easy_connect()
    {
        var fields = ConnectionFields.For("Oracle");
        fields.OracleName = "ORCL";
        fields.User = "system";
        fields.Password = "secret";

        var parsed = Parse(fields.Compose("Oracle"));

        Assert.AreEqual("localhost:1521/ORCL", parsed["Data Source"]);
        Assert.AreEqual("system", parsed["User Id"]);
    }

    [TestMethod]
    public void Oracle_sid_uses_a_connect_descriptor()
    {
        var fields = ConnectionFields.For("Oracle");
        fields.OracleKind = "sid";
        fields.OracleName = "ORCL";
        fields.User = "system";
        fields.Password = "secret";

        var parsed = Parse(fields.Compose("Oracle"));

        Assert.AreEqual(
            "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SID=ORCL)))",
            parsed["Data Source"]);
    }

    [TestMethod]
    public void Network_fields_reject_a_blank_database_and_a_bad_port()
    {
        var blank = ConnectionFields.For("PostgreSQL");
        blank.User = "postgres";
        blank.Password = "secret";
        Assert.AreEqual("請填寫資料庫。", blank.Compose("PostgreSQL").Error!.Message);

        var port = ConnectionFields.For("PostgreSQL");
        port.Database = "app";
        port.User = "postgres";
        port.Password = "secret";
        port.Port = "0";
        Assert.AreEqual("連接埠必須是 1 到 65535。", port.Compose("PostgreSQL").Error!.Message);
    }

    private static DbConnectionStringBuilder Parse(Workbench.Core.OpResult<string> result)
    {
        Assert.IsTrue(result.Ok, result.Error?.Message);
        return new DbConnectionStringBuilder { ConnectionString = result.Value };
    }
}
