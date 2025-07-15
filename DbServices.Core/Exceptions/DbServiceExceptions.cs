using System;

namespace DbServices.Core.Exceptions
{
    /// <summary>
    /// 資料庫服務基底例外類別
    /// </summary>
    public class DbServiceException : Exception
    {
        public string? TableName { get; }
        public string? OperationType { get; }

        public DbServiceException(string message) : base(message) { }

        public DbServiceException(string message, Exception innerException) : base(message, innerException) { }

        public DbServiceException(string message, string? tableName, string? operationType) : base(message)
        {
            TableName = tableName;
            OperationType = operationType;
        }

        public DbServiceException(string message, Exception innerException, string? tableName, string? operationType) : base(message, innerException)
        {
            TableName = tableName;
            OperationType = operationType;
        }
    }

    /// <summary>
    /// 連線例外
    /// </summary>
    public class DbConnectionException : DbServiceException
    {
        public DbConnectionException(string message) : base(message) { }
        public DbConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// 查詢例外
    /// </summary>
    public class DbQueryException : DbServiceException
    {
        public string? SqlQuery { get; }

        public DbQueryException(string message, string? sqlQuery = null) : base(message)
        {
            SqlQuery = sqlQuery;
        }

        public DbQueryException(string message, Exception innerException, string? sqlQuery = null) : base(message, innerException)
        {
            SqlQuery = sqlQuery;
        }
    }

    /// <summary>
    /// 資料驗證例外
    /// </summary>
    public class DbValidationException : DbServiceException
    {
        public string? FieldName { get; }

        public DbValidationException(string message, string? fieldName = null) : base(message)
        {
            FieldName = fieldName;
        }
    }

    /// <summary>
    /// 資料表不存在例外
    /// </summary>
    public class TableNotFoundException : DbServiceException
    {
        public TableNotFoundException(string tableName) : base($"資料表 '{tableName}' 不存在", tableName, "TableCheck") { }
    }
}
