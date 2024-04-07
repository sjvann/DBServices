namespace DbServices.Core.Models.Interface
{
    public interface IBulkInsert
    {
        public string GetSqlForBulkAdd();
        public string[] GetFields();
    }
}