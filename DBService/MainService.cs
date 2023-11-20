using Dapper;
using DBService.Models;
using DBService.Models.Interface;
using DBService.SqlStringGenerator;
using DBService.SqlStringGenerator.MsSql;
using DBService.SqlStringGenerator.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;


namespace DBService
{
    public class MainService : IDbService, ISupportedDb
    {
        private static MainService? _instance = null;
        private readonly string _connectString;
        private DbConnection? _conn;
        private SqlProviderBase? _sqlProvider;
        private TableBaseModel? _currentTable;
        private IEnumerable<string>? _tableNameList;
        private IEnumerable<FieldBaseModel>? _currentTableFields;
        private readonly static object lockObject = new();
        private string? _currentTableName;
        #region 1. 建構元
        public static MainService GetInstance(string connectString)
        {
            if (_instance == null)
            {
                lock (lockObject)
                {
                    _instance ??= new MainService(connectString);
                }
            }
            return _instance;
        }
        private MainService(string connectString)
        {
            _connectString = connectString;
        }
        #endregion

        #region 2. 環境設定作業區
        #region 設定資料庫廠商
        public MainService UseSQLite()
        {
            _conn = new SqliteConnection(_connectString);
            _sqlProvider = new SqlProviderForSqlite();
            _tableNameList = GetAllTables();
            return this;
        }

        public MainService UseMsSQL()
        {
            _conn = new SqlConnection(_connectString);
            _sqlProvider = new SqlProviderForMsSql();
            _tableNameList = GetAllTables();
            return this;
        }

        #endregion
        #region 設定要處理的資料表
        ///// <summary>
        ///// 取得資料庫中所有資料表(包含view)
        ///// </summary>
        ///// <returns></returns>
        public IEnumerable<string>? GetTableNamesFromDb() => _tableNameList;
        public IEnumerable<FieldBaseModel>? GetCurrentTableFields() => _currentTableFields;

        public TableBaseModel? SetCurrentTable(string tableName)
        {
            if (tableName == null) return null;
            _currentTableName = tableName;
            _currentTableFields = CurrentTableFields(tableName);
            _currentTable = new()
            {
                Connection = _conn,
                TableName = tableName,
                Fields = _currentTableFields
            };
            return _currentTable;
        }
        public TableBaseModel? GetCurrentTable() => _currentTable;
        #endregion
        #endregion

        #region 3. 主要功能區

        #region 控制管理類功能
        public bool CheckHasRecord(string tableName)
        {
            bool result = false;

            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForCheckRows(tableName);
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
        public TableBaseModel? GetRecordByTable(string tableName)
        {

            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel? tableResult = null;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForAll(tableName);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        tableResult = MapToTableBaseModel(temp, tableName);
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
        public TableBaseModel? GetRecordById(long id, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel? tableResult = null;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlById(tableName, id);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        tableResult = MapToTableBaseModel(temp, tableName);
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
        public TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, string? opertor = null, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel? tableResult = null;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = (query.Value != null) ? _sqlProvider.GetSqlByKeyValue(tableName, query.Key, query.Value, opertor) : null;
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        tableResult = MapToTableBaseModel(temp, tableName);
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
        public TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel? tableResult = null;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlByKeyValues(tableName, query);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        tableResult = MapToTableBaseModel(temp, tableName);

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
        public TableBaseModel? GetRecordForAll(string? whereStr = null, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel? tableResult = null;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForAll(tableName, whereStr);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        tableResult = MapToTableBaseModel(temp, tableName);
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
        public TableBaseModel? GetRecordForField(string[] fields, string? whereStr = null, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel? tableResult = null;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForFields(tableName, fields, whereStr);
                        var temp = (sql != null) ? _conn.Query(sql) : null;
                        tableResult = MapToTableBaseModel(temp, tableName);
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
        public IEnumerable<string>? GetValueSetByFieldName(string fieldName, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            List<string> result = [];
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();

                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForValueSet(tableName, fieldName);
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
        public TableBaseModel? AddRecord(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForInsert(tableName, source);

                        int resultId = sql != null ? _conn.Execute(sql) : 0;
                        if (resultId != 0)
                        {
                            string? sql2 = _sqlProvider.GetSqlLastInsertId(tableName);
                            var temp = string.IsNullOrEmpty(sql2) ? null : _conn.Query(sql2);

                            if (temp != null && temp.Any())
                            {
                                var t1 = (IEnumerable<KeyValuePair<string, object?>>)temp.First();
                                var t2 = t1.First();
                                var lastId = Convert.ToInt64(t2.Value);
                                return GetRecordById(lastId, tableName);
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
        public TableBaseModel? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;
            TableBaseModel tableResult = new();
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForUpdate(tableName, id, source);
                        int effectRow = sql != null ? _conn.Execute(sql) : 0;
                        if (effectRow > 0)
                        {
                            var temp = GetRecordById(id, tableName);
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
        public bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return false;

            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForUpdateByKey(tableName, query, source);
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
        public bool DeleteRecordById(long id, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return false;
            if (_conn != null && id != 0)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForDelete(tableName, id);
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
        public bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return false;
            if (_conn != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    if (_sqlProvider != null)
                    {
                        string? sql = _sqlProvider.GetSqlForDeleteByKey(tableName, criteria);
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
        private TableBaseModel? MapToTableBaseModel(IEnumerable<dynamic>? target, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (string.IsNullOrEmpty(tableName)) return null;

            if (_sqlProvider == null || target == null) return null;
            TableBaseModel tableResult = new();
            List<RecordBaseModel> result = [.. _sqlProvider.MapToRecordBaseModel(target)];
            tableResult.Records = result;
            tableResult.TableName = tableName;
            tableResult.Connection = _conn;
            tableResult.Fields = _currentTableFields ?? CurrentTableFields(tableName);

            return tableResult;
        }

        #region Init Process

        /// 取回資料庫中所有資料表名稱
        /// </summary>
        /// <returns></returns>
        private List<string> GetAllTables()
        {
           
            List<string> result = [];
            if (_sqlProvider != null && _conn != null)
            {
                 _conn.Open();
                var sqlStr = _sqlProvider.GetSqlTableNameList();
                if (sqlStr != null)
                {
                    result.AddRange(_conn.Query<string>(sqlStr));
                }
                 _conn.Close();
            }
           
            return result;
        }
        /// <summary>
        /// 取回資料表的所有欄位資訊
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private IEnumerable<FieldBaseModel>? CurrentTableFields(string tableName)
        {
            if (_sqlProvider == null || _conn == null) return null;
            var sql = _sqlProvider.GetSqlFieldsByTableName(tableName);
            var result = string.IsNullOrEmpty(sql)? null: _conn.Query(sql);
            string? foreignSql = _sqlProvider.GetSqlForeignInfoByTableName(tableName);
            var foreignness = string.IsNullOrEmpty(foreignSql)? null: _conn.Query(foreignSql);
            if(result != null && foreignness != null)
            {
                return _sqlProvider.MapToFieldBaseModel(result, foreignness);
            }
            else
            {
                return null;
            }
            

        }
        #endregion

        #endregion
    }
}
