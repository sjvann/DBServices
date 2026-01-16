using DbServices.Core.Models.Interface;
using Microsoft.Extensions.Logging;

namespace DbServices.Core.Migration
{
    /// <summary>
    /// 資料庫遷移基類
    /// 所有遷移類別都應該繼承此類別
    /// </summary>
    public abstract class MigrationBase
    {
        /// <summary>
        /// 遷移版本號（應該唯一且遞增）
        /// </summary>
        public abstract long Version { get; }

        /// <summary>
        /// 遷移描述
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// 執行遷移（升級）
        /// </summary>
        /// <param name="dbService">資料庫服務</param>
        /// <param name="logger">日誌記錄器</param>
        public abstract void Up(IDbService dbService, ILogger? logger = null);

        /// <summary>
        /// 回滾遷移（降級）
        /// </summary>
        /// <param name="dbService">資料庫服務</param>
        /// <param name="logger">日誌記錄器</param>
        public abstract void Down(IDbService dbService, ILogger? logger = null);
    }
}

