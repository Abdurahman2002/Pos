using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Purchasing
{
    public class PurchaseLineInputVM
    {
        [Required]
        public Guid ItemId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int? Qty { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "9999999999999")]
        public decimal? UnitPriceEur { get; set; }

        [Range(typeof(decimal), "0", "9999999999999")]
        public decimal? SellPriceLyd { get; set; }
    }

    public class PurchaseCreateVM
    {
        [DataType(DataType.Date)]
        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Required]
        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal? EurToDinarRateSnapshot { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Required(ErrorMessage = "حدد المورد لفاتورة المشتريات.")]
        public Guid? SupplierId { get; set; }

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(20)]
        public string DiscountType { get; set; } = "Amount";

        [Range(typeof(decimal), "0", "9999999999999")]
        public decimal DiscountValue { get; set; }

        public Guid? BankId { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? DueDate { get; set; }

        [MinLength(1, ErrorMessage = "Add at least one line")]
        public List<PurchaseLineInputVM> Lines { get; set; } = new();
    }
}
