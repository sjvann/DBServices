using DBService.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace DBServiceTest.Model
{
    [Table("Demo")]
    public class Demo: TableBased
    {
        [Column("Id", TypeName = "INTEGER")]
        public int? Id { get; set; }
        [Column("Name", TypeName = "TEXT")]
        public string? Name { get; set; }
        [Column("LoginTimes", TypeName = "INTEGER")]
        public int? LoginTimes { get; set; }
        [Column("BirthOfDay", TypeName = "TEXT")]
        public string? BirthOfDay { get; set; }

        public override int GetId()
        {
            return Id ?? 0;
        }
    }
}
