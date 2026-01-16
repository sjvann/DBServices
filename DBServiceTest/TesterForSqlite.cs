using DbServices.Provider.Sqlite;
using DBServices;
using DbServices.Core.Models.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DBServiceTest
{

    [TestClass]
    public class TesterForSqlite : DataBaseServiceTests
    {
			private readonly string _dbPath;
			private readonly IDbService _db;

			public TesterForSqlite() : this(CreateTempDbPath())
			{
			}

			private TesterForSqlite(string dbPath) : this(dbPath, CreateDbService(dbPath))
			{
			}

			private TesterForSqlite(string dbPath, IDbService db) : base(db)
			{
				_dbPath = dbPath;
				_db = db;
			}

			[TestCleanup]
			public void Cleanup()
			{
				if (_db is IDisposable d) d.Dispose();
				try
				{
					if (File.Exists(_dbPath)) File.Delete(_dbPath);
				}
				catch
				{
					// ignore cleanup failures
				}
			}

			private static string CreateTempDbPath()
			{
				var dir = Path.Combine(Path.GetTempPath(), "DBServicesTests");
				Directory.CreateDirectory(dir);
				return Path.Combine(dir, $"{Guid.NewGuid():N}.db");
			}

			private static IDbService CreateDbService(string dbPath)
			{
				var connString = $"Data Source={dbPath};";
				return MainService.UseDataBase(connString, (cs) => new ProviderService(cs));
			}
    }
}