using System.ComponentModel.DataAnnotations;
namespace DbServices.Core.Models
{
    /// <summary>
    /// 提供給資料表物件繼承使用。
    /// 習慣資料表物件名稱之後為Table，與資料庫資料表對應
    /// 若是提供應用時，物件繼承前項，物件名稱之後為Model
    /// </summary>
    public abstract class TableDefBaseModel
    {
        protected TableDefBaseModel(string? userName = null)
        {
            CreatedDate ??= DateTime.UtcNow;
            LastModifyDate = DateTime.UtcNow;
            CreatorName = userName;

        }

        [Key]
        [Required]
        public long Id { get; set; }
        [Required]
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastModifyDate { get; set; }
        public string? CreatorName { get; set; }
    }
    public static class TableDefBaseModelExtension
    {
        public static IEnumerable<KeyValuePair<string, object?>> GetKeyValuePairs(this TableDefBaseModel source, bool withKey = false)
        {
            var properties = source.GetType().GetProperties();
            List<KeyValuePair<string, object?>> target = [];
            foreach (var p in properties)
            {
                if (p.Name == "Id" && !withKey) continue;
                target.Add(new(p.Name, p.GetValue(source)));
            }
            return target;
        }
    }
}

