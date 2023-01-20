using DBService.Models;


namespace DBService.Models.Interface
{
    public interface IDataControl
    {
        string? GetSqlForCheckRows();
        string? GetSqlForCheckTableExist();
        string? GetSqlLastInsertId();
        string? GetSqlForTruncate();
    }
}
