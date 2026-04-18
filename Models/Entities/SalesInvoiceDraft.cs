using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class SalesInvoiceDraft : BaseEntity
    {
        [StringLength(120)]
        public string Title { get; set; } = "Held sale";

        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Column(TypeName = "decimal(18,6)")]
        public decimal EurToDinarRateSnapshot { get; set; }

        [StringLength(10)]
        public string DiscountType { get; set; } = "Amount";

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        public bool IsReturn { get; set; }

        public Guid? CustomerId { get; set; }

        [StringLength(256)]
        public string? CustomerNameSnapshot { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubtotalEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetDinar { get; set; }

        public int LineCount { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }
    }
}