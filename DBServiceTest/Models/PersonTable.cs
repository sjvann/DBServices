

using DbServices.Core.Models;

namespace DBServiceTest.Models
{
    public class PersonTable : TableDefBaseModel
    {
        public string? Name { get; set; }
        public int Age { get; set; }

    }
}
