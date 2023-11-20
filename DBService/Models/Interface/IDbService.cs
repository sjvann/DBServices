using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDbService
    {
        IEnumerable<string>? GetTableNamesFromDb();
        TableBaseModel? SetCurrentTable(string tableName);
        TableBaseModel? GetCurrentTable();
        TableBaseModel? GetRecordByTable(string tableName);
        TableBaseModel? GetRecordById(long id, string? tableName = null);
        TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, string? opertor = null, string? tableName = null);
        TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, string? tableName = null);
        TableBaseModel? GetRecordForAll(string? whereStr = null, string? tableName = null);
        TableBaseModel? GetRecordForField(string[] fields, string? whereStr = null, string? tableName = null);
        TableBaseModel? AddRecord(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null);
        TableBaseModel? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null);
        IEnumerable<string>? GetValueSetByFieldName(string fieldName, string? tableName = null);
        bool CheckHasRecord(string tableName);
        bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null);
        bool DeleteRecordById(long id, string? tableName = null);
        bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria, string? tableName = null);

    }
}
