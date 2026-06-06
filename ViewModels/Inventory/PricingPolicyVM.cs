using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Inventory
{
    public class ItemSalePriceVM
    {
        [Required]
        public Guid ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string? CategoryName { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal? DefaultSalePriceLyd { get; set; }
    }

    public class PricingPolicyVM
    {
        [Range(typeof(decimal), "0", "100")]
        public decimal MaxCashierDiscountPercent { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999")]
        public decimal CommissionSalesStepLyd { get; set; } = 1000m;

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal CommissionAmountPerStepLyd { get; set; } = 10m;

        [MinLength(1)]
        public List<ItemSalePriceVM> Items { get; set; } = new();
    }
}
