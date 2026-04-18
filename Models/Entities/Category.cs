using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class Category : BaseEntity
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        public ICollection<Item>? Items { get; set; }
    }
}
