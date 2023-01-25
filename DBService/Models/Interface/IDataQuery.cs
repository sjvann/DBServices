using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDataQuery
    {
        string? GetSqlById(object id);
        string? GetSqlByKeyValue(string key, object value);
        string? GetSqlByKeyValues(IDictionary<string, object?> values);
        string? GetSqlByKeyValues(IEnumerable<KeyValuePair<string, object?>> values);
        string? GetSqlForAll(string? whereStr = null);
        string? GetSqlForFields(string[] fieldNames, string? whereStr = null);
        string? GetSqlForValueSet(string fieldName);
        string? GetSqlForInnerJoin(string targetTableName, string sourceKeyName, string targetKeyName);
        string? GetSqlForLeftJoin(string targetTableName, string sourceKeyName, string targetKeyName);
        string? GetSqlForRightJoin(string targetTableName, string sourceKeyName, string targetKeyName);
    }
}
