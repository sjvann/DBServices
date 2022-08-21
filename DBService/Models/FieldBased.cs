using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models
{
    public class FieldBased
    {
        public string? FieldName { get; set; }
        public string? FieldType { get; set; }  
        public bool IsNull { get; set; }
        public bool IsKey { get; set; }
    }
}
