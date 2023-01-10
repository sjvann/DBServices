using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDbService
    {
        MainService UseMsSQL(string? tableName= null);
        MainService UseSQLite(string? tableName = null);
        IEnumerable<string> GetTableNameList();
        IDictionary<string, IEnumerable<FieldBaseModel>> GetAllFieldList();
        IEnumerable<FieldBaseModel>? GetFieldObjectByName(string tableName);
        bool CheckHasRecord();
        TableBaseModel? GetRecordByTable(string? tableName = null);
        TableBaseModel? GetRecordById(int id);
        TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object> query);
        TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object>> query);
        TableBaseModel? GetRecordForAll(string? whereStr = null);
        TableBaseModel? GetRecordForField(string[] fields, string? whereStr = null);
        TableBaseModel AddRecord(IEnumerable<KeyValuePair<string, object>> source);
        TableBaseModel UpdateRecordById(int id, IEnumerable<KeyValuePair<string, object>> source);
        bool UpdateRecordByKeyValue(KeyValuePair<string, object> query, IEnumerable<KeyValuePair<string, object>> source);
        bool DeleteRecordById(int id);
        bool DeleteRecordByKeyValue(KeyValuePair<string, object> criteria);
    }
}
