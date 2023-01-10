using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDataQuery
    {
        string? GetSqlById(string tableName, int id);
        string? GetSqlByKeyValue(string tableName, string key, object value);
        string? GetSqlByKeyValues(string tableName, IDictionary<string, object> values);
        string? GetSqlByKeyValues(string tableName, IEnumerable<KeyValuePair<string, object>> values);
        string? GetSqlForAll(string tableName, string? whereStr = null);
        string? GetSqlForFields(string tableName, string[] fieldNames, string? whereStr = null);
    }
}
