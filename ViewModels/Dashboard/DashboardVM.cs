using System;
using System.Collections.Generic;

namespace NewsApp2.ViewModels.Dashboard
{
    public class DashboardVM
    {
        public DateOnly Today { get; set; }

        // Today's activity
        public decimal NetSalesTodayLyd { get; set; }
        public decimal CashSalesTodayLyd { get; set; }
        public int InvoiceCountToday { get; set; }

        // Shifts
        public int OpenShiftCount { get; set; }
        public decimal OpenShiftOpeningCashLyd { get; set; }

        // Inventory
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        // Accounts
        public decimal ReceivablesLyd { get; set; }
        public decimal PayablesLyd { get; set; }

        public List<DashboardStockRow> LowStockItems { get; set; } = new();
        public List<DashboardInvoiceRow> RecentInvoices { get; set; } = new();
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
