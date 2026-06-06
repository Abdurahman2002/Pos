using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class InventorySettings : BaseEntity
    {
        public bool IsSingleWarehouseMode { get; set; } = true;

        [Required]
        [StringLength(3)]
        public string BaseCurrencyCode { get; set; } = "EUR";

        [Required]
        [StringLength(20)]
        public string DinarCurrencyCode { get; set; } = "LYD";

        [StringLength(200)]
        public string? SingleWarehouseName { get; set; } = "Main Warehouse";

        [StringLength(50)]
        public string? DefaultRateSource { get; set; } = "Manual";

        [Range(0, 100)]
        public decimal MaxCashierDiscountPercent { get; set; } = 10m;

        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionSalesStepLyd { get; set; } = 1000m;

        [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionAmountPerStepLyd { get; set; } = 10m;
    }
}
