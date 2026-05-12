using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class SalesLine : BaseEntity
    {
        [Required]
        public Guid SalesInvoiceId { get; set; }

        [Required]
        public Guid ItemId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Qty { get; set; }

        public int LineOrder { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPriceEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotalDinar { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCostLyd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineCostDinar { get; set; }

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRateSnapshot { get; set; }

        public SalesInvoice? SalesInvoice { get; set; }
        public Item? Item { get; set; }
    }
}
