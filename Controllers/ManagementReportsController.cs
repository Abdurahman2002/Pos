using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.ManagementReports;

namespace NewsApp2.Controllers;

[Authorize(Policy = "InventoryCreatePolicy")]
[Authorize(Policy = "ApprovedUserPolicy")]
[ViewLayout("_LayoutDashboard")]
public class ManagementReportsController : Controller
{
    private readonly AppDbContext _context;

    public ManagementReportsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Reports(DateOnly? from, DateOnly? to)
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        from ??= new DateOnly(now.Year, now.Month, 1);
        to ??= now;

        var sales = await _context.Set<SalesInvoice>()
            .AsNoTracking()
            .Where(i => i.Status == "Posted" && i.InvoiceDate >= from.Value && i.InvoiceDate <= to.Value)
            .ToListAsync();

        static bool IsReturnInvoice(SalesInvoice invoice)
        {
            return invoice.TotalDinar < 0
                || (!string.IsNullOrWhiteSpace(invoice.Note)
                    && invoice.Note.Contains("[POS-RETURN]", StringComparison.OrdinalIgnoreCase));
        }

        var saleInvoices = sales
            .Where(i => !IsReturnInvoice(i) && i.TotalDinar > 0)
            .ToList();

        var returnInvoices = sales
            .Where(IsReturnInvoice)
            .ToList();

        var saleInvoiceIds = saleInvoices.Select(i => i.Id).ToList();
        var returnInvoiceIds = returnInvoices.Select(i => i.Id).ToList();

        var saleLines = await _context.Set<SalesLine>()
            .AsNoTracking()
            .Include(l => l.Item)
            .Where(l => saleInvoiceIds.Contains(l.SalesInvoiceId) && l.LineTotalDinar > 0 && l.Qty > 0)
            .ToListAsync();

        var returnLines = await _context.Set<SalesLine>()
            .AsNoTracking()
            .Where(l => returnInvoiceIds.Contains(l.SalesInvoiceId) && (l.LineTotalDinar < 0 || l.Qty < 0))
            .ToListAsync();

        var salesCount = saleInvoices.Count;
        var returnCount = returnInvoices.Count;
        var totalSales = saleInvoices.Sum(i => i.TotalDinar);
        var totalReturns = returnInvoices.Sum(i => Math.Abs(i.TotalDinar));
        var salesCost = saleLines.Sum(l => l.LineCostDinar);
        var returnsCost = Math.Abs(returnLines.Sum(l => l.LineCostDinar));
        var costOfGoodsSold = salesCost - returnsCost;

        var purchases = await _context.Set<PurchaseInvoice>()
            .AsNoTracking()
            .Where(i => i.Status != "Cancelled" && i.InvoiceDate >= from.Value && i.InvoiceDate <= to.Value)
            .SumAsync(i => i.TotalDinar);

        var expenses = await _context.Set<ExpenseEntry>()
            .AsNoTracking()
            .Where(e => e.ExpenseDate >= from.Value && e.ExpenseDate <= to.Value)
            .ToListAsync();

