using Dapper;
using DbServices.Core.Models;
using DbServices.Core.Models.Enum;
using DbServices.Core.Models.Interface;
using DbServices.Core.SqlStringGenerator;
using DbServices.Core.Exceptions;
using DbServices.Core.Services;
using DbServices.Core.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DbServices.Core
{
    /// <summary>
    /// 抽象物件。由個別供應商的資料庫來繼承使用。
    /// 個別供應服務物件,提供DbConnection,SqlProvider,TableNameList等基本資料。
    /// 本物件可提供整體資料庫的服務。若再已知資料表的下,可以透過DataTableService來提供資料表的服務。
    /// </summary>
    /// <param name="connectionString"></param>
    public abstract class DataBaseService : IDbService, IDbServiceAsync, IDisposable
    {
        protected DbConnection? _conn;
        protected SqlProviderBase? _sqlProvider;
        protected string[]? _tableNameList;
        protected string? _currentTableName;
        protected readonly ILogger<DataBaseService>? _logger;
        protected readonly IValidationService? _validationService;
        protected readonly IRetryPolicyService? _retryPolicyService;
        protected readonly DbServiceOptions _options;
        private bool _disposed = false;

        protected DataBaseService(string connectionString)
        {
            _options = new DbServiceOptions { ConnectionString = connectionString };
        }

        protected DataBaseService(DbServiceOptions options, ILogger<DataBaseService>? logger = null, 
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _validationService = validationService;
            _retryPolicyService = retryPolicyService;
        }

			private void RefreshTableNameList(bool includeView = true)
			{
				// IMPORTANT: schema can change after service construction (e.g., unit tests creating/dropping tables).
				_tableNameList = GetAllTableNames(includeView);
			}
        #region 前置處理服務

        public string? GetCurrentTableName() => _currentTableName;
        public void SetCurrentTableName(string tableName)
        {
            try
            {
                _validationService?.ValidateTableName(tableName);
                
	                if (_tableNameList == null || !_tableNameList.Contains(tableName))
	                {
	                    // Refresh once in case schema changed since this service instance was created.
	                    RefreshTableNameList();
	                }

	                if (_tableNameList == null || !_tableNameList.Contains(tableName))
	                {
	                    _logger?.LogError("資料表 {TableName} 不存在資料庫中", tableName);
	                    throw new TableNotFoundException(tableName);
	                }
                else
                {
                    _currentTableName = tableName;
                    _logger?.LogDebug("設定目前資料表為: {TableName}", tableName);
                }
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "設定目前資料表失敗: {TableName}", tableName);
                throw new DbServiceException($"設定目前資料表失敗: {tableName}", ex);
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
                _validationService?.ValidateTableName(tableName);
                
                if (_conn.State != ConnectionState.Open) _conn.Open();
                string? sql = _sqlProvider.GetSqlForCheckTableExist(tableName);
                if (!string.IsNullOrEmpty(sql))
                {
                    result = _conn.Query(sql).Any();
                    _logger?.LogDebug("檢查資料表存在性: {TableName} = {Exists}", tableName, result);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "檢查資料表 {TableName} 是否存在時發生錯誤", tableName);
                throw new DbQueryException($"檢查資料表 {tableName} 是否存在時發生錯誤", ex);
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
			finally
			{
				RefreshTableNameList();
			}

            return result;
        }

        public int CreateNewTable(IEnumerable<FieldBaseModel> tableDefine, string? tableName = null)
        {
            int result = 1;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return result;
           
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                string? sql = _sqlProvider.GetSqlForCreateTable(tableName, tableDefine);
                if (!string.IsNullOrEmpty(sql))
                {
                    _conn.Execute(sql);
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
			finally
			{
				RefreshTableNameList();
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
			finally
			{
				RefreshTableNameList();
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
        #region 非同步方法實作
        
        /// <summary>
        /// 非同步檢查資料表是否存在
        /// </summary>
        public async Task<bool> HasTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            bool result = false;
            if (_conn == null || _sqlProvider == null) return result;
            
            try
            {
                _validationService?.ValidateTableName(tableName);
                
                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);
                    
                string? sql = _sqlProvider.GetSqlForCheckTableExist(tableName);
                if (!string.IsNullOrEmpty(sql))
                {
                    var queryResult = await _conn.QueryAsync(sql);
                    result = queryResult.Any();
                    _logger?.LogDebug("非同步檢查資料表存在性: {TableName} = {Exists}", tableName, result);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步檢查資料表 {TableName} 是否存在時發生錯誤", tableName);
                throw new DbQueryException($"檢查資料表 {tableName} 是否存在時發生錯誤", ex);
            }
            return result;
        }

        public async Task<string[]?> GetAllTableNamesAsync(bool includeView = true, CancellationToken cancellationToken = default)
        {
            if (_conn == null || _sqlProvider == null) return null;

            try
            {
                if (_retryPolicyService != null)
                {
                    return await _retryPolicyService.ExecuteDatabaseOperationAsync(async () =>
                    {
                        return await GetAllTableNamesInternalAsync(includeView, cancellationToken);
                    }, cancellationToken);
                }
                else
                {
                    return await GetAllTableNamesInternalAsync(includeView, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步取得所有資料表名稱時發生錯誤");
                throw new DbQueryException("取得所有資料表名稱時發生錯誤", ex);
            }
        }

        private async Task<string[]?> GetAllTableNamesInternalAsync(bool includeView, CancellationToken cancellationToken)
        {
            if (_conn!.State != ConnectionState.Open) 
                await _conn.OpenAsync(cancellationToken);

            string? sql = _sqlProvider!.GetSqlTableNameList(includeView);
            if (!string.IsNullOrEmpty(sql))
            {
                var temp = await _conn.QueryAsync(sql);
                var result = temp.Select(x => ((dynamic)x).TABLE_NAME?.ToString())
                                 .Where(name => !string.IsNullOrEmpty(name))
                                 .OfType<string>()
                                 .ToArray();
                _logger?.LogDebug("非同步取得 {Count} 個資料表名稱", result.Length);
                return result;
            }
            return null;
        }

        public async Task<IEnumerable<FieldBaseModel>?> GetFieldsByTableNameAsync(string tableName, CancellationToken cancellationToken = default)
        {
            if (_conn == null || _sqlProvider == null) return null;

            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = _sqlProvider.GetSqlFieldsByTableName(tableName);
                string? sqlForeign = _sqlProvider.GetSqlForeignInfoByTableName(tableName);

                if (!string.IsNullOrEmpty(sql))
                {
                    var temp = await _conn.QueryAsync(sql);
                    var tempForeign = !string.IsNullOrEmpty(sqlForeign) ? await _conn.QueryAsync(sqlForeign) : null;
                    var result = _sqlProvider.MapToFieldBaseModel(temp, tempForeign ?? Enumerable.Empty<dynamic>());
                    _logger?.LogDebug("非同步取得資料表 {TableName} 的 {Count} 個欄位", tableName, result.Count());
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步取得資料表 {TableName} 欄位時發生錯誤", tableName);
                throw new DbQueryException($"取得資料表 {tableName} 欄位時發生錯誤", ex);
            }
            return null;
        }

        public async Task<bool> HasRecordAsync(string? tableName = null, CancellationToken cancellationToken = default)
        {
            bool result = false;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return result;
            
            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);
                    
                string? sql = _sqlProvider.GetSqlForCheckRows(tableName);
                if (sql != null)
                {
                    var queryResult = await _conn.QueryAsync(sql);
                    result = queryResult.Any();
                    _logger?.LogDebug("非同步檢查資料表 {TableName} 是否有記錄: {HasRecord}", tableName, result);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步檢查資料表 {TableName} 是否有記錄時發生錯誤", tableName);
                throw new DbQueryException($"檢查資料表 {tableName} 是否有記錄時發生錯誤", ex);
            }
            return result;
        }

        public async Task<TableBaseModel?> GetRecordByIdAsync(long id, string? tableName = null, CancellationToken cancellationToken = default)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || tableName == null) return tableResult;
            
            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = _sqlProvider.GetSqlById(tableName, id);
                if (sql != null)
                {
                    var temp = await _conn.QueryAsync(sql);
                    tableResult = MapToTableBaseModel(temp, tableName);
                    _logger?.LogDebug("非同步取得資料表 {TableName} ID {Id} 的記錄", tableName, id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步取得資料表 {TableName} ID {Id} 記錄時發生錯誤", tableName, id);
                throw new DbQueryException($"取得資料表 {tableName} ID {id} 記錄時發生錯誤", ex);
            }
            return tableResult;
        }

        public async Task<TableBaseModel?> GetRecordByKeyValueAsync(KeyValuePair<string, object?> query, EnumQueryOperator? giveOperator = null, string? tableName = null, CancellationToken cancellationToken = default)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                _validationService?.ValidateTableName(tableName);
                _validationService?.ValidateFieldName(query.Key);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = query.Value != null ? _sqlProvider.GetSqlByKeyValue(tableName, query.Key, query.Value, giveOperator) : null;
                if (sql != null)
                {
                    var temp = await _conn.QueryAsync(sql);
                    tableResult = MapToTableBaseModel(temp, tableName);
                    _logger?.LogDebug("非同步依據條件取得資料表 {TableName} 記錄: {Key}={Value}", tableName, query.Key, query.Value);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步依據條件取得資料表 {TableName} 記錄時發生錯誤: {Key}={Value}", tableName, query.Key, query.Value);
                throw new DbQueryException($"依據條件取得資料表 {tableName} 記錄時發生錯誤", ex);
            }

            return tableResult;
        }

        public async Task<TableBaseModel?> InsertRecordAsync(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null, CancellationToken cancellationToken = default)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = _sqlProvider.GetSqlForInsert(tableName, source);
                if (!string.IsNullOrEmpty(sql))
                {
                    int effectRow = await _conn.ExecuteAsync(sql);
                    if (effectRow > 0)
                    {
                        string? lastIdSql = _sqlProvider.GetSqlLastInsertId(tableName);
                        if (!string.IsNullOrEmpty(lastIdSql))
                        {
                            var t1 = await _conn.QueryAsync(lastIdSql);
                            if (t1.Any())
                            {
                                var t2 = t1.First();
                                var lastId = Convert.ToInt64(t2.Value);
                                tableResult = await GetRecordByIdAsync(lastId, tableName, cancellationToken);
                                // 記錄成功新增的日誌
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步新增記錄到資料表 {TableName} 時發生錯誤", tableName);
                throw new DbQueryException($"新增記錄到資料表 {tableName} 時發生錯誤", ex);
            }

            return tableResult;
        }

        public async Task<TableBaseModel?> UpdateRecordByIdAsync(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null, CancellationToken cancellationToken = default)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = _sqlProvider.GetSqlForUpdate(tableName, id, source);
                if (sql != null)
                {
                    int effectRow = await _conn.ExecuteAsync(sql);
                    if (effectRow > 0)
                    {
                        tableResult = await GetRecordByIdAsync(id, tableName, cancellationToken);
                        _logger?.LogInformation("非同步更新資料表 {TableName} ID {Id} 的記錄", tableName, id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步更新資料表 {TableName} ID {Id} 記錄時發生錯誤", tableName, id);
                throw new DbQueryException($"更新資料表 {tableName} ID {id} 記錄時發生錯誤", ex);
            }

            return tableResult;
        }

        public async Task<bool> DeleteRecordByIdAsync(long id, string? tableName = null, CancellationToken cancellationToken = default)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName) || id == 0) return false;

            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = _sqlProvider.GetSqlForDelete(tableName, id);
                if (sql != null)
                {
                    int effectRow = await _conn.ExecuteAsync(sql);
                    bool result = effectRow > 0;
                    _logger?.LogInformation("非同步刪除資料表 {TableName} ID {Id} 的記錄: {Success}", tableName, id, result);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步刪除資料表 {TableName} ID {Id} 記錄時發生錯誤", tableName, id);
                throw new DbQueryException($"刪除資料表 {tableName} ID {id} 記錄時發生錯誤", ex);
            }

            return false;
        }

        public async Task<IEnumerable<object>?> GetValueSetByFieldNameAsync(string fieldName, string? tableName = null, CancellationToken cancellationToken = default)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return null;

            try
            {
                _validationService?.ValidateTableName(tableName);
                _validationService?.ValidateFieldName(fieldName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                string? sql = _sqlProvider.GetSqlForValueSet(tableName, fieldName);
                if (!string.IsNullOrEmpty(sql))
                {
                    var temp = await _conn.QueryAsync(sql);
                    var result = temp.Select(x => (object)x.GetType().GetProperty(fieldName)?.GetValue(x, null)!).Distinct();
                    _logger?.LogDebug("非同步取得資料表 {TableName} 欄位 {FieldName} 的 {Count} 個不重複值", tableName, fieldName, result.Count());
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "非同步取得資料表 {TableName} 欄位 {FieldName} 值集合時發生錯誤", tableName, fieldName);
                throw new DbQueryException($"取得資料表 {tableName} 欄位 {fieldName} 值集合時發生錯誤", ex);
            }
            return null;
        }

        public async Task<int> BulkInsertAsync(IEnumerable<IEnumerable<KeyValuePair<string, object?>>> records, string? tableName = null, CancellationToken cancellationToken = default)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return 0;

            int totalInserted = 0;
            
            try
            {
                _validationService?.ValidateTableName(tableName);

                if (_conn.State != ConnectionState.Open) 
                    await _conn.OpenAsync(cancellationToken);

                var recordList = records.ToList();

                // 使用交易確保資料一致性
                using var transaction = _conn.BeginTransaction();
                try
                {
                    foreach (var record in recordList)
                    {
                        string? sql = _sqlProvider.GetSqlForInsert(tableName, record);
                        if (!string.IsNullOrEmpty(sql))
                        {
                            int effectRow = await _conn.ExecuteAsync(sql, transaction: transaction);
                            totalInserted += effectRow;
                        }
                    }
                    
                    transaction.Commit();
                    _logger?.LogInformation("批次新增 {Count} 筆記錄到資料表 {TableName}", totalInserted, tableName);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "批次新增記錄到資料表 {TableName} 時發生錯誤", tableName);
                throw new DbQueryException($"批次新增記錄到資料表 {tableName} 時發生錯誤", ex);
            }

            return totalInserted;
        }

        #endregion

        #region IDisposable 實作

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _conn?.Dispose();
                    _logger?.LogDebug("資料庫連線已釋放");
                }
                _disposed = true;
            }
        }

        ~DataBaseService()
        {
            Dispose(false);
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
            tableResult.ConnectionString = _conn?.ConnectionString;
            tableResult.Fields = GetFieldsByTableName(tableName);

            return tableResult;
        }


        #endregion


    }
}