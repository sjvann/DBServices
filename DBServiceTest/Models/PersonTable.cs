
using DBServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBServiceTest.Models
{
    public class PersonTable : TableDefBaseModel
    {

        public string? Name { get; set; }
        public int Age { get; set; }

        public override IEnumerable<KeyValuePair<string, object?>> GetKeyValuePairs(bool withKey = false)
        {
            List<KeyValuePair<string, object?>> target = [
                new("CreatedDate", CreatedDate),
                new("LastModifyDate", LastModifyDate),
                new("CreatorName", CreatorName),
                new("Name", Name),
                new("Age", Age)
                ];
            if (withKey)
            {
                target.Add(new("Id", Id));
            }
            return target;

        }
    }
}