        var operatingExpenses = expenses
            .Where(e =>
                string.Equals(e.ExpenseKind, "General", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.ExpenseKind, "Salary", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalExpenses = operatingExpenses.Sum(e => e.Amount);

        var netSales = totalSales - totalReturns;
        var grossProfit = netSales - costOfGoodsSold;
        var netProfit = grossProfit - totalExpenses;
        var margin = netSales > 0 ? (grossProfit / netSales) * 100 : 0;

        var cashSales = saleInvoices.Where(i => i.PaymentMethod == null || i.PaymentMethod == "Cash").Sum(i => i.TotalDinar);
        var cardSales = saleInvoices.Where(i => i.PaymentMethod == "Card").Sum(i => i.TotalDinar);
        var transferSales = saleInvoices.Where(i => i.PaymentMethod == "Transfer").Sum(i => i.TotalDinar);
        var creditSales = saleInvoices.Where(i => i.PaymentMethod == "Credit").Sum(i => i.TotalDinar);

        var stockValue = await _context.Set<InvStockBalance>()
            .AsNoTracking()
            .SumAsync(s => s.QuantityOnHand * s.AverageCostLyd);

        var customerDebts = await _context.Set<SalesInvoice>()
            .AsNoTracking()
            .Where(i => i.PaymentMethod == "Credit" && i.Status != "Cancelled")
            .SumAsync(i => i.TotalDinar);

        var supplierDebts = await _context.Set<PurchaseInvoice>()
            .AsNoTracking()
            .Where(i => i.PaymentMethod == "Credit" && i.Status != "Cancelled")
            .SumAsync(i => i.TotalDinar);

        var topItems = saleLines
            .GroupBy(l => l.Item!.Name)
            .Select(g => new TopItemRow
            {
                ItemName = g.Key,
                TotalQty = g.Sum(l => l.Qty),
                TotalRevenueLyd = g.Sum(l => l.LineTotalDinar),
                TotalCostLyd = g.Sum(l => l.LineCostDinar),
                GrossProfitLyd = g.Sum(l => l.LineTotalDinar - l.LineCostDinar)
            })
            .OrderByDescending(x => x.TotalRevenueLyd)
            .Take(10)
            .ToList();

        var expenseBreakdown = operatingExpenses
            .GroupBy(e => e.Category)
            .Select(g => new ExpenseCategoryRow { Category = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var vm = new ReportsVM
        {
            From = from,
            To = to,
            TotalSalesLyd = totalSales,
            TotalReturnsLyd = totalReturns,
            NetSalesLyd = netSales,
            TotalPurchasesLyd = purchases,
            CostOfGoodsSoldLyd = costOfGoodsSold,
            GrossProfitLyd = grossProfit,
            MarginPercent = Math.Round(margin, 1),
            TotalExpensesLyd = totalExpenses,
            NetProfitLyd = netProfit,
            CashSalesLyd = cashSales,
            CardSalesLyd = cardSales,
            TransferSalesLyd = transferSales,
            CreditSalesLyd = creditSales,
            SalesCount = salesCount,
            ReturnCount = returnCount,
            TotalInvoices = salesCount + returnCount,
            StockValueLyd = stockValue,
            CustomerDebtsLyd = customerDebts,
            SupplierDebtsLyd = supplierDebts,
            TopItems = topItems,
            ExpenseBreakdown = expenseBreakdown
        };

        ViewBag.SecondaryCurrencyCode = "LYD";
        return View(vm);
    }

    public async Task<IActionResult> DailyInventory(DateOnly? date)
    {
        date ??= DateOnly.FromDateTime(DateTime.UtcNow);

        var stockList = await _context.Set<InvStockBalance>()
            .AsNoTracking()
            .Include(s => s.Item)!.ThenInclude(i => i!.Category)
            .OrderBy(s => s.Item!.Category!.Name)
            .ThenBy(s => s.Item!.Name)
            .ToListAsync();

        var rows = stockList.Select(s => new DailyInventoryRow
        {
            ItemId = s.ItemId,
            ItemName = s.Item?.Name ?? "-",
            CategoryName = s.Item?.Category?.Name ?? "-",
            Barcode = s.Item?.Barcode ?? "-",
            QuantityOnHand = s.QuantityOnHand,
            AverageCostLyd = s.AverageCostLyd,
            StockValueLyd = s.QuantityOnHand * s.AverageCostLyd,
            DefaultSalePriceLyd = s.Item?.DefaultSalePriceLyd,
            SaleValueLyd = s.QuantityOnHand * (s.Item?.DefaultSalePriceLyd ?? 0m),
            ExpectedProfitLyd = s.Item?.DefaultSalePriceLyd.HasValue == true
                ? (s.QuantityOnHand * s.Item.DefaultSalePriceLyd.Value) - (s.QuantityOnHand * s.AverageCostLyd)
                : 0m,
            ReorderLevel = s.Item?.ReorderLevel,
            Status = s.QuantityOnHand <= 0 ? "نفذ" : (s.Item?.ReorderLevel.HasValue == true && s.QuantityOnHand <= s.Item.ReorderLevel.Value ? "منخفض" : "جيد")
        }).ToList();

        var vm = new DailyInventoryVM
        {
            ReportDate = date,
            TotalItems = rows.Count,
            LowStockCount = rows.Count(r => r.Status == "منخفض"),
            OutOfStockCount = rows.Count(r => r.Status == "نفذ"),
            TotalStockValueLyd = rows.Sum(r => r.StockValueLyd),
            TotalSaleValueLyd = rows.Sum(r => r.SaleValueLyd),
            TotalExpectedProfitLyd = rows.Sum(r => r.ExpectedProfitLyd),
            Rows = rows
        };

        ViewBag.SecondaryCurrencyCode = "LYD";
        return View(vm);
    }

    public async Task<IActionResult> Stock(string? search, Guid? categoryId)
    {
        var query = _context.Set<InvStockBalance>()
            .AsNoTracking()
            .Include(s => s.Item)!.ThenInclude(i => i!.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Item!.Name.Contains(term) || (s.Item!.Barcode != null && s.Item.Barcode.Contains(term)));
        }

        if (categoryId.HasValue && categoryId != Guid.Empty)
            query = query.Where(s => s.Item!.CategoryId == categoryId.Value);

        var stockList = await query
            .OrderBy(s => s.Item!.Category!.Name)
            .ThenBy(s => s.Item!.Name)
            .ToListAsync();

        var rows = stockList.Select(s => new StockRow
        {
            ItemId = s.ItemId,
            ItemName = s.Item?.Name ?? "-",
            CategoryName = s.Item?.Category?.Name ?? "-",
            Barcode = s.Item?.Barcode ?? "-",
            QuantityOnHand = s.QuantityOnHand,
            AverageCostLyd = s.AverageCostLyd,
            StockValueLyd = s.QuantityOnHand * s.AverageCostLyd,
            SaleValueLyd = s.QuantityOnHand * (s.Item?.DefaultSalePriceLyd ?? 0m),
            ExpectedProfitLyd = s.Item?.DefaultSalePriceLyd.HasValue == true
                ? (s.QuantityOnHand * s.Item.DefaultSalePriceLyd.Value) - (s.QuantityOnHand * s.AverageCostLyd)
                : 0m,
            ReorderLevel = s.Item?.ReorderLevel,
            DefaultSalePriceLyd = s.Item?.DefaultSalePriceLyd,
            Status = s.QuantityOnHand <= 0 ? "نفذ" : (s.Item?.ReorderLevel.HasValue == true && s.QuantityOnHand <= s.Item.ReorderLevel.Value ? "منخفض" : "جيد")
        }).ToList();

        var vm = new StockVM
        {
            Search = search,
            CategoryId = categoryId,
            Rows = rows
        };

        ViewBag.SecondaryCurrencyCode = "LYD";
        await LoadCategoriesAsync();
        return View(vm);
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _context.Set<Category>()
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "Id", "Name");
    }
}
