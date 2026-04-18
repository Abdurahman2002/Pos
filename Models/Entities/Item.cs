using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class Item : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(64)]
        public string? Barcode { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        public Category? Category { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Reorder level must be zero or greater")]
        public decimal? ReorderLevel { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Default sale price must be zero or greater")]
        public decimal? DefaultSalePriceLyd { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }
    }
}
