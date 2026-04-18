namespace NewsApp2.ViewModels.Financial
{
    public class ProfitLossItemRowVM
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;

        public decimal SoldQty { get; set; }
        public decimal RevenueEur { get; set; }
        public decimal RevenueDinar { get; set; }

        public decimal CogsEur { get; set; }
        public decimal CogsDinar { get; set; }

        public decimal GrossProfitEur { get; set; }
        public decimal GrossProfitDinar { get; set; }

        public decimal RemainingQty { get; set; }
        public decimal RemainingValueEur { get; set; }
        public decimal RemainingValueDinar { get; set; }
    }

    public class ProfitLossReportVM
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }

        public string SecondaryCurrencyCode { get; set; } = "LYD";

        public decimal RevenueEur { get; set; }
        public decimal RevenueDinar { get; set; }

        public decimal CogsEur { get; set; }
        public decimal CogsDinar { get; set; }

        public decimal GrossProfitEur { get; set; }
        public decimal GrossProfitDinar { get; set; }

        public decimal MarginPercent { get; set; }

        public decimal RemainingStockQty { get; set; }
        public decimal RemainingStockValueEur { get; set; }
        public decimal RemainingStockValueDinar { get; set; }

        public decimal PostedPurchasesInPeriodEur { get; set; }
        public decimal PostedPurchasesInPeriodDinar { get; set; }

        // Payment Methods Breakdown (Sales)
        public decimal CashSalesDinar { get; set; }
        public decimal CardSalesDinar { get; set; }
        public decimal TransferSalesDinar { get; set; }
        public decimal CreditSalesDinar { get; set; }
        public decimal ReturnsDinar { get; set; }

        // Operating outflows in period
        public decimal GeneralExpensesDinar { get; set; }
        public decimal SalariesDinar { get; set; }
        public decimal AdvancesDinar { get; set; }
        public decimal TotalOperatingOutflowsDinar { get; set; }
        public decimal NetProfitAfterExpensesDinar { get; set; }

        public List<ProfitLossItemRowVM> Rows { get; set; } = new();
    }
}