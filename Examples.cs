// DBServices 使用範例
// 展示各種使用模式和功能

using DBServices;
using DbServices.Core.Configuration;
using DbServices.Core.Models.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DBServices.Examples
{
    /// <summary>
    /// DBServices 基本使用範例
    /// </summary>
    public class BasicUsageExamples
    {
        /// <summary>
        /// 1. 最簡單的使用方式
        /// </summary>
        public static async Task SimpleUsageExample()
        {
            // 直接建立 SQLite 連線
            var db = MainService.UseSQLite("Data Source=example.db");
            
            // 取得所有資料表
            var tables = await db.GetAllTableNamesAsync();
            Console.WriteLine($"找到 {tables?.Length} 個資料表");
            
            // 取得資料表欄位
            if (tables?.Length > 0)
            {
                var fields = await db.GetFieldsByTableNameAsync(tables[0]);
                Console.WriteLine($"資料表 {tables[0]} 有 {fields?.Count()} 個欄位");
            }
        }

        /// <summary>
        /// 2. 多資料庫支援範例
        /// </summary>
        public static async Task MultiDatabaseExample()
        {
            // SQLite
            var sqliteDb = MainService.UseSQLite("Data Source=sqlite.db");
            
            // SQL Server
            var sqlServerDb = MainService.UseMsSQL(
                "Server=localhost;Database=TestDB;Trusted_Connection=true");
            
            // MySQL
            var mysqlDb = MainService.UseMySQL(
                "Server=localhost;Database=TestDB;Uid=root;Pwd=password");
            
            // Oracle
            var oracleDb = MainService.UseOracle(
                "Data Source=localhost:1521/XE;User Id=hr;Password=password");

            // 測試連線
            var sqliteTables = await sqliteDb.GetAllTableNamesAsync();
            Console.WriteLine($"SQLite 資料表數量: {sqliteTables?.Length}");
        }

        /// <summary>
        /// 3. 建構器模式範例
        /// </summary>
        public static async Task BuilderPatternExample()
        {
            // 使用建構器建立服務
            var db = MainService.CreateBuilder("Data Source=builder.db")
                .UseSQLite()
                .Build();

            // 測試連線並建立服務
            var testDb = await MainService.CreateAndTestAsync(
                "Data Source=test.db", 
                DatabaseProvider.SQLite);

            Console.WriteLine("建構器模式建立成功");
        }
    }

    /// <summary>
    /// 進階功能範例
    /// </summary>
    public class AdvancedUsageExamples
    {
        /// <summary>
        /// 1. 依賴注入範例
        /// </summary>
        public static async Task DependencyInjectionExample()
        {
            // 建立服務容器
            var services = new ServiceCollection();
            
            // 註冊 DBServices
            services.AddDbServices(options =>
            {
                options.ConnectionString = "Data Source=di.db";
                options.Provider = DatabaseProvider.SQLite;
                options.EnableLogging = true;
                options.EnableValidation = true;
                options.MaxRetryCount = 3;
            });

            // 建立服務提供者
            var serviceProvider = services.BuildServiceProvider();
            
            // 取得資料庫服務
            var dbService = serviceProvider.GetRequiredService<IDbService>();
            
            var tables = await dbService.GetAllTableNamesAsync();
            Console.WriteLine($"DI 模式取得 {tables?.Length} 個資料表");
        }

        /// <summary>
        /// 2. 進階設定範例
        /// </summary>
        public static async Task AdvancedConfigurationExample()
        {
            var options = new DbServiceOptions
            {
                ConnectionString = "Data Source=advanced.db",
                EnableLogging = true,
                EnableValidation = true,
                EnableCache = true,
                CacheExpirationMinutes = 10,
                MaxRetryCount = 5,
                RetryDelaySeconds = 2,
                CommandTimeoutSeconds = 30
            };

            // 建立服務提供者（用於日誌等服務）
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            var serviceProvider = services.BuildServiceProvider();

            var db = MainService.UseSQLite(options, serviceProvider);
            
            Console.WriteLine("進階設定建立成功");
        }
    }

    /// <summary>
    /// CRUD 操作範例
    /// </summary>
    public class CrudOperationsExamples
    {
        private static IDbService? _db;

        static CrudOperationsExamples()
        {
            _db = MainService.UseSQLite("Data Source=crud.db");
        }

        /// <summary>
        /// 1. 查詢操作範例
        /// </summary>
        public static async Task QueryExamples()
        {
            if (_db == null) return;

            // 按 ID 查詢
            var record = await _db.GetRecordByIdAsync(1, "Users");
            
            // 條件查詢
            var users = await _db.GetRecordsByQueryAsync(
                new[] { new KeyValuePair<string, object>("Age", 25) }, 
                "Users");

            // 檢查是否有資料
            bool hasData = await _db.HasRecordAsync("Users");
            
            Console.WriteLine($"查詢完成，找到 {users?.Count()} 筆記錄");
        }

        /// <summary>
        /// 2. 新增操作範例
        /// </summary>
        public static async Task InsertExamples()
        {
            if (_db == null) return;

            // 新增單筆記錄
            var newUser = await _db.InsertRecordAsync(
                new[] 
                { 
                    new KeyValuePair<string, object>("Name", "John Doe"),
                    new KeyValuePair<string, object>("Age", 30),
                    new KeyValuePair<string, object>("Email", "john@example.com")
                }, 
                "Users");

            Console.WriteLine($"新增記錄成功，ID: {newUser?.Records?.FirstOrDefault()?.Id}");
        }

        /// <summary>
        /// 3. 更新操作範例
        /// </summary>
        public static async Task UpdateExamples()
        {
            if (_db == null) return;

            // 按 ID 更新
            var updated = await _db.UpdateRecordByIdAsync(1,
                new[] 
                { 
                    new KeyValuePair<string, object>("Age", 31),
                    new KeyValuePair<string, object>("Email", "john.doe@example.com")
                },
                "Users");

            Console.WriteLine($"更新記錄成功: {updated != null}");
        }

        /// <summary>
        /// 4. 刪除操作範例
        /// </summary>
        public static async Task DeleteExamples()
        {
            if (_db == null) return;

            // 按 ID 刪除
            bool deleted = await _db.DeleteRecordByIdAsync(1, "Users");
            
            Console.WriteLine($"刪除記錄成功: {deleted}");
        }

        /// <summary>
        /// 5. 批次操作範例
        /// </summary>
        public static async Task BulkOperationsExample()
        {
            if (_db == null) return;

            // 準備批次資料
            var records = new List<IEnumerable<KeyValuePair<string, object>>>
            {
                new[] 
                { 
                    new KeyValuePair<string, object>("Name", "User1"),
                    new KeyValuePair<string, object>("Age", 25)
                },
                new[] 
                { 
                    new KeyValuePair<string, object>("Name", "User2"),
                    new KeyValuePair<string, object>("Age", 30)
                },
                new[] 
                { 
                    new KeyValuePair<string, object>("Name", "User3"),
                    new KeyValuePair<string, object>("Age", 35)
                }
            };

            // 批次新增
            int inserted = await _db.BulkInsertAsync(records, "Users");
            
            Console.WriteLine($"批次新增 {inserted} 筆記錄");
        }
    }

    /// <summary>
    /// 主程式範例
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== DBServices 使用範例 ===");

            try
            {
                // 基本使用範例
                Console.WriteLine("\n1. 基本使用範例");
                await BasicUsageExamples.SimpleUsageExample();

                Console.WriteLine("\n2. 多資料庫支援範例");
                await BasicUsageExamples.MultiDatabaseExample();

                Console.WriteLine("\n3. 建構器模式範例");
                await BasicUsageExamples.BuilderPatternExample();

                // 進階功能範例
                Console.WriteLine("\n4. 依賴注入範例");
                await AdvancedUsageExamples.DependencyInjectionExample();

                Console.WriteLine("\n5. 進階設定範例");
                await AdvancedUsageExamples.AdvancedConfigurationExample();

                // CRUD 操作範例
                Console.WriteLine("\n6. 查詢操作範例");
                await CrudOperationsExamples.QueryExamples();

                Console.WriteLine("\n7. 新增操作範例");
                await CrudOperationsExamples.InsertExamples();

                Console.WriteLine("\n8. 更新操作範例");
                await CrudOperationsExamples.UpdateExamples();

                Console.WriteLine("\n9. 刪除操作範例");
                await CrudOperationsExamples.DeleteExamples();

                Console.WriteLine("\n10. 批次操作範例");
                await CrudOperationsExamples.BulkOperationsExample();

                Console.WriteLine("\n=== 所有範例執行完成 ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"執行範例時發生錯誤: {ex.Message}");
                Console.WriteLine($"詳細錯誤: {ex}");
            }

            Console.WriteLine("\n按任意鍵結束...");
            Console.ReadKey();
        }
    }
}
