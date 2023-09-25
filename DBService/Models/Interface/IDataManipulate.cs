using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface IDataManipulate
    {
        string? GetSqlForDelete(long id);
        string? GetSqlForDeleteByKey(KeyValuePair<string, object?> criteria);

        string? GetSqlForInsert(IEnumerable<KeyValuePair<string, object?>> source);
        string? GetSqlForUpdate(long id, IEnumerable<KeyValuePair<string, object?>> source);
        string? GetSqlForUpdateByKey(KeyValuePair<string, object?> criteria, IEnumerable<KeyValuePair<string, object?>> source);
        #region Multi Version

        #endregion
    }
}
