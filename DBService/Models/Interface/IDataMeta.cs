using DBService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDataMeta
    {
        string GetSqlFieldsByName(string tableName);
        string GetSqlTableNameList(bool includeView = true);
    }
}
