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
using System.Text;
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
    /// <summary>
    /// 抽象資料庫服務類別
    /// 由個別供應商的資料庫來繼承使用。
    /// 個別供應服務物件,提供DbConnection,SqlProvider,TableNameList等基本資料。
    /// 本物件可提供整體資料庫的服務。若在已知資料表的情況下,可以透過DataTableService來提供資料表的服務。
    /// </summary>
    public abstract class DataBaseService : IDbService, IDbServiceAsync, IAdvancedQueryService, IDisposable
    {
        protected DbConnection? _conn;
        protected SqlProviderBase? _sqlProvider;
        protected string[]? _tableNameList;
        protected string? _currentTableName;
        protected readonly ILogger<DataBaseService>? _logger;
        protected readonly IValidationService? _validationService;
        protected readonly IRetryPolicyService? _retryPolicyService;
        protected readonly ITableStructureCacheService? _cacheService;
        protected readonly DbServiceOptions _options;
        private bool _disposed = false;

        /// <summary>
        /// 取得資料庫連線（用於事務管理等進階功能）
        /// </summary>
        /// <returns>資料庫連線</returns>
        public System.Data.Common.DbConnection? GetConnection()
        {
            if (_conn != null && _conn.State != ConnectionState.Open)
            {
                _conn.Open();
            }
            return _conn;
        }

        protected DataBaseService(string connectionString)
        {
            _options = new DbServiceOptions { ConnectionString = connectionString };
        }

        protected DataBaseService(DbServiceOptions options, ILogger<DataBaseService>? logger = null, 
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null,
            ITableStructureCacheService? cacheService = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _validationService = validationService;
            _retryPolicyService = retryPolicyService;
            
            // 如果啟用快取，建立快取服務
            if (options.EnableQueryCache)
            {
                _cacheService = cacheService ?? new TableStructureCacheService(
                    options.CacheExpirationMinutes, 
                    logger != null ? Microsoft.Extensions.Logging.Abstractions.NullLogger<TableStructureCacheService>.Instance : null);
            }
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
        
        /// <summary>
        /// 檢查資料表是否存在
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>如果資料表存在則返回 true，否則返回 false</returns>
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
        /// <summary>
        /// 檢查資料表是否有記錄
        /// </summary>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>如果資料表有記錄則返回 true，否則返回 false</returns>
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

            // 嘗試從快取取得
            if (_cacheService != null && _options.EnableQueryCache)
            {
                var cachedFields = _cacheService.GetCachedFields(tableName);
                if (cachedFields != null)
                {
                    _logger?.LogDebug("從快取取得資料表欄位資訊: {TableName}", tableName);
                    return cachedFields;
                }
            }

            // 從資料庫查詢
            var sql = _sqlProvider.GetSqlFieldsByTableName(tableName);
            var result = string.IsNullOrEmpty(sql) ? null : _conn.Query(sql);
            string? foreignSql = _sqlProvider.GetSqlForeignInfoByTableName(tableName);
            var foreignness = string.IsNullOrEmpty(foreignSql) ? null : _conn.Query(foreignSql);
            
            IEnumerable<FieldBaseModel>? fields = null;
            if (result != null && foreignness != null)
            {
                fields = _sqlProvider.MapToFieldBaseModel(result, foreignness);
                
                // 存入快取
                if (_cacheService != null && _options.EnableQueryCache && fields != null)
                {
                    _cacheService.SetCachedFields(tableName, fields);
                    _logger?.LogDebug("快取資料表欄位資訊: {TableName}", tableName);
                }
            }

            return fields;
        }

        /// <summary>
        /// 取得資料表中某欄位的所有不重複值
        /// </summary>
        /// <param name="fieldName">欄位名稱</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>欄位值的集合，如果查詢失敗則返回空集合</returns>
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

        /// <summary>
        /// 根據欄位定義建立新的資料表
        /// </summary>
        /// <param name="tableDefine">資料表的欄位定義集合</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>受影響的資料列數（通常為 0，因為 CREATE TABLE 不影響資料列）</returns>
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

        /// <summary>
        /// 刪除資料表
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>受影響的資料列數（通常為 0，因為 DROP TABLE 不影響資料列）</returns>
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
        /// <summary>
        /// 根據 ID 取得單筆記錄
        /// </summary>
        /// <param name="id">記錄 ID</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含記錄的 TableBaseModel，如果找不到則返回 null</returns>
        public virtual TableBaseModel? GetRecordById(long id, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || tableName == null) return tableResult;
            try
            {
                _validationService?.ValidateTableName(tableName);
                
                if (_conn.State != ConnectionState.Open) _conn.Open();

                // 優先使用參數化查詢
                var paramResult = _sqlProvider.GetParameterizedSqlById(tableName, id);
                if (paramResult != null)
                {
                    var temp = _conn.Query(paramResult.Sql, paramResult.Parameters);
                    tableResult = MapToTableBaseModel(temp, tableName);
                    _logger?.LogDebug("使用參數化查詢取得記錄: Table={TableName}, Id={Id}", tableName, id);
                }
                else
                {
                    // 降級使用字串拼接（不建議）
                    string? sql = _sqlProvider.GetSqlById(tableName, id);
                    var temp = sql != null ? _conn.Query(sql) : null;
                    tableResult = MapToTableBaseModel(temp, tableName);
                    _logger?.LogWarning("使用字串拼接查詢（不建議）: Table={TableName}, Id={Id}", tableName, id);
                }
            }
            catch (SqlException ex)
            {
                _logger?.LogError(ex, "根據 ID 查詢記錄時發生資料庫錯誤: Table={TableName}, Id={Id}", tableName, id);
                throw new DbServiceException($"查詢記錄失敗: {tableName}, Id={id}", ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogError(ex, "根據 ID 查詢記錄時發生無效操作錯誤: Table={TableName}, Id={Id}", tableName, id);
                throw new DbServiceException($"查詢記錄失敗: {tableName}, Id={id}", ex);
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "根據 ID 查詢記錄時發生未預期的錯誤: Table={TableName}, Id={Id}", tableName, id);
                throw new DbServiceException($"查詢記錄失敗: {tableName}, Id={id}", ex);
            }
            return tableResult;
        }

        /// <summary>
        /// 根據單一鍵值對查詢記錄
        /// </summary>
        /// <param name="query">查詢條件（鍵值對）</param>
        /// <param name="giveOperator">查詢運算子（預設為等於）</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含符合條件記錄的 TableBaseModel，如果找不到則返回 null</returns>
        public virtual TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, EnumQueryOperator? giveOperator = null, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                _validationService?.ValidateTableName(tableName);
                _validationService?.ValidateFieldName(query.Key);

                if (_conn.State != ConnectionState.Open) _conn.Open();

                // 優先使用參數化查詢
                var paramResult = _sqlProvider.GetParameterizedSqlByKeyValue(tableName, query.Key, query.Value, giveOperator);
                if (paramResult != null)
                {
                    var temp = _conn.Query(paramResult.Sql, paramResult.Parameters);
                    tableResult = MapToTableBaseModel(temp, tableName);
                    _logger?.LogDebug("使用參數化查詢取得記錄: Table={TableName}, Key={Key}, Value={Value}", 
                        tableName, query.Key, query.Value);
                }
                else if (query.Value != null)
                {
                    // 降級使用字串拼接（不建議）
                    string? sql = _sqlProvider.GetSqlByKeyValue(tableName, query.Key, query.Value, giveOperator);
                    var temp = sql != null ? _conn.Query(sql) : null;
                    tableResult = MapToTableBaseModel(temp, tableName);
                    _logger?.LogWarning("使用字串拼接查詢（不建議）: Table={TableName}, Key={Key}", tableName, query.Key);
                }
            }
            catch (SqlException ex)
            {
                _logger?.LogError(ex, "根據鍵值查詢記錄時發生資料庫錯誤: Table={TableName}, Key={Key}", tableName, query.Key);
                throw new DbServiceException($"查詢記錄失敗: {tableName}, Key={query.Key}", ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogError(ex, "根據鍵值查詢記錄時發生無效操作錯誤: Table={TableName}, Key={Key}", tableName, query.Key);
                throw new DbServiceException($"查詢記錄失敗: {tableName}, Key={query.Key}", ex);
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "根據鍵值查詢記錄時發生未預期的錯誤: Table={TableName}, Key={Key}", tableName, query.Key);
                throw new DbServiceException($"查詢記錄失敗: {tableName}, Key={query.Key}", ex);
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


        /// <summary>
        /// 取得資料表的所有記錄
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>包含所有記錄的 TableBaseModel，如果查詢失敗則返回 null</returns>
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

        /// <summary>
        /// 根據 WHERE 條件字串查詢記錄
        /// </summary>
        /// <param name="whereStr">WHERE 子句內容（不包含 WHERE 關鍵字）</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含符合條件記錄的 TableBaseModel，如果查詢失敗則返回 null</returns>
        /// <remarks>此方法不建議使用，建議使用參數化查詢方法以避免 SQL 注入風險</remarks>
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

        /// <summary>
        /// 取得特定欄位的資料，可提供 WHERE 條件
        /// </summary>
        /// <param name="fields">要查詢的欄位名稱陣列</param>
        /// <param name="whereStr">WHERE 子句內容（不包含 WHERE 關鍵字）。如果為 null 則查詢所有記錄</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含符合條件記錄的 TableBaseModel，如果查詢失敗則返回 null</returns>
        /// <remarks>此方法不建議使用，建議使用參數化查詢方法以避免 SQL 注入風險</remarks>
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

        /// <summary>
        /// 根據外來鍵取得一對多關係中的「多」方記錄
        /// </summary>
        /// <param name="oneSideId">「一」方記錄的 ID</param>
        /// <param name="fkFieldName">外來鍵欄位名稱</param>
        /// <param name="manySideTableName">「多」方資料表名稱</param>
        /// <param name="oneSideTableName">「一」方資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含「多」方記錄的 TableBaseModel，如果找不到則返回 null</returns>
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
        /// <summary>
        /// 插入新記錄
        /// </summary>
        /// <param name="source">要插入的欄位和值</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>插入後的記錄（包含自動產生的 ID），如果插入失敗則返回 null</returns>
        /// <remarks>此方法會自動取得插入後的記錄 ID，並返回完整的記錄資料</remarks>
        public virtual TableBaseModel? InsertRecord(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;
            try
            {
                if (_conn.State != ConnectionState.Open) _conn.Open();

                // 優先使用參數化查詢
                var paramResult = _sqlProvider.GetParameterizedSqlForInsert(tableName, source);
                int resultId = 0;
                
                if (paramResult != null)
                {
                    resultId = _conn.Execute(paramResult.Sql, paramResult.Parameters);
                    _logger?.LogDebug("使用參數化查詢插入記錄: Table={TableName}, AffectedRows={Rows}", tableName, resultId);
                }
                else
                {
                    // 降級使用字串拼接（不建議）
                    string? sql = _sqlProvider.GetSqlForInsert(tableName, source);
                    resultId = sql != null ? _conn.Execute(sql) : 0;
                    _logger?.LogWarning("使用字串拼接插入（不建議）: Table={TableName}", tableName);
                }
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
                _logger?.LogError(ex, "執行 SQL 插入操作時發生資料庫錯誤: {TableName}", tableName);
                throw new DbServiceException($"插入記錄失敗: {tableName}", ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogError(ex, "執行 SQL 插入操作時發生無效操作錯誤: {TableName}", tableName);
                throw new DbServiceException($"插入記錄失敗: {tableName}", ex);
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "執行 SQL 插入操作時發生未預期的錯誤: {TableName}", tableName);
                throw new DbServiceException($"插入記錄失敗: {tableName}", ex);
            }

            return tableResult;
        }

        /// <summary>
        /// 根據 ID 更新記錄
        /// </summary>
        /// <param name="id">記錄 ID</param>
        /// <param name="source">要更新的欄位和值</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>更新後的記錄，如果更新失敗則返回 null</returns>
        public virtual TableBaseModel? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null)
        {
            TableBaseModel? tableResult = null;
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return tableResult;

            try
            {
                _validationService?.ValidateTableName(tableName);
                
                if (_conn.State != ConnectionState.Open) _conn.Open();

                // 優先使用參數化查詢
                var paramResult = _sqlProvider.GetParameterizedSqlForUpdate(tableName, id, source);
                int effectRow = 0;
                
                if (paramResult != null)
                {
                    effectRow = _conn.Execute(paramResult.Sql, paramResult.Parameters);
                    _logger?.LogDebug("使用參數化查詢更新記錄: Table={TableName}, Id={Id}, AffectedRows={Rows}", 
                        tableName, id, effectRow);
                }
                else
                {
                    // 降級使用字串拼接（不建議）
                    string? sql = _sqlProvider.GetSqlForUpdate(tableName, id, source);
                    effectRow = sql != null ? _conn.Execute(sql) : 0;
                    _logger?.LogWarning("使用字串拼接更新（不建議）: Table={TableName}, Id={Id}", tableName, id);
                }
                
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
                _logger?.LogError(ex, "更新記錄時發生資料庫錯誤: Table={TableName}, Id={Id}", tableName, id);
                throw new DbServiceException($"更新記錄失敗: {tableName}, Id={id}", ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogError(ex, "更新記錄時發生無效操作錯誤: Table={TableName}, Id={Id}", tableName, id);
                throw new DbServiceException($"更新記錄失敗: {tableName}, Id={id}", ex);
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "更新記錄時發生未預期的錯誤: Table={TableName}, Id={Id}", tableName, id);
                throw new DbServiceException($"更新記錄失敗: {tableName}, Id={id}", ex);
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

        #region 進階查詢服務 (IAdvancedQueryService)

        /// <summary>
        /// 根據條件查詢記錄（支援分頁和排序）
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）。如果為 null 則查詢所有記錄</param>
        /// <param name="options">查詢選項（排序、分頁等）。如果為 null 則使用預設選項</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含符合條件記錄的 TableBaseModel</returns>
        public virtual TableBaseModel? GetRecordsWithOptions(
            IEnumerable<KeyValuePair<string, object?>>? query = null,
            QueryOptions? options = null,
            string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return null;

            try
            {
                _validationService?.ValidateTableName(tableName);
                
                if (_conn.State != ConnectionState.Open) _conn.Open();

                // 建立基礎 SQL
                var fields = options?.SelectFields != null && options.SelectFields.Any()
                    ? string.Join(", ", options.SelectFields)
                    : "*";

                var sqlBuilder = new StringBuilder($"SELECT {fields} FROM {tableName}");

                // 加入 WHERE 條件
                object? parameters = null;
                if (query != null && query.Any())
                {
                    var paramResult = _sqlProvider.GetParameterizedSqlByKeyValues(tableName, query);
                    if (paramResult != null)
                    {
                        sqlBuilder.Append(" WHERE ").Append(paramResult.Sql.Replace("SELECT * FROM " + tableName + " WHERE ", ""));
                        parameters = paramResult.Parameters;
                    }
                }

                // 加入 ORDER BY
                if (!string.IsNullOrEmpty(options?.OrderBy))
                {
                    _validationService?.ValidateFieldName(options.OrderBy);
                    var direction = options.OrderByDescending ? "DESC" : "ASC";
                    sqlBuilder.Append($" ORDER BY {options.OrderBy} {direction}");
                }

                // 加入分頁（LIMIT/OFFSET）
                // 注意：不同資料庫的分頁語法不同，這裡使用通用方式
                if (options?.Take.HasValue == true)
                {
                    sqlBuilder.Append($" LIMIT {options.Take.Value}");
                }
                if (options?.Skip.HasValue == true)
                {
                    sqlBuilder.Append($" OFFSET {options.Skip.Value}");
                }

                sqlBuilder.Append(';');
                var sql = sqlBuilder.ToString();

                // 執行查詢
                var temp = parameters != null 
                    ? _conn.Query(sql, parameters)
                    : _conn.Query(sql);
                
                var result = MapToTableBaseModel(temp, tableName);
                _logger?.LogDebug("進階查詢完成: Table={TableName}, RecordCount={Count}", 
                    tableName, result?.Records?.Count() ?? 0);

                return result;
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "進階查詢時發生錯誤: Table={TableName}", tableName);
                throw new DbServiceException($"進階查詢失敗: {tableName}", ex);
            }
        }

        /// <summary>
        /// 查詢記錄總數
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）。如果為 null 則查詢所有記錄的總數</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>記錄總數</returns>
        public virtual long GetRecordCount(IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null)
        {
            tableName ??= _currentTableName;
            if (_conn == null || _sqlProvider == null || string.IsNullOrEmpty(tableName)) return 0;

            try
            {
                _validationService?.ValidateTableName(tableName);
                
                if (_conn.State != ConnectionState.Open) _conn.Open();

                var sqlBuilder = new StringBuilder($"SELECT COUNT(*) as Count FROM {tableName}");

                // 加入 WHERE 條件
                object? parameters = null;
                if (query != null && query.Any())
                {
                    var paramResult = _sqlProvider.GetParameterizedSqlByKeyValues(tableName, query);
                    if (paramResult != null)
                    {
                        var whereClause = paramResult.Sql.Replace("SELECT * FROM " + tableName + " WHERE ", "");
                        sqlBuilder.Append(" WHERE ").Append(whereClause.Replace(";", ""));
                        parameters = paramResult.Parameters;
                    }
                }

                sqlBuilder.Append(';');
                var sql = sqlBuilder.ToString();

                // 執行查詢
                var result = parameters != null
                    ? _conn.QueryFirstOrDefault<dynamic>(sql, parameters)
                    : _conn.QueryFirstOrDefault<dynamic>(sql);

                if (result != null)
                {
                    long count = 0;
                    if (result is IDictionary<string, object> dict)
                    {
                        count = dict.ContainsKey("Count") ? Convert.ToInt64(dict["Count"]) : 0;
                    }
                    else
                    {
                        // 嘗試直接轉換為 long
                        try
                        {
                            count = Convert.ToInt64(result);
                        }
                        catch
                        {
                            // 轉換失敗，返回 0
                            count = 0;
                        }
                    }
                    
                    if (_logger != null)
                    {
                        _logger.LogDebug("查詢記錄總數: Table={TableName}, Count={Count}", tableName, count);
                    }
                    return count;
                }

                return 0;
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "查詢記錄總數時發生錯誤: Table={TableName}", tableName);
                throw new DbServiceException($"查詢記錄總數失敗: {tableName}", ex);
            }
        }

        /// <summary>
        /// 查詢是否存在符合條件的記錄
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）。如果為 null 則檢查資料表是否有任何記錄</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>如果存在則返回 true，否則返回 false</returns>
        public virtual bool Exists(IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null)
        {
            return GetRecordCount(query, tableName) > 0;
        }

        /// <summary>
        /// 根據條件查詢單一記錄（如果有多筆則返回第一筆）
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）。如果為 null 則返回第一筆記錄</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>第一筆符合條件的記錄，如果找不到則返回 null</returns>
        public virtual TableBaseModel? GetFirstRecord(IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null)
        {
            var options = new QueryOptions { Take = 1 };
            return GetRecordsWithOptions(query, options, tableName);
        }

        /// <summary>
        /// 根據條件查詢單一欄位的值
        /// </summary>
        /// <typeparam name="T">欄位值的類型</typeparam>
        /// <param name="fieldName">欄位名稱</param>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）。如果為 null 則查詢第一筆記錄</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>欄位值，如果找不到則返回 default(T)</returns>
        public virtual T? GetFieldValue<T>(string fieldName, IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null)
        {
            if (string.IsNullOrEmpty(fieldName)) return default(T);

            try
            {
                _validationService?.ValidateFieldName(fieldName);
                
                var options = new QueryOptions 
                { 
                    SelectFields = new[] { fieldName },
                    Take = 1 
                };
                
                var result = GetRecordsWithOptions(query, options, tableName);
                
                if (result?.Records?.FirstOrDefault() is RecordBaseModel record)
                {
                    var value = record.GetFieldValue(fieldName);
                    if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }

                return default(T);
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "查詢欄位值時發生錯誤: Table={TableName}, Field={FieldName}", tableName, fieldName);
                throw new DbServiceException($"查詢欄位值失敗: {tableName}, Field={fieldName}", ex);
            }
        }

        /// <summary>
        /// 根據條件查詢多個欄位的值
        /// </summary>
        /// <param name="fieldNames">要查詢的欄位名稱陣列</param>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）。如果為 null 則查詢第一筆記錄</param>
        /// <param name="tableName">資料表名稱（如果為 null 則使用當前設定的資料表）</param>
        /// <returns>包含欄位值的字典，鍵為欄位名稱，值為欄位值。如果找不到則返回 null</returns>
        public virtual Dictionary<string, object?>? GetFieldValues(string[] fieldNames, IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null)
        {
            if (fieldNames == null || !fieldNames.Any()) return null;

            try
            {
                foreach (var fieldName in fieldNames)
                {
                    _validationService?.ValidateFieldName(fieldName);
                }

                var options = new QueryOptions 
                { 
                    SelectFields = fieldNames,
                    Take = 1 
                };
                
                var result = GetRecordsWithOptions(query, options, tableName);
                
                if (result?.Records?.FirstOrDefault() is RecordBaseModel record)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var fieldName in fieldNames)
                    {
                        dict[fieldName] = record.GetFieldValue(fieldName);
                    }
                    return dict;
                }

                return null;
            }
            catch (Exception ex) when (!(ex is DbServiceException))
            {
                _logger?.LogError(ex, "查詢多個欄位值時發生錯誤: Table={TableName}, Fields={Fields}", 
                    tableName, string.Join(", ", fieldNames));
                throw new DbServiceException($"查詢欄位值失敗: {tableName}", ex);
            }
        }

        #endregion

    }
}