namespace NewsApp2.ViewModels.Customers
{
    public class CustomerDebtRowVM
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public decimal DebtAmount { get; set; }
        public DateOnly? LastCreditSaleDate { get; set; }
        public DateOnly? LastPaymentDate { get; set; }
    }

    public class CustomerDebtReportVM
    {
        public string? Search { get; set; }
        public decimal TotalDebt { get; set; }
        public List<CustomerDebtRowVM> Rows { get; set; } = new();
    }

    public class CustomerDebtPaymentInputVM
    {
        public Guid CustomerId { get; set; }
        public DateOnly ReceiptDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? Note { get; set; }
    }

    public class CustomerStatementRowVM
    {
        public Guid? ReceiptId { get; set; }
        public bool IsLockedByClosedShift { get; set; }
        public DateTime CreatedAtLocal { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public string? Note { get; set; }
    }

    public class CustomerReceiptEditVM
    {
        public Guid ReceiptId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateOnly ReceiptDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? Note { get; set; }
        public bool IsLockedByClosedShift { get; set; }
    }

    public class CustomerStatementVM
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }

        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }

        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal ClosingBalance { get; set; }

        public CustomerDebtPaymentInputVM PaymentInput { get; set; } = new();
        public List<CustomerStatementRowVM> Rows { get; set; } = new();
    }
}
