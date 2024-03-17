namespace DBServices.Models.Interface
{
    public interface IBulkInsert
    {
        public string GetSqlForBulkAdd();
        public string[] GetFields();
    }
}