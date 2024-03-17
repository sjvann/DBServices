using DBServices.Models;
using Microsoft.Identity.Client;

namespace DBServiceTest.Models
{
    public class TestTable : TableDefBaseModel
    {
        public string? Description { get; set; }


        public override IEnumerable<KeyValuePair<string, object?>> GetKeyValuePairs(bool withKey = false)
        {
            List<KeyValuePair<string, object?>> target = [

                new("CreatedDate", CreatedDate),
                new("LastModifyDate", LastModifyDate),
                new("CreatorName", CreatorName),
                 new("Description", Description),
                ];
            if (withKey) { target.Add(new("Id", Id)); }

            return target;
        }

    }
}
