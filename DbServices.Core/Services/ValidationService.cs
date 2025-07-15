using DbServices.Core.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DbServices.Core.Services
{
    /// <summary>
    /// 輸入驗證服務介面
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// 驗證資料表名稱
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns></returns>
        bool ValidateTableName(string tableName);

        /// <summary>
        /// 驗證欄位名稱
        /// </summary>
        /// <param name="fieldName">欄位名稱</param>
        /// <returns></returns>
        bool ValidateFieldName(string fieldName);

        /// <summary>
        /// 驗證 SQL 查詢字串（基本檢查）
        /// </summary>
        /// <param name="whereClause">WHERE 子句</param>
        /// <returns></returns>
        bool ValidateWhereClause(string whereClause);

        /// <summary>
        /// 驗證物件屬性
        /// </summary>
        /// <param name="obj">要驗證的物件</param>
        /// <returns></returns>
        ValidationResult ValidateObject(object obj);
    }

    /// <summary>
    /// 輸入驗證服務實作
    /// </summary>
    public class ValidationService : IValidationService
    {
        private static readonly Regex TableNameRegex = new(@"^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex FieldNameRegex = new(@"^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex SqlInjectionRegex = new(@"('|(;|--|\*|\||<|>))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool ValidateTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new DbValidationException("資料表名稱不能為空");

            if (tableName.Length > 128)
                throw new DbValidationException("資料表名稱長度不能超過128個字元");

            if (!TableNameRegex.IsMatch(tableName))
                throw new DbValidationException("資料表名稱格式不正確，只能包含字母、數字和底線，且必須以字母開頭");

            return true;
        }

        public bool ValidateFieldName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new DbValidationException("欄位名稱不能為空", fieldName);

            if (fieldName.Length > 128)
                throw new DbValidationException("欄位名稱長度不能超過128個字元", fieldName);

            if (!FieldNameRegex.IsMatch(fieldName))
                throw new DbValidationException("欄位名稱格式不正確，只能包含字母、數字和底線，且必須以字母開頭", fieldName);

            return true;
        }

        public bool ValidateWhereClause(string whereClause)
        {
            if (string.IsNullOrWhiteSpace(whereClause))
                return true;

            // 基本的 SQL 注入檢查
            if (SqlInjectionRegex.IsMatch(whereClause))
                throw new DbValidationException("WHERE 子句包含可能的 SQL 注入字元");

            return true;
        }

        public ValidationResult ValidateObject(object obj)
        {
            var context = new ValidationContext(obj);
            var results = new List<ValidationResult>();
            
            bool isValid = Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
            
            if (!isValid)
            {
                var errorMessages = results.Select(r => r.ErrorMessage).ToList();
                return new ValidationResult(string.Join("; ", errorMessages));
            }

            return ValidationResult.Success!;
        }
    }
}
