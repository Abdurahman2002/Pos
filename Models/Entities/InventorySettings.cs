using System.ComponentModel.DataAnnotations;

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
    }
}
