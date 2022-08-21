using Dapper;
using DBService.Models;
using DBService.Models.Enum;
using DBService.TableService;
using System.Data;
using System.Data.SqlClient;


namespace DBService
{
    public class DbService
    {
        private readonly SqlConnection _conn;
        private readonly EnumSupportedServer _providerName;

        public DbService(EnumSupportedServer providerName, string? connectString)
        {
            _conn = new SqlConnection(connectString);
            _providerName = providerName;
        }
        #region Public Method
        public SqlConnection GetConnect => _conn;

        public int CheckHasRecord<T>() where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForCheckRows();
                    int affectedRows = (sql != null) ? _conn.Execute(sql) : 0;
                    return affectedRows;
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
            return 0;

        }


        #region CRUD Public Method
        public IEnumerable<T>? GetRecordbyTable<T>() where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForAll();
                    var result = (sql != null) ? _conn.Query<T>(sql) : new List<T>();
                    return result;
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
            return new List<T>();


        }
        public IEnumerable<T>? GetRecords<T>(string[]? fields = null, string? whereStr = null) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = (fields != null && fields.Length > 0) ? _sqlProvider.GetSqlForFields(fields, whereStr) : _sqlProvider.GetSqlForAll(whereStr);
                    var result = (sql != null) ? _conn.Query<T>(sql) : new List<T>();
                    return result;
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
            return new List<T>();
        }


        public T? GetRecord<T>(int id) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlById(id);
                    var result = sql != null ? _conn.QueryFirstOrDefault<T>(sql) : null;
                    return result;
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
            return null;
        }
        public IEnumerable<T>? GetRecordByKey<T>(string key, object value) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlByKey(key, value);
                    var result = sql != null ? _conn.Query<T>(sql) : new List<T>();
                    return result;
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
            return new List<T>();
        }

        public T? AddRecord<T>(T source, int? parentId = null) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null && source != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForInsert(source, parentId);
                    int resultId = sql != null ? _conn.Query<int>(sql, source).SingleOrDefault() : 0;
                    return GetRecord<T>(resultId);
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
            return null;

        }
        public bool AddBulkRecord<T>(IList<T> source) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if (_conn != null && _sqlProvider != null && source != null && source.Any())
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    using var tranc = _conn.BeginTransaction();
                    string? sql = _sqlProvider.GetSqlForBlukAdd();
                    int effectRow = sql != null ? _conn.Execute(sql, source, tranc) : 0;
                    tranc.Commit();
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
            }
            return false;

        }
        public bool DeleteRecord<T>(int id) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
             if(_conn != null && _sqlProvider != null && id != 0)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForDelete(id);
                    int effectRow = sql != null ?  _conn.Execute(sql) : 0;
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
            }
          
            return false;
        }
        public bool DeleteRecordByKey<T>(string key, int id) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if(_conn != null && _sqlProvider != null && id !=0)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForDeleteByKey(key, id);
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
            }
            return false;
        }
        public bool UpdateRecord<T>(int id, T source) where T : TableBased
        {
             var _sqlProvider = SetupSqlProvider<T>();
            if(_conn != null && _sqlProvider != null && id != 0 && source != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForUpdate(id, source);
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
            }

            return false;
        }
        public bool UpdateRecordByKey<T>(string key, int id, T source) where T : TableBased
        {
            var _sqlProvider = SetupSqlProvider<T>();
            if(_conn != null && _sqlProvider != null && id != 0 && source != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForUpdateByKey(key, id, source);
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
            }
            return false;
        }
        

        #endregion


        #endregion

        #region Private Method
        public ITableSqlService<TTable>? SetupSqlProvider<TTable>() where TTable : TableBased
        {
            switch (_providerName)
            {
                case EnumSupportedServer.SqlServer:
                    return new TableSqlForMsSql<TTable>();
                case EnumSupportedServer.Sqlite:
                    return new TableSqlForSqlite<TTable>();
                default:
                    return null;
            }
        }
        #endregion

    }
}
