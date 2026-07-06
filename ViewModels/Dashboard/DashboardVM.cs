using System;
using System.Collections.Generic;
using System.Linq;

namespace NewsApp2.ViewModels.Dashboard
{
    public class DashboardVM
    {
        public DateOnly Today { get; set; }
        public DateOnly MonthStart { get; set; }

        // Today
        public decimal SalesTodayLyd { get; set; }
        public int InvoiceCountToday { get; set; }
        public decimal CashTodayLyd { get; set; }

        // This month
        public decimal SalesMonthLyd { get; set; }
        public int InvoiceCountMonth { get; set; }
        public decimal GrossProfitMonthLyd { get; set; }

        // Shifts
        public int OpenShiftCount { get; set; }

        // Inventory
        public int ItemCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public decimal StockValueLyd { get; set; }

        // Accounts
        public decimal ReceivablesLyd { get; set; }
        public decimal PayablesLyd { get; set; }

        public List<DashboardDayRow> Last7Days { get; set; } = new();
        public List<DashboardTopItemRow> TopItems { get; set; } = new();
        public List<DashboardStockRow> LowStockItems { get; set; } = new();
        public List<DashboardInvoiceRow> RecentInvoices { get; set; } = new();

        public decimal Last7DaysMax => Last7Days.Count == 0 ? 0m : Last7Days.Max(d => d.NetSalesLyd);
    }

    public class DashboardDayRow
    {
        public DateOnly Date { get; set; }
        public decimal NetSalesLyd { get; set; }
    }

    public class DashboardTopItemRow
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal SalesLyd { get; set; }
    }

    public class DashboardStockRow
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Reorder { get; set; }
    }

    public class DashboardInvoiceRow
    {
        public Guid Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string? CustomerName { get; set; }
        public decimal TotalDinar { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
