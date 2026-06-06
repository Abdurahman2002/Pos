using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Sales
{
    public class SalesEditLineVM
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

    public class SalesEditVM
    {
        [Required]
        public Guid InvoiceId { get; set; }

        [DataType(DataType.Date)]
        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Required]
        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal? EurToDinarRateSnapshot { get; set; }

        public Guid? CustomerId { get; set; }

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";

        public Guid? BankId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [MinLength(1, ErrorMessage = "Add at least one line")]
        public List<SalesEditLineVM> Lines { get; set; } = new();
    }
}
