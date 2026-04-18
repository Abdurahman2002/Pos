using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class PurchaseInvoice : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Number { get; set; } = string.Empty;

        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Column(TypeName = "decimal(18,6)")]
        public decimal EurToDinarRateSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDinar { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Posted";

        [StringLength(500)]
        public string? Note { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }

        public ICollection<PurchaseLine>? Lines { get; set; }
    }
}
