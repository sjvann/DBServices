using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDataManipulate
    {
        string? GetSqlForDelete(string tableName,int id);
        string? GetSqlForDeleteByKey(string tableName,KeyValuePair<string, object> criteria);
        string? GetSqlForInsert(string tableName,IEnumerable<KeyValuePair<string, object>> source);
        string? GetSqlForUpdate(string tableName,int id, IEnumerable<KeyValuePair<string, object>> source);
        string? GetSqlForUpdateByKey(string tableName,KeyValuePair<string, object> criteria, IEnumerable<KeyValuePair<string, object>> source);
    }
}
