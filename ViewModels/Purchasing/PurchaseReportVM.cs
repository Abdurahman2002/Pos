namespace NewsApp2.ViewModels.Purchasing
{
    public class PurchaseReportRowVM
    {
        public string Number { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal EurToSecondaryRate { get; set; }
        public decimal TotalEur { get; set; }
        public decimal TotalDinar { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? BankName { get; set; }
    }

    public class PurchaseReportVM
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public decimal SumEur { get; set; }
        public decimal SumDinar { get; set; }
        public List<PurchaseReportRowVM> Rows { get; set; } = new();
    }
}
