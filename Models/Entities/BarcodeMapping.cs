using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class BarcodeMapping : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string CodeType { get; set; } = "Raw"; // Raw, Gtin

        [Required]
        public Guid ItemId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public Item? Item { get; set; }
    }
}
