using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models.Interface
{
    public interface ISupportedDb
    {
        MainService UseMsSQL();
        MainService UseSQLite();
    }
}
