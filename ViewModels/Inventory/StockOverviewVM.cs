using System;
using System.Collections.Generic;
using NewsApp2.ViewModels.Common;

namespace NewsApp2.ViewModels.Inventory
{
    /// <summary>
    /// Unified stock screen: current quantities + status + (management-only) valuation.
    /// Replaces the overlapping StockBalances/Index, ManagementReports/Stock and
    /// ManagementReports/DailyInventory current-state views.
    /// </summary>
    public class StockOverviewVM
    {
        // Filters
        public string? Search { get; set; }
        public Guid? CategoryId { get; set; }
        public bool LowOnly { get; set; }
        public decimal LowThreshold { get; set; } = 5m;

        // Whether the current user may see cost / sale value / profit (Admin/Prog/SalesManager).
        public bool CanViewFinancials { get; set; }

        // Summary over the full filtered set
        public int TotalItems { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalCostValueLyd { get; set; }
        public decimal TotalSaleValueLyd { get; set; }
        public decimal TotalExpectedProfitLyd { get; set; }
        public int LowCount { get; set; }
        public int OutOfStockCount { get; set; }

        public List<StockOverviewRow> Rows { get; set; } = new();
        public PaginationVM? Pagination { get; set; }
    }

    public class StockOverviewRow
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? CategoryName { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal AverageCostLyd { get; set; }
        public decimal? DefaultSalePriceLyd { get; set; }
        public decimal CostValueLyd { get; set; }
        public decimal SaleValueLyd { get; set; }
        public decimal ExpectedProfitLyd { get; set; }
        public string Status { get; set; } = string.Empty; // جيد / منخفض / نفذ
    }
}
