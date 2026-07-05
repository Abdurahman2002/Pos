namespace NewsApp2.ViewModels.Sales
{
    public class SalesDetailedReportVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public int InvoiceCount { get; set; }
        public decimal GrossSalesLyd { get; set; }
        public decimal ReturnsLyd { get; set; }
        public decimal NetSalesLyd { get; set; }
        public decimal CashSalesLyd { get; set; }
        public decimal CardSalesLyd { get; set; }
        public decimal TransferSalesLyd { get; set; }
        public decimal CreditSalesLyd { get; set; }
        public decimal TotalCogsLyd { get; set; }
        public decimal GrossProfitLyd { get; set; }
        public decimal GrossProfitPercent { get; set; }
        public decimal GeneralExpensesLyd { get; set; }
        public decimal SalariesLyd { get; set; }
        public decimal AdvancesLyd { get; set; }
        public decimal TotalExpensesLyd { get; set; }
        public decimal NetProfitLyd { get; set; }
        public decimal NetProfitPercent { get; set; }
    }

    public class SalesSimpleReportVM
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public List<SalesSimpleReportItemVM> Items { get; set; } = new();
        public List<SalesSimpleReportExpenseVM> Expenses { get; set; } = new();
        public List<SalesSimpleReportBankTransferVM> TransferSalesByBank { get; set; } = new();
        public List<SalesSimpleReportCustomerCreditVM> CreditSalesByCustomer { get; set; } = new();
        public decimal GrossSalesLyd { get; set; }
        public decimal ReturnsLyd { get; set; }
        public decimal NetSalesLyd { get; set; }
        public decimal CashSalesLyd { get; set; }
        public decimal CardSalesLyd { get; set; }
        public decimal TransferSalesLyd { get; set; }
        public decimal CreditSalesLyd { get; set; }
        public decimal TotalExpensesLyd { get; set; }
        public decimal CashExpensesLyd { get; set; }
        public decimal CardExpensesLyd { get; set; }
        public decimal TransferExpensesLyd { get; set; }
        public decimal InternalExpensesLyd { get; set; }
        public int InvoiceCount { get; set; }
        public int ReturnInvoiceCount { get; set; }
        public int SoldInvoiceCount { get; set; }

        public decimal ItemsQtyTotal => Items.Sum(x => x.NetQty);
        public decimal ItemsSalesTotalLyd => Items.Sum(x => x.NetDinar);
        public decimal ItemsGrossProfitLyd => Items.Sum(x => x.GrossProfitLyd);
        public decimal NetAfterExpensesLyd => NetSalesLyd - TotalExpensesLyd;
    }

    public class SalesSimpleReportBankTransferVM
    {
        public string BankName { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal TotalLyd { get; set; }
    }

    public class SalesSimpleReportCustomerCreditVM
    {
        public string CustomerName { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal TotalLyd { get; set; }
    }

    public class SalesSimpleReportItemVM
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal SoldQty { get; set; }
        public decimal ReturnQty { get; set; }
        public decimal NetQty { get; set; }
        public decimal GrossSalesDinar { get; set; }
        public decimal ReturnDinar { get; set; }
        public decimal NetDinar { get; set; }
        public decimal TotalCostLyd { get; set; }
        public decimal GrossProfitLyd { get; set; }
    }

    public class SalesSimpleReportExpenseVM
    {
        public DateOnly ExpenseDate { get; set; }
        public string ExpenseKind { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? EmployeeName { get; set; }
        public string? Note { get; set; }
        public decimal Amount { get; set; }
    }
}
