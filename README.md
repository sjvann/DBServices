# DBServices
資料庫存取工具


本專案是基於Dapper的資料庫存取工具，提供了一個簡單的資料庫存取介面，讓開發者可以快速的存取資料庫。
目前支援SQLite、SQL Server、MySQL、Oracle等資料庫。原則上，使用者可以自行擴充其他資料庫，主要就是針對資料庫本身特殊的SQL語法部分進行增補。

另外本專案也提供一個Web Service，讓使用者可以透過Web API的方式存取資料庫，這樣可以讓前端程式直接存取資料庫，而不需要透過後端程式來存取資料庫。


## 連接資料庫
在已知資料庫連線字串的前提下，提供兩種方法連接資料庫：

(1) UseDataBase:

	只要using DbServices.Provider.XXX;不同資料庫即可。
	 UseDataBase(connString,
			(connString) =>
			{
				return new ProviderService(connString);
			}))

(2) UseXXX:
	指定要連接的資料庫。若一個專案同時要連結數個資料庫，建議用此方法。
