namespace NewsApp2.ViewModels.Suppliers
{
    public class SupplierDebtRowVM
    {
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public decimal DebtAmount { get; set; }
        public DateOnly? LastCreditPurchaseDate { get; set; }
        public DateOnly? LastPaymentDate { get; set; }
    }

    public class SupplierDebtReportVM
    {
        public string? Search { get; set; }
        public decimal TotalDebt { get; set; }
        public List<SupplierDebtRowVM> Rows { get; set; } = new();
    }

    public class SupplierStatementEntryVM
    {
        public DateTime At { get; set; }
        public string Reference { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Note { get; set; }
        public decimal RunningBalance { get; set; }
    }

    public class SupplierStatementVM
    {
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<SupplierStatementEntryVM> Entries { get; set; } = new();
    }
}
