using DbServices.Core.Models;
using DbServices.Core.Models.Interface;

namespace DbServices.Core.Catalog
{
    public sealed class CatalogService(IDbService db) : ICatalogService
    {
        public IReadOnlyList<string> ListTables(bool includeView = true)
            => db.GetAllTableNames(includeView) ?? [];

        public IReadOnlyList<FieldBaseModel> ListColumns(string tableName)
            => db.GetFieldsByTableName(tableName)?.ToList() ?? [];

        public IReadOnlyList<string> ListDatabases()
        {
            if (db is DataBaseService data)
                return data.ListDatabases();
            return [];
        }

        public void Reload()
        {
            if (db is DataBaseService data)
                data.ReloadCatalog();
        }
    }
}
