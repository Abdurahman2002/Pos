namespace NewsApp2.ViewModels.ManagementReports;

public class StockVM
{
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public List<StockRow> Rows { get; set; } = new();
}

public class StockRow
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCostLyd { get; set; }
    public decimal StockValueLyd { get; set; }
    public decimal SaleValueLyd { get; set; }
    public decimal ExpectedProfitLyd { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? DefaultSalePriceLyd { get; set; }
    public string Status { get; set; } = string.Empty;
}
