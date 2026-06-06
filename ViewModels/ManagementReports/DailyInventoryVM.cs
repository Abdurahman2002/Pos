namespace NewsApp2.ViewModels.ManagementReports;

public class DailyInventoryVM
{
    public DateOnly? ReportDate { get; set; }
    public int TotalItems { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public decimal TotalStockValueLyd { get; set; }
    public decimal TotalSaleValueLyd { get; set; }
    public decimal TotalExpectedProfitLyd { get; set; }
    public List<DailyInventoryRow> Rows { get; set; } = new();
}

public class DailyInventoryRow
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCostLyd { get; set; }
    public decimal StockValueLyd { get; set; }
    public decimal? DefaultSalePriceLyd { get; set; }
    public decimal SaleValueLyd { get; set; }
    public decimal ExpectedProfitLyd { get; set; }
    public decimal? ReorderLevel { get; set; }
    public string Status { get; set; } = string.Empty;
}
