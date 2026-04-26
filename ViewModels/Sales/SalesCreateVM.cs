using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Sales
{
    public class SalesLineInputVM
    {
        [Required]
        public Guid ItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? Qty { get; set; }

        [Range(typeof(decimal), "0", "9999999999999")]
        public decimal? UnitPriceEur { get; set; }
    }

    public class SalesCreateVM
    {
        public Guid? DraftId { get; set; }

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

        public bool IsOnAccount { get; set; }

        [StringLength(10)]
        public string DiscountType { get; set; } = "Amount";

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal? DiscountValue { get; set; }

        public bool IsReturn { get; set; }

        public bool AutoPrintReceipt { get; set; } = true;

        [MinLength(1, ErrorMessage = "Add at least one line")]
        public List<SalesLineInputVM> Lines { get; set; } = new();
    }

    public class SalesApproveLineVM
    {
        [Required]
        public Guid LineId { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "9999999999999")]
        public decimal UnitPriceEur { get; set; }
    }

    public class SalesApproveVM
    {
        [Required]
        public Guid InvoiceId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal EurToDinarRateSnapshot { get; set; }

        [MinLength(1)]
        public List<SalesApproveLineVM> LinePrices { get; set; } = new();
    }
}
