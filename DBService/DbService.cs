using Dapper;
using DBService.Models;
using DBService.Models.Enum;
using DBService.TableService;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;

namespace DBService
{
    public class DbService<T> where T : TableBased
    {
        private DbConnection? _conn;
        private readonly string? _connectString;
        private readonly EnumSupportedServer _providerName;

        public DbService(EnumSupportedServer providerName, string connectString)
        {
            _providerName = providerName;
            _connectString = connectString;
        }
        #region Public Method


        public int CheckHasRecord()
        {
            var _sqlProvider = SetupSqlProvider();
            if (_conn != null && _sqlProvider != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForCheckRows();
                    var affectedRows = _conn.Query<int>(sql);
                    return affectedRows.Count();
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
        public IEnumerable<T>? GetRecordbyTable()
        {
            var _sqlProvider = SetupSqlProvider();
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
        public IEnumerable<T>? GetRecords(string[]? fields = null, string? whereStr = null)
        {
            var _sqlProvider = SetupSqlProvider();
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


        public T? GetRecord(int id)
        {
            var _sqlProvider = SetupSqlProvider();
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
        public IEnumerable<T>? GetRecordByKey(string key, object value)
        {
            var _sqlProvider = SetupSqlProvider();
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

        public object? AddRecord(T source, int? parentId = null)
        {
            var _sqlProvider = SetupSqlProvider();
            if (_conn != null && _sqlProvider != null && source != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForInsert(source, parentId);
                    int resultId = sql != null ? _conn.Execute(sql) : 0;
                    if (resultId != 0)
                    {
                        string? sql2 = _sqlProvider.GetSqlLastInsertId();
                       return string.IsNullOrEmpty(sql2) ? 0 : _conn.Query<int>(sql2).FirstOrDefault();
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
            return null;

        }
        public bool AddBulkRecord(IList<T> source)
        {
            var _sqlProvider = SetupSqlProvider();
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
        public bool DeleteRecord(int id)
        {
            var _sqlProvider = SetupSqlProvider();
            if (_conn != null && _sqlProvider != null && id != 0)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForDelete(id);
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
        public bool DeleteRecordByKey(string key, object id)
        {
            var _sqlProvider = SetupSqlProvider();
            if (_conn != null && _sqlProvider != null && id != null)
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
        public T? UpdateRecord(int id, T source)
        {
            var _sqlProvider = SetupSqlProvider();
            if (_conn != null && _sqlProvider != null && id != 0 && source != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForUpdate(id, source);
                    int effectRow = sql != null ? _conn.Execute(sql) : 0;
                    if (effectRow > 0)
                    {
                        return GetRecord(id);
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

            return null;
        }
        public IEnumerable<T>? UpdateRecordByKey(string key, object value, T source)
        {

            var _sqlProvider = SetupSqlProvider();
            if (_conn != null && _sqlProvider != null && value != null && source != null)
            {
                try
                {
                    if (_conn.State != ConnectionState.Open) _conn.Open();
                    string? sql = _sqlProvider.GetSqlForUpdateByKey(key, value, source);
                    int effectRow = sql != null ? _conn.Execute(sql) : 0;
                    if (effectRow > 0)
                    {

                        return GetRecordByKey(key, (int)value);
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
            return new List<T>();
        }


        #endregion


        #endregion

        #region Private Method
        private ITableSqlService<T>? SetupSqlProvider()
        {
            switch (_providerName)
            {
                case EnumSupportedServer.SqlServer:
                    _conn = new SqlConnection(_connectString);
                    return new TableSqlForMsSql<T>();
                case EnumSupportedServer.Sqlite:
                    _conn = new SQLiteConnection(_connectString);
                    return new TableSqlForSqlite<T>();
                default:
                    return null;
            }
        }
        #endregion

    }
}
