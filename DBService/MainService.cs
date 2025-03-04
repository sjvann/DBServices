using DbServices.Core.Models.Interface;

namespace DBServices
{
    /// <summary>
    /// 起始服務物件。透過此物件來設定資料庫類別。
    /// </summary>
    public static class MainService
    {
        #region 設定資料庫廠商

        public static IDbService UseDataBase(string connectString, Func<string, IDbService> providerService)
        {
            return providerService(connectString);
        }

        /// <summary>
        /// 使用SQLite資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseSQLite(string connectString)
        {
           return  new DbServices.Provider.Sqlite.ProviderService(connectString);
          
        }
        /// <summary>
        /// 使用MsSQL資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseMsSQL(string connectString)
        {
            return new DbServices.Provider.MsSql.ProviderService(connectString);
        }
        /// <summary>
        /// 使用MySQL資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>

        public static IDbService UseMySQL(string connectString)
        {
            return new DbServices.Provider.MySql.ProviderService(connectString);
        }
        /// <summary>
        /// 使用Oracle資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseOracle(string connectString)
        {
            return new DbServices.Provider.Oracle.ProviderService(connectString);
        }
        #endregion

    }
}
