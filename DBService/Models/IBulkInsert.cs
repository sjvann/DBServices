namespace DBService.Models
{
    public interface IBulkInsert
    {
        public string GetSqlForBlukAdd();
        public string[] GetFields();
    }
}