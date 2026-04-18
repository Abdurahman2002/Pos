namespace NewsApp2.ViewModels.Inventory
{
    public class ItemLabelPrintVM
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string SelectedCode { get; set; } = string.Empty;
        public List<string> AvailableCodes { get; set; } = new();
        public decimal? Price { get; set; }
        public int Copies { get; set; } = 1;
        public string? ShopName { get; set; }
        public string? ShopLogoUrl { get; set; }
    }
}
