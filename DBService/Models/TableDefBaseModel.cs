using System.ComponentModel.DataAnnotations;

namespace DBService.Models
{
    /// <summary>
    /// 提供給資料表物件繼承使用。
    /// 習慣資料表物件名稱之後為Table，與資料庫資料表對應
    /// 若是提供應用時，物件繼承前項，物件名稱之後為Model
    /// </summary>
    public abstract class TableDefBaseModel
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModifyDate { get; set; }
        public string? CreatorName { get; set; }

    }
}
