
using WebService.Models.EnumModel;

namespace WebService.Models
{
    public class ResultMessage
    {
        public string? Code { get; set; }
        public string? Method { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }
}
