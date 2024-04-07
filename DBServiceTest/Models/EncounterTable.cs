
using DbServices.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace DBServiceTest.Models
{
    public class EncounterTable : TableDefBaseModel
    {
        [ForeignKey("PersonTable")]
        public long PersonFK { get; set; }
        public string? EncounterType { get; set; }

    }
}
