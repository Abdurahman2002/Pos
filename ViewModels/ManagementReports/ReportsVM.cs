namespace NewsApp2.ViewModels.ManagementReports;

public class ReportsVM
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSalesLyd { get; set; }
    public decimal TotalReturnsLyd { get; set; }
    public decimal NetSalesLyd { get; set; }
    public decimal TotalPurchasesLyd { get; set; }
    public decimal CostOfGoodsSoldLyd { get; set; }
    public decimal GrossProfitLyd { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal TotalExpensesLyd { get; set; }
    public decimal NetProfitLyd { get; set; }
    public decimal CashSalesLyd { get; set; }
    public decimal CardSalesLyd { get; set; }
    public decimal TransferSalesLyd { get; set; }
    public decimal CreditSalesLyd { get; set; }
    public int SalesCount { get; set; }
    public int ReturnCount { get; set; }
    public int TotalInvoices { get; set; }
    public decimal StockValueLyd { get; set; }
    public decimal CustomerDebtsLyd { get; set; }
    public decimal SupplierDebtsLyd { get; set; }
    public List<TopItemRow> TopItems { get; set; } = new();
    public List<ExpenseCategoryRow> ExpenseBreakdown { get; set; } = new();
}

public class TopItemRow
{
    public string ItemName { get; set; } = string.Empty;
    public decimal TotalQty { get; set; }
    public decimal TotalRevenueLyd { get; set; }
    public decimal TotalCostLyd { get; set; }
    public decimal GrossProfitLyd { get; set; }
}

public class ExpenseCategoryRow
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
