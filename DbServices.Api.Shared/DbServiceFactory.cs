using DbServices.Core.Models.Interface;
using DBServices;

namespace DbServices.Api.Shared;

/// <summary>
/// 依連線字串與伺服器類型建立 <see cref="IDbService"/>。
/// </summary>
public static class DbServiceFactory
{
    public static IDbService? Create(string connectString, string serverName)
    {
        return serverName switch
        {
            "Sqlite" => MainService.UseSQLite(connectString),
            "SqlServer" => MainService.UseMsSQL(connectString),
            "MySQL" => MainService.UseMySQL(connectString),
            "Oracle" => MainService.UseOracle(connectString),
            "PostgreSQL" => MainService.UsePostgreSQL(connectString),
            _ => null
        };
    }
}
