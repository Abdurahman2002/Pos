using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class Customer : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public ICollection<SalesInvoice>? SalesInvoices { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
