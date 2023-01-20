using System.Text.Json.Nodes;

namespace WebService.Models
{
    public class QueryModel
    {
        
        public string? Key { get; set; }
        public string? Value { get; set; }

        public static ValueTask<QueryModel> BindAsync(HttpContext context)
        {
            string? key = context.Request.Query[nameof(Key)];
            string? value = context.Request.Query[nameof(Value)];
            var result = new QueryModel
            {
                Key = key,
                Value = value
            };

            return ValueTask.FromResult(result);
        }

    }
}
