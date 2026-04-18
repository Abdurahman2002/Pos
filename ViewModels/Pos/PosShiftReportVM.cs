namespace NewsApp2.ViewModels.Pos
{
    public class PosShiftReportVM
    {
        public Guid ShiftId { get; set; }
        public string OpenedByUserName { get; set; } = string.Empty;
        public DateTime OpenedAtLocal { get; set; }
        public DateTime? ClosedAtLocal { get; set; }

        public decimal OpeningCashLyd { get; set; }
        public decimal GrossSalesLyd { get; set; }
        public decimal ReturnsLyd { get; set; }
        public decimal NetSalesLyd { get; set; }
        public decimal CashSalesLyd { get; set; }
        public decimal CardSalesLyd { get; set; }
        public decimal TransferSalesLyd { get; set; }
        public decimal CreditSalesLyd { get; set; }
        public decimal ExpectedCashLyd { get; set; }
        public decimal? ActualCashLyd { get; set; }
        public decimal CashDifferenceLyd { get; set; }

        public int InvoiceCount { get; set; }

        public List<PosShiftInvoiceRowVM> Invoices { get; set; } = new();
    }

    public class PosShiftInvoiceRowVM
    {
        public string Number { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; }
        public string CustomerName { get; set; } = "-";
        public decimal TotalLyd { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public bool IsReturn { get; set; }
        public DateTime CreatedLocal { get; set; }
    }

    public class CashierPerformanceRowVM
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = "-";
        public int ShiftCount { get; set; }
        public int SoldInvoiceCount { get; set; }
        public int ReturnInvoiceCount { get; set; }
        public decimal GrossSalesLyd { get; set; }
        public decimal ReturnsLyd { get; set; }
        public decimal NetSalesLyd { get; set; }
        public decimal CashSalesLyd { get; set; }
        public decimal CardSalesLyd { get; set; }
        public decimal TransferSalesLyd { get; set; }
        public decimal CreditSalesLyd { get; set; }
        public decimal CommissionAmountLyd { get; set; }
    }

    public class CashierPerformanceVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string? UserId { get; set; }
        public decimal CommissionPercent { get; set; }
        public List<CashierPerformanceRowVM> Rows { get; set; } = new();

        public int TotalShiftCount { get; set; }
        public int TotalSoldInvoiceCount { get; set; }
        public decimal TotalNetSalesLyd { get; set; }
        public decimal TotalCommissionLyd { get; set; }
    }
}
