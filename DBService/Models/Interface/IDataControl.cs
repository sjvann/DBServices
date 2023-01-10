using DBService.Models;


namespace DBService.Models.Interface
{
    public interface IDataControl
    {
        string? GetSqlForCheckRows(string tableName);
        string? GetSqlForCheckTableExist(string tableName);
        string? GetSqlLastInsertId(string tableName);
        string? GetSqlForTruncate(string tableName);
    }
}
