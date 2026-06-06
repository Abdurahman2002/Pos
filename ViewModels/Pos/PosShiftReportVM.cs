namespace NewsApp2.ViewModels.Pos
{
    public class PosShiftListItemVM
    {
        public Guid ShiftId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public DateTime OpenedAtLocal { get; set; }
        public DateTime? ClosedAtLocal { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal OpeningCashLyd { get; set; }
        public decimal GrossSalesLyd { get; set; }
        public decimal ReturnsLyd { get; set; }
        public decimal NetSalesLyd { get; set; }
        public decimal ExpectedCashLyd { get; set; }
        public decimal? ActualCashLyd { get; set; }
        public decimal CashDifferenceLyd { get; set; }
        public string CashDifferenceText { get; set; } = "-";
        public bool IsDeficit { get; set; }
        public int InvoiceCount { get; set; }
    }

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
        public decimal CashReceiptsLyd { get; set; }
        public decimal CashExpensesLyd { get; set; }
        public decimal TotalExpensesLyd { get; set; }
        public decimal ExpectedCashLyd { get; set; }
        public decimal? ActualCashLyd { get; set; }
        public decimal CashDifferenceLyd { get; set; }

        public int InvoiceCount { get; set; }

        public List<PosShiftInvoiceRowVM> Invoices { get; set; } = new();
        public List<PosShiftExpenseRowVM> Expenses { get; set; } = new();
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

    public class PosShiftExpenseRowVM
    {
        public DateOnly ExpenseDate { get; set; }
        public string ExpenseKind { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }

    public class CashierPerformanceRowVM
    {
        public Guid EmployeeId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = "-";
        public string EmployeeName { get; set; } = "-";
        public bool HasSystemAccount { get; set; }
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
        public decimal BaseSalaryLyd { get; set; }
        public decimal SalaryPaidLyd { get; set; }
        public decimal CommissionWithdrawalsLyd { get; set; }
        public decimal AdvancesLyd { get; set; }
        public decimal DeductionsLyd { get; set; }
        public decimal NetDueLyd { get; set; }
    }

    public class CashierPerformanceVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? UserId { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal CommissionSalesStepLyd { get; set; }
        public decimal CommissionAmountPerStepLyd { get; set; }
        public List<CashierPerformanceRowVM> Rows { get; set; } = new();

        public int TotalShiftCount { get; set; }
        public int TotalSoldInvoiceCount { get; set; }
        public decimal TotalNetSalesLyd { get; set; }
        public decimal TotalBaseSalaryLyd { get; set; }
        public decimal TotalSalaryPaidLyd { get; set; }
        public decimal TotalCommissionLyd { get; set; }
        public decimal TotalNetDueLyd { get; set; }
    }
}
