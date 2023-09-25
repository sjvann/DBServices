using Dapper;
using DBService.Models;
using DBService.Models.Interface;
using DBService.SqlStringGenerator;
using DBService.SqlStringGenerator.MsSql;
using DBService.SqlStringGenerator.Sqlite;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;

namespace DBService
{
    public class MainService : IDbService
    {
        private static MainService? _instance = null;
        private DbConnection? _conn;
        private readonly string? _connectString;
        private SqlProviderBase? _sqlProvider;
        private TableBaseModel? _current;
        private IEnumerable<string>? _tableNameList;
        private IDictionary<string, IEnumerable<FieldBaseModel>>? _fieldListWithTableName;
        private readonly static object lockObject = new();
        #region 1. 建構元
        public static MainService GetInstance(string connectString)
        {
            if (_instance == null)
            {
                lock (lockObject)
                {
                    _instance ??= new MainService(connectString);
                }
                _instance = new MainService(connectString);
            }
            return _instance;
        }
        private MainService(string connectString)
        {
            _connectString = connectString;
            _conn = new SqlConnection(connectString);


        }
        #endregion

        #region 2. 環境設定作業區
        #region 設定資料庫廠商
        public MainService UseSQLite(string? tableName = null)
        {
            _conn = new SQLiteConnection(_connectString);
            _sqlProvider = new SqlProviderForSqlite(tableName);
            GetAllFieldList();
            SetCurrent(tableName);
            return this;
        }
        public MainService UseMsSQL(string? tableName = null)
        {
            _conn = new SqlConnection(_connectString);
            _sqlProvider = new SqlProviderForMsSql(tableName);
            GetAllFieldList();
            SetCurrent(tableName);
            return this;
        }
        #endregion
        #region 設定要處理的資料表
        /// <summary>
        /// 取得資料庫中所有資料表(包含view)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetTableNameList()
        {
            if (_tableNameList == null) SetTableNameList();
            return _tableNameList ?? new List<string>();

        }

        public void SetCurrent(string? tableName)
        {
            if (tableName != null)
            {
                _sqlProvider?.SetTableName(tableName);
                var targetTable = (_fieldListWithTableName != null && _fieldListWithTableName.Any()) ? _fieldListWithTableName?.First(x => x.Key == tableName) : null;
                _current = new()
                {
                    ConnectString = _connectString,
                    TableName = tableName,
                    Fields = targetTable?.Value,
                    PkFieldList = targetTable?.Value.Where(x => x.IsKey.HasValue && x.IsKey.Value).Select(x => x.FieldName!)
                };
            }
        }
        public TableBaseModel? GetCurrent()
        {
            return _current;
        }
        #endregion
        #endregion

        #region 3. 主要功能區

