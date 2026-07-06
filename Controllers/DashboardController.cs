using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Services;
using NewsApp2.ViewModels.Dashboard;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "AdminOrProgPolicy")] // Prog / Admin / SalesManager
    public class DashboardController : Controller
    {
        private const decimal DefaultLowThreshold = 5m;

        private readonly AppDbContext _context;
        private readonly AccountBalanceService _balances;

        public DashboardController(AppDbContext context, AccountBalanceService balances)
        {
            _context = context;
            _balances = balances;
        }

        private static bool IsReturn(decimal total, string? note)
            => total < 0m || (note != null && note.Contains("[POS-RETURN]"));

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var weekStart = today.AddDays(-6);

            // ---- Today's sales ----
            var todayRows = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted" && i.InvoiceDate == today)
                .Select(i => new { i.TotalDinar, i.PaymentMethod, i.Note })
                .ToListAsync();
            var todaySales = todayRows.Where(r => !IsReturn(r.TotalDinar, r.Note)).ToList();
            var salesToday = todaySales.Sum(r => r.TotalDinar) - todayRows.Where(r => IsReturn(r.TotalDinar, r.Note)).Sum(r => Math.Abs(r.TotalDinar));
            var cashToday = todaySales.Where(r => r.PaymentMethod == null || r.PaymentMethod == "Cash").Sum(r => r.TotalDinar);

            // ---- This month sales (net + count) ----
            var monthRows = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted" && i.InvoiceDate >= monthStart && i.InvoiceDate <= today)
                .Select(i => new { i.TotalDinar, i.Note })
                .ToListAsync();
            var monthSales = monthRows.Where(r => !IsReturn(r.TotalDinar, r.Note)).ToList();
            var salesMonth = monthSales.Sum(r => r.TotalDinar) - monthRows.Where(r => IsReturn(r.TotalDinar, r.Note)).Sum(r => Math.Abs(r.TotalDinar));

            // ---- Month lines: gross profit + top items (same methodology as the daily report) ----
            var monthLines = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => l.Qty > 0 && l.LineTotalDinar > 0
                    && l.SalesInvoice != null
                    && l.SalesInvoice.Status == "Posted"
                    && l.SalesInvoice.InvoiceDate >= monthStart
                    && l.SalesInvoice.InvoiceDate <= today)
                .Select(l => new { l.Qty, l.LineTotalDinar, l.LineCostDinar, ItemName = l.Item != null ? l.Item.Name : "-" })
                .ToListAsync();
            var grossProfitMonth = monthLines.Sum(l => l.LineTotalDinar - l.LineCostDinar);
            var topItems = monthLines
                .GroupBy(l => l.ItemName)
                .Select(g => new DashboardTopItemRow { ItemName = g.Key, Qty = g.Sum(x => x.Qty), SalesLyd = g.Sum(x => x.LineTotalDinar) })
                .OrderByDescending(x => x.SalesLyd)
                .Take(5)
                .ToList();

            // ---- Last 7 days trend ----
            var weekRows = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted" && i.InvoiceDate >= weekStart && i.InvoiceDate <= today)
                .Select(i => new { i.InvoiceDate, i.TotalDinar, i.Note })
                .ToListAsync();
            var last7 = new List<DashboardDayRow>();
            for (var d = 0; d < 7; d++)
            {
                var date = weekStart.AddDays(d);
                var dayRows = weekRows.Where(r => r.InvoiceDate == date).ToList();
                var net = dayRows.Where(r => !IsReturn(r.TotalDinar, r.Note)).Sum(r => r.TotalDinar)
                        - dayRows.Where(r => IsReturn(r.TotalDinar, r.Note)).Sum(r => Math.Abs(r.TotalDinar));
                last7.Add(new DashboardDayRow { Date = date, NetSalesLyd = net });
            }

            // ---- Open shifts ----
            var openShiftCount = await _context.Set<PosShift>().AsNoTracking().CountAsync(s => s.Status == "Open");

            // ---- Inventory ----
            var balanceRows = await _context.Set<InvStockBalance>()
                .AsNoTracking()
                .Where(b => b.Item != null)
                .Select(b => new { b.ItemId, ItemName = b.Item!.Name, b.QuantityOnHand, Reorder = b.Item.ReorderLevel, b.AverageCostLyd })
                .ToListAsync();

            decimal Limit(decimal? reorder) => (reorder.HasValue && reorder.Value > 0) ? reorder.Value : DefaultLowThreshold;

            var itemCount = await _context.Set<Item>().AsNoTracking().CountAsync();
            var stockValue = balanceRows.Sum(b => b.QuantityOnHand * b.AverageCostLyd);
            var lowItems = balanceRows
                .Where(b => b.QuantityOnHand <= Limit(b.Reorder))
                .OrderBy(b => b.QuantityOnHand)
                .Take(8)
                .Select(b => new DashboardStockRow { ItemId = b.ItemId, ItemName = b.ItemName, Qty = b.QuantityOnHand, Reorder = b.Reorder ?? 0m })
                .ToList();

            // ---- Recent invoices today ----
            var recent = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .Where(i => i.Status == "Posted" && i.InvoiceDate == today)
                .OrderByDescending(i => i.Created)
                .Take(8)
                .Select(i => new DashboardInvoiceRow
                {
                    Id = i.Id,
                    Number = i.Number,
                    CreatedUtc = i.Created,
                    CustomerName = i.Customer != null ? i.Customer.Name : null,
                    TotalDinar = i.TotalDinar,
                    PaymentMethod = i.PaymentMethod
                })
                .ToListAsync();

            // ---- Party balances (guarded; never fail the dashboard) ----
            decimal receivables = 0m, payables = 0m;
            try { receivables = await _balances.GetCustomerReceivablesTotalAsync(); } catch { /* non-fatal */ }
            try { payables = await _balances.GetSupplierPayablesTotalAsync(); } catch { /* non-fatal */ }

            var vm = new DashboardVM
            {
                Today = today,
                MonthStart = monthStart,
                SalesTodayLyd = salesToday,
                InvoiceCountToday = todaySales.Count,
                CashTodayLyd = cashToday,
                SalesMonthLyd = salesMonth,
                InvoiceCountMonth = monthSales.Count,
                GrossProfitMonthLyd = grossProfitMonth,
                OpenShiftCount = openShiftCount,
                ItemCount = itemCount,
                LowStockCount = balanceRows.Count(b => b.QuantityOnHand <= Limit(b.Reorder)),
                OutOfStockCount = balanceRows.Count(b => b.QuantityOnHand <= 0),
                StockValueLyd = stockValue,
                ReceivablesLyd = receivables,
                PayablesLyd = payables,
                Last7Days = last7,
                TopItems = topItems,
                LowStockItems = lowItems,
                RecentInvoices = recent
            };

            return View(vm);
        }
    }
}
