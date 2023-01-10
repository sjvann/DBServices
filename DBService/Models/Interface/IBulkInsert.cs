namespace DBService.Models.Interface
{
    public interface IBulkInsert
    {
        public string GetSqlForBlukAdd();
        public string[] GetFields();
    }
}