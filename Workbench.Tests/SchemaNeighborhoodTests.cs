using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Contracts;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class SchemaNeighborhoodTests
{
    private static List<SchemaTableDto> Sample() =>
    [
        Table("orders", Column("id", pk: true), Column("customerId", "customers", "id"), Column("qty")),
        Table("customers", Column("id", pk: true), Column("regionId", "regions", "id"), Column("name")),
        Table("regions", Column("id", pk: true), Column("name")),
    ];

    [TestMethod]
    public void First_hop_stops_at_the_direct_table()
    {
        var found = SchemaNeighborhood.Suggest(Sample(), "orders", 1);

        Assert.AreEqual(1, found.Count);
        Assert.AreEqual("customers", found[0].Table);
        Assert.AreEqual(1, found[0].Depth);
        Assert.AreEqual("orders", found[0].From);
        Assert.AreEqual("customerId", found[0].FromColumn);
        Assert.AreEqual("customers", found[0].To);
        Assert.AreEqual("id", found[0].ToColumn);
    }

    [TestMethod]
    public void Second_hop_keeps_the_table_in_between()
    {
        var found = SchemaNeighborhood.Suggest(Sample(), "orders", 2);
        var regions = found.Single(item => item.Table == "regions");

        Assert.AreEqual(2, regions.Depth);
        Assert.AreEqual("customers", regions.Parent);
        Assert.AreEqual("customers", regions.From);
        Assert.AreEqual("regionId", regions.FromColumn);
    }

    [TestMethod]
    public void Incoming_key_is_a_first_hop_from_the_referenced_table()
    {
        var found = SchemaNeighborhood.Suggest(Sample(), "customers", 1);
        var orders = found.Single(item => item.Table == "orders");

        Assert.AreEqual("orders", orders.From);
        Assert.AreEqual("customerId", orders.FromColumn);
        Assert.AreEqual("customers", orders.To);
    }

    [TestMethod]
    public void Unknown_root_has_no_suggestions()
    {
        Assert.AreEqual(0, SchemaNeighborhood.Suggest(Sample(), "missing", 2).Count);
    }

    private static SchemaTableDto Table(string name, params SchemaColumnDto[] columns) => new()
    {
        Name = name,
        Columns = columns.ToList(),
    };

    private static SchemaColumnDto Column(string name, string? table = null, string? column = null, bool pk = false) => new()
    {
        Name = name,
        PrimaryKey = pk,
        ReferencesTable = table,
        ReferencesColumn = column,
    };
}
