using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class Warehouse : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
