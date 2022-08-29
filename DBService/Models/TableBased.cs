using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models
{
    public abstract class TableBased
    {

        public static string? GetTableName<T>() where T : TableBased
        {

            TableAttribute? ta = (TableAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(TableAttribute));
            return ta?.Name;

        }

        public static IEnumerable<FieldBased> GetFields<T>() where T : TableBased
        {
            List<FieldBased> _fields = new();
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                FieldBased oneField = new();
                Attribute[]? cas = Attribute.GetCustomAttributes(prop);
                if (cas != null && cas.Any())
                {
                    foreach (Attribute ca in cas)
                    {

                        if (ca is ColumnAttribute temp)
                        {
                            oneField.FieldName = temp.Name;
                            oneField.FieldType = temp.TypeName;
                        }
                        if (ca is KeyAttribute)
                        {
                            oneField.IsKey = true;
                        }
                    }
                }
                _fields.Add(oneField);
            }
            return _fields;
        }
        public abstract int GetId();


       

    }
}
