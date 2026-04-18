namespace NewsApp2.ViewModels.Sales
{
    public class SalesReportRowVM
    {
        public string Number { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal EurToSecondaryRate { get; set; }
        public decimal TotalEur { get; set; }
        public decimal TotalDinar { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public bool IsReturn { get; set; }
    }

    public class SalesReportVM
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public Guid? CustomerId { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal SumEur { get; set; }
        public decimal SumDinar { get; set; }
        public decimal CashSalesLyd { get; set; }
        public decimal CardSalesLyd { get; set; }
        public decimal TransferSalesLyd { get; set; }
        public decimal CreditSalesLyd { get; set; }
        public decimal ReturnsLyd { get; set; }
        public decimal NetSalesLyd { get; set; }
        public List<SalesReportRowVM> Rows { get; set; } = new();
    }
}
