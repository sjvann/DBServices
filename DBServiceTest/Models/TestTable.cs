using DBServices.Models;
using Microsoft.Identity.Client;

namespace DBServiceTest.Models
{
    public class TestTable : TableDefBaseModel
    {
        public string? Description { get; set; }
    }
}
