using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class PurchaseLine : BaseEntity
    {
        [Required]
        public Guid PurchaseInvoiceId { get; set; }

        [Required]
        public Guid ItemId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Qty { get; set; }

        public int LineOrder { get; set; }

        // Four decimals: the unit cost is often a line total divided by the quantity
        // (50 / 12 = 4.1666...), and two decimals would drift the line total.
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPriceEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalDinar { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAllocatedEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAllocatedDinar { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCostLyd { get; set; }

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRateSnapshot { get; set; }

        public PurchaseInvoice? PurchaseInvoice { get; set; }
        public Item? Item { get; set; }
    }
}
