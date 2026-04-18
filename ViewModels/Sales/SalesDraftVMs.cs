using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Sales
{
    public class SalesDraftLineVM
    {
        [Required]
        public Guid ItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? Qty { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "9999999999999")]
        public decimal? UnitPriceEur { get; set; }
    }

    public class SalesDraftPayloadVM
    {
        [DataType(DataType.Date)]
        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal? EurToDinarRateSnapshot { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public Guid? CustomerId { get; set; }

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(10)]
        public string DiscountType { get; set; } = "Amount";

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal? DiscountValue { get; set; }

        public bool IsReturn { get; set; }

        [MinLength(0)]
        public List<SalesDraftLineVM> Lines { get; set; } = new();
    }

    public class SalesInvoiceDraftSummaryVM
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; }
        public string? CustomerName { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public bool IsReturn { get; set; }
        public int LineCount { get; set; }
        public decimal NetEur { get; set; }
        public decimal NetDinar { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Modified { get; set; }
        public List<string> ItemNames { get; set; } = new();
    }
}