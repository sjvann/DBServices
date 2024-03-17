using DBServices.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace DBServiceTest.Models
{
    public class EncounterTable : TableDefBaseModel
    {
        [ForeignKey("PersonTable")]
        public long PersonFK { get; set; }
        public string? EncounterType { get; set; }

        public override IEnumerable<KeyValuePair<string, object?>> GetKeyValuePairs(bool withKey = false)
        {
            List<KeyValuePair<string, object?>> target = [
                new("CreatedDate", CreatedDate),
                new("LastModifyDate", LastModifyDate),
                new("CreatorName", CreatorName),
                new("PersonFK", PersonFK),
                new("EncounterType", EncounterType)
                ];
           if(withKey) { target.Add(new("Id", Id)); }
           return target;
        }
    }
}
