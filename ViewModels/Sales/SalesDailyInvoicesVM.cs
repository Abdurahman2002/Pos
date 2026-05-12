namespace NewsApp2.ViewModels.Sales
{
    public class SalesDailyInvoiceRowVM
    {
        public Guid InvoiceId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CustomerName { get; set; } = "-";
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public bool IsReturn { get; set; }
        public decimal TotalDinar { get; set; }
    }

    public class SalesDailyInvoicesVM
    {
        public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public List<SalesDailyInvoiceRowVM> Rows { get; set; } = new();

        public int TotalInvoicesCount => Rows.Count;
        public int SalesInvoicesCount => Rows.Count(x => !x.IsReturn);
        public int ReturnInvoicesCount => Rows.Count(x => x.IsReturn);

        public decimal TotalSalesLyd => Rows.Where(x => !x.IsReturn).Sum(x => x.TotalDinar);
        public decimal TotalReturnsLyd => Rows.Where(x => x.IsReturn).Sum(x => Math.Abs(x.TotalDinar));
        public decimal NetSalesLyd => TotalSalesLyd - TotalReturnsLyd;
    }
}
