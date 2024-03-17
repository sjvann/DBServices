
using DBServices.Models.Interface;

namespace DBServices
{
    /// <summary>
    /// 起始服務物件。透過此物件來設定資料庫類別。
    /// </summary>
    public static class MainService
    {
        #region 設定資料庫廠商
        /// <summary>
        /// 使用SQLite資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseSQLite(string connectString)
        {
            return new SqliteService(connectString);
        }
        /// <summary>
        /// 使用MsSQL資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseMsSQL(string connectString)
        {
            return new MsSqlService(connectString);

        }
        /// <summary>
        /// 使用MySQL資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>

        public static IDbService UseMySQL(string connectString)
        {
            return new MySqlService(connectString);
        }
        /// <summary>
        /// 使用Oracle資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseOracle(string connectString)
        {
           return new OracleService(connectString);
        }
        #endregion

    }
}