        #region 控制管理類功能
        public bool CheckHasRecord()
        {
            bool result = false;

            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForCheckRows();
                        if (sql != null)
                        {
                            result = _conn.Query(sql).Any();
                        }
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.Message);

                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);

                }
            }
            return result;

        }
        #endregion
        #region 查詢類服務
        public TableBaseModel? GetRecordByTable(string? tableName = null)
        {

            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        List<RecordBaseModel> result = new();
                        if (tableName != null) _sqlProvider.SetTableName(tableName);
                        string? sql = _sqlProvider.GetSqlForAll();
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        if (temp != null && temp.Any())
                        {
                            if (_current != null)
                            {
                                result.AddRange(MapToRecordBased(temp, _current.PkFieldList));
                                _current.Records = result;
                                tableResult = _current;
                            }
                            else
                            {
                                result.AddRange(MapToRecordBased(temp, null));
                                tableResult.Records = result;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return tableResult;

        }
        public TableBaseModel? GetRecordById(long id)
        {
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        List<RecordBaseModel> result = new();
                        string? sql = _sqlProvider.GetSqlById(id);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        if (temp != null && temp.Any())
                        {
                            if (_current != null)
                            {
                                result.AddRange(MapToRecordBased(temp, _current.PkFieldList));
                                _current.Records = result;
                                tableResult = _current;
                            }
                            else
                            {
                                result.AddRange(MapToRecordBased(temp, null));
                                tableResult.Records = result;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return tableResult;
        }
        public TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, string? opertor = null)
        {
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        List<RecordBaseModel> result = new();


                        string? sql = (query.Value != null) ? _sqlProvider.GetSqlByKeyValue(query.Key, query.Value, opertor) : null;
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        if (temp != null && temp.Any())
                        {
                            if (_current != null)
                            {
                                result.AddRange(MapToRecordBased(temp, _current.PkFieldList));
                                _current.Records = result;
                                tableResult = _current;
                            }
                            else
                            {
                                result.AddRange(MapToRecordBased(temp, null));
                                tableResult.Records = result;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return tableResult;
        }
        public TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query)
        {
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        List<RecordBaseModel> result = new();

                        string? sql = _sqlProvider.GetSqlByKeyValues(query);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        if (temp != null && temp.Any())
                        {
                            if (_current != null)
                            {
                                result.AddRange(MapToRecordBased(temp, _current.PkFieldList));
                                _current.Records = result;
                                tableResult = _current;
                            }
                            else
                            {
                                result.AddRange(MapToRecordBased(temp, null));
                                tableResult.Records = result;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return tableResult;
        }
        public TableBaseModel? GetRecordForAll(string? whereStr = null)
        {
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        List<RecordBaseModel> result = new();

                        string? sql = _sqlProvider.GetSqlForAll(whereStr);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        if (temp != null && temp.Any())
                        {
                            if (_current != null)
                            {
                                result.AddRange(MapToRecordBased(temp, _current.PkFieldList));
                                _current.Records = result;
                                tableResult = _current;
                            }
                            else
                            {
                                result.AddRange(MapToRecordBased(temp, null));
                                tableResult.Records = result;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return tableResult;
        }
        public TableBaseModel? GetRecordForField(string[] fields, string? whereStr = null)
        {
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        List<RecordBaseModel> result = new();

                        string? sql = _sqlProvider.GetSqlForFields(fields, whereStr);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        if (temp != null && temp.Any())
                        {
                            if (_current != null)
                            {
                                result.AddRange(MapToRecordBased(temp, _current.PkFieldList));
                                _current.Records = result;
                                tableResult = _current;
                            }
                            else
                            {
                                result.AddRange(MapToRecordBased(temp, null));
                                tableResult.Records = result;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return tableResult;
        }
        public IEnumerable<string> GetValueSetByFieldName(string fieldName)
        {
            List<string> result = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForValueSet(fieldName);
                        var temp = (sql != null) ? _conn.Query<string>(sql) : null;
                        if (temp != null)
                        {
                            result.AddRange(temp);
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return result;
        }
        #endregion
        #region 新增更新刪除服務
        public TableBaseModel? AddRecord(IEnumerable<KeyValuePair<string, object?>> source)
        {

            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForInsert(source);

                        int resultId = sql != null ? _conn.Execute(sql) : 0;
                        if (resultId != 0)
                        {
                            string? sql2 = _sqlProvider.GetSqlLastInsertId();
                            var temp = (sql != null) ? _conn.Query(sql2) : null;

                            if (temp != null && temp.Any())
                            {
                                var t1 = (IEnumerable<KeyValuePair<string, object?>>)temp.First();
                                var t2 = t1.First();
                                var lastId = Convert.ToInt64(t2.Value);
                                return GetRecordById(lastId);
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return tableResult;

        }
        public TableBaseModel UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source)
        {
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForUpdate(id, source);
                        int effectRow = sql != null ? _conn.Execute(sql) : 0;
                        if (effectRow > 0)
                        {
                            var temp = GetRecordById(id);
                            if (temp != null)
                            {
                                tableResult = temp;
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return tableResult;

        }
        public bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source)
        {

            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForUpdateByKey(query, source);
                        int effectRow = sql != null ? _conn.Execute(sql) : 0;
                        if (effectRow > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (SqlException ex)
                {

                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return false;

        }
        public bool DeleteRecordById(long id)
        {
            if (_conn != null && id != 0)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForDelete(id);
                        int effectRow = sql != null ? _conn.Execute(sql) : 0;
                        return effectRow > 0;
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return false;
        }
        public bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria)
        {
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForDeleteByKey(criteria);
                        int effectRow = sql != null ? _conn.Execute(sql) : 0;
                        return effectRow > 0;
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return false;

        }
        #endregion
        #endregion

        #region 4. Private Method
        /// <summary>
        /// 取回資料庫中所有資料表與欄位
        /// </summary>
        /// <returns></returns>
        private void GetAllFieldList()
        {
            if (_fieldListWithTableName == null || !_fieldListWithTableName.Any())
            {
                var tableList = GetTableNameList();
                SetAllFieldList(tableList);
            }
        }
        public static IEnumerable<RecordBaseModel> MapToRecordBased(IEnumerable<dynamic> target, IEnumerable<string>? PkFieldName, string? FkFieldId = null)
        {
            List<RecordBaseModel> result = new();

            foreach (var item in target)
            {
                List<string> pkValue = new();
                List<KeyValuePair<string, object>> recordValue = new();
                RecordBaseModel record = new();
                record.ParentKeyValue = FkFieldId;

                if (item is IDictionary<string, object> recordTarget)
                {
                    if (PkFieldName != null)
                    {
                        pkValue.AddRange(from value in recordTarget
                                         where PkFieldName.Contains(value.Key)
                                         select value.Value as string);
                        record.PkFieldValue = pkValue;
                    }
                    recordValue.AddRange(recordTarget);
                    record.FieldValue = recordValue;
                    _ = int.TryParse(recordTarget.First(x => x.Key == "Id").Value.ToString(), out int id);
                    record.Id = id;
                    result.Add(record);
                }
            }

            return result;
        }
        public static IEnumerable<FieldBaseModel> MapToFieldBased(IEnumerable<dynamic> target)
        {
            List<FieldBaseModel> result = new();

            foreach (var item in target)
            {
                if (item is IDictionary<string, object> targets)
                {
                    FieldBaseModel field = new()
                    {
                        FieldName = targets.Where(x => x.Key == "name").Select(x => x.Value as string).FirstOrDefault(),
                        FieldType = targets.Where(x => x.Key == "type").Select(x => x.Value as string).FirstOrDefault()
                    };
                    var isPk = targets.Where(x => x.Key == "pk").Select(x => x.Value).FirstOrDefault();
                    if (isPk is long pk1)
                    {
                        field.IsKey = pk1 == 1;
                    }
                    else
                    {
                        field.IsKey = false;
                    }
                    result.Add(field);
                }
            }

            return result;
        }
        private void SetTableNameList()
        {
            if (_conn != null && _conn.State != ConnectionState.Open) _conn.Open();
            if (_sqlProvider != null)
            {
                var sqlStr = _sqlProvider.GetSqlTableNameList();
                _tableNameList = _conn.Query<string>(sqlStr);
            }
        }
        private void SetAllFieldList(IEnumerable<string> tableNames)
        {
            Dictionary<string, IEnumerable<FieldBaseModel>> _allFieldList = new();
            if (_sqlProvider != null)
            {
                foreach (var tableName in tableNames)
                {
                    var sql = _sqlProvider.GetSqlFieldsByName(tableName);

                    var fields = _conn.Query(sql);
                    if (fields != null)
                    {

                        var fieldList = MapToFieldBased(fields);
                        if (fieldList != null)
                        {
                            _allFieldList.Add(tableName, fieldList);
                        }
                    }
                }
            }
            _fieldListWithTableName = _allFieldList;
        }



        #endregion
    }
}
