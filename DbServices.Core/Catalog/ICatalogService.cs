using DbServices.Core.Models;

namespace DbServices.Core.Catalog
{
    public interface ICatalogService
    {
        IReadOnlyList<string> ListTables(bool includeView = true);
        IReadOnlyList<FieldBaseModel> ListColumns(string tableName);
        IReadOnlyList<string> ListDatabases();
        void Reload();
    }
}
