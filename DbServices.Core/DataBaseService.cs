
using Dapper;
using DbServices.Core.Models;
using DbServices.Core.Models.Enum;
using DbServices.Core.Models.Interface;
using DbServices.Core.SqlStringGenerator;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DbServices.Core
{
    /// <summary>
    /// 抽象物件。由個別供應商的資料庫來繼承使用。
    /// 個別供應服務物件,提供DbConnection,SqlProvider,TableNameList等基本資料。
    /// 本物件可提供整體資料庫的服務。若再已知資料表的下,可以透過DataTableService來提供資料表的服務。
    /// </summary>
    /// <param name="connectionString"></param>
    public abstract class DataBaseService(string connectionString) : IDbService
    {
        protected DbConnection? _conn;
        protected SqlProviderBase? _sqlProvider;
        protected string[]? _tableNameList;
        protected string? _currentTableName;
        #region 前置處理服務

        public string? GetCurrentTableName() => _currentTableName;
        public void SetCurrentTableName(string tableName)
        {
            if (_tableNameList == null || !_tableNameList.Contains(tableName))
            {
                throw new ArgumentException($"{tableName} 資料表不存在資料庫中。");
            }
            else
            {
                _currentTableName = tableName;
            }
        }
        public void SetCurrentTable<T>() where T : TableDefBaseModel
        {
            var tableName = typeof(T).Name;
            SetCurrentTableName(tableName);
        }



        #endregion
        #region 整體服務類
        public bool HasTable(string tableName)
        {
            bool result = false;
            if (_conn == null || _sqlProvider == null) return result;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();
                string? sql = _sqlProvider.GetSqlForCheckTableExist(tableName);
                if (!string.IsNullOrEmpty(sql))
                {
                    result = _conn.Query(sql).Any();
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
            return result;
        }
        public bool HasRecord(string? tableName = null)
        {
            bool result = false;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return result;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();
                string? sql = _sqlProvider.GetSqlForCheckRows(tableName);
                if (sql != null)
                {
                    result = _conn.Query(sql).Any();
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
            return result;
        }


        /// <summary>
        /// 取回所有資料表名稱
        /// </summary>
        /// <param name="includeView"></param>
        /// <returns></returns>
        public virtual string[]? GetAllTableNames(bool includeView = true)
        {
            List<string> result = [];
            if (_conn == null || _sqlProvider == null) return null;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();
                string? sqlStr = _sqlProvider.GetSqlTableNameList();
                if (sqlStr != null)
                {
                    result.AddRange(_conn.Query<string>(sqlStr));
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
            return [.. result];

        }
        public virtual IEnumerable<FieldBaseModel>? GetFieldsByTableName(string tableName)
        {
            if (_sqlProvider == null || _conn == null || string.IsNullOrEmpty(tableName)) return null;
            var sql = _sqlProvider.GetSqlFieldsByTableName(tableName);
            var result = string.IsNullOrEmpty(sql) ? null : _conn.Query(sql);
            string? foreignSql = _sqlProvider.GetSqlForeignInfoByTableName(tableName);
            var foreignness = string.IsNullOrEmpty(foreignSql) ? null : _conn.Query(foreignSql);
            if (result != null && foreignness != null)
            {
                return _sqlProvider.MapToFieldBaseModel(result, foreignness);
            }
            else
            {
                return null;
            }
        }

        public virtual IEnumerable<object>? GetValueSetByFieldName(string fieldName, string? tableName = null)
        {
            List<object> result = [];
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return result;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();
                string? sql = _sqlProvider.GetSqlForValueSet(tableName, fieldName);
                var temp = sql != null ? _conn.Query<object>(sql) : null;
                if (temp != null)
                {
                    result.AddRange(temp);
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

            return result;
        }

        #endregion
        #region DDL
        public int CreateNewTable<T>() where T : TableDefBaseModel
        {
            int result = 0;
            if (_conn == null || _sqlProvider == null) return result;
            var dbSource = TableBaseModel.CreateTableBaseModel<T>();
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForCreateTable(dbSource);
                if (!string.IsNullOrEmpty(sql))
                {
                    result = _conn.Execute(sql);
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

            return result;
        }

        public int DropTable(string tableName)
        {
            int result = 0;
            if (_conn == null || _sqlProvider == null) return result;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForDropTable(tableName);
                if (!string.IsNullOrEmpty(sql))
                {
                    result = _conn.Execute(sql);
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

            return result;
        }

        #endregion
        #region 查詢類
        public virtual TableBaseModel? GetRecordById(long id, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || tableName == null) return tableResult;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlById(tableName, id);
                var temp = sql != null ? _conn.Query(sql) : null;
                tableResult = MapToTableBaseModel(temp, tableName);

            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }
            return tableResult;
        }

        public virtual TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, EnumQueryOperator? giveOperator = null, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = query.Value != null ? _sqlProvider.GetSqlByKeyValue(tableName, query.Key, query.Value, giveOperator) : null;
                var temp = sql != null ? _conn.Query(sql) : null;
                tableResult = MapToTableBaseModel(temp, tableName);

            }
            catch (SqlException ex)
            {

                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return tableResult;
        }

        public virtual TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || tableName == null) return tableResult;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();


                string? sql = _sqlProvider.GetSqlByKeyValues(tableName, query);
                var temp = sql != null ? _conn.Query(sql) : null;
                tableResult = MapToTableBaseModel(temp, tableName);
            }
            catch (SqlException ex)
            {

                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }


            return tableResult;
        }


        public virtual TableBaseModel? GetRecordByTableName(string tableName)
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
                        var temp = sql != null ? _conn.Query(sql) : null;
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

        public virtual TableBaseModel? GetRecordWithWhere(string whereStr, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForAll(tableName, whereStr);
                var temp = sql != null ? _conn.Query(sql) : null;
                tableResult = MapToTableBaseModel(temp, tableName);

            }
            catch (SqlException ex)
            {

                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return tableResult;
        }

        public virtual TableBaseModel? GetRecordForField(string[] fields, string? whereStr = null, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForFields(tableName, fields, whereStr);
                var temp = sql != null ? _conn.Query(sql) : null;
                tableResult = MapToTableBaseModel(temp, tableName);

            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return tableResult;
        }
        public virtual TableBaseModel? GetRecordByForeignKeyForOneSide(string fkFieldName, long manySideId, string manySideTableName, string? oneSideTableName = null)
        {
            oneSideTableName ??= _currentTableName;
            if (string.IsNullOrEmpty(fkFieldName) || manySideId < 1 || string.IsNullOrEmpty(manySideTableName) || string.IsNullOrEmpty(oneSideTableName)) return null;
            string[] fieldNames = [fkFieldName];
            TableBaseModel? manySideResult = GetRecordForField(fieldNames, $"Id = {manySideId}", manySideTableName);
            if (manySideResult?.Records is IEnumerable<RecordBaseModel> records)
            {
                var target = records.FirstOrDefault()?.GetFieldValue(fkFieldName);
                if (target is long qId && qId > 0)
                {
                    return GetRecordById(qId, oneSideTableName);
                }
            }
            return null;
        }

        public TableBaseModel? GetRecordByForeignKeyForManySide(int oneSideId, string fkFieldName, string manySideTableName, string? oneSideTableName = null)
        {

            if (string.IsNullOrEmpty(fkFieldName) || oneSideId < 1 || string.IsNullOrEmpty(manySideTableName)) return null;

            return GetRecordWithWhere($"{fkFieldName} = {oneSideId}", manySideTableName);
        }

        #endregion
        #region 操作類
        public int ExecuteStoredProcedure(string spName, IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            if (_conn == null) return 0;
            return _conn.Execute(spName, SetupParameter(parameters), commandType: CommandType.StoredProcedure);

        }
        public int ExecuteSQL(string sql)
        {
            if (_conn == null) return 0;
            return _conn.Execute(sql);
        }
        public virtual TableBaseModel? InsertRecord(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

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
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return tableResult;
        }

        public virtual TableBaseModel? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

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
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return tableResult;
        }

        public virtual bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {

            tableName ??= _currentTableName;

            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return false;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForUpdateByKey(tableName, query, source);
                int effectRow = sql != null ? _conn.Execute(sql) : 0;
                if (effectRow > 0)
                {
                    return true;
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

            return false;
        }
        public virtual bool DeleteRecordById(long id, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName) || id == 0) return false;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForDelete(tableName, id);
                int effectRow = sql != null ? _conn.Execute(sql) : 0;
                return effectRow > 0;

            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return false;
        }

        public virtual bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return false;

            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForDeleteByKey(tableName, criteria);
                int effectRow = sql != null ? _conn.Execute(sql) : 0;
                return effectRow > 0;
            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return false;
        }

        #endregion
        #region Private Method
        private static DynamicParameters SetupParameter(IEnumerable<KeyValuePair<string, object?>> source)
        {
            DynamicParameters result = new();
            foreach (var item in source)
            {
                result.Add(item.Key, item.Value);
            }
            return result;
        }
        private TableBaseModel? MapToTableBaseModel(IEnumerable<dynamic>? target, string? tableName = null)
        {

            tableName ??= _currentTableName;

            if (_sqlProvider == null || string.IsNullOrEmpty(tableName) || target == null) return null;

            TableBaseModel tableResult = new();
            List<RecordBaseModel> result = [.. _sqlProvider.MapToRecordBaseModel(target)];
            tableResult.Records = result;
            tableResult.TableName = tableName;
            tableResult.ConnectionString = connectionString;
            tableResult.Fields = GetFieldsByTableName(tableName);

            return tableResult;
        }


        #endregion


    }
}