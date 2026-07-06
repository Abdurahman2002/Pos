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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // --- Today's sales (read-only) ---
            var todayRows = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted" && i.InvoiceDate == today)
                .Select(i => new { i.TotalDinar, i.PaymentMethod, i.Note })
                .ToListAsync();

            static bool IsReturn(decimal total, string? note)
                => total < 0m || (note != null && note.Contains("[POS-RETURN]"));

            var sales = todayRows.Where(r => !IsReturn(r.TotalDinar, r.Note)).ToList();
            var grossToday = sales.Sum(r => r.TotalDinar);
            var returnsToday = todayRows.Where(r => IsReturn(r.TotalDinar, r.Note)).Sum(r => Math.Abs(r.TotalDinar));
            var cashToday = sales.Where(r => r.PaymentMethod == null || r.PaymentMethod == "Cash").Sum(r => r.TotalDinar);

            // --- Open shifts ---
            var openShiftOpening = await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.Status == "Open")
                .Select(s => s.OpeningCashLyd)
                .ToListAsync();

            // --- Inventory (low / out of stock) ---
            var balanceRows = await _context.Set<InvStockBalance>()
                .AsNoTracking()
                .Where(b => b.Item != null)
                .Select(b => new { b.ItemId, ItemName = b.Item!.Name, b.QuantityOnHand, Reorder = b.Item.ReorderLevel })
                .ToListAsync();

            decimal Limit(decimal? reorder) => (reorder.HasValue && reorder.Value > 0) ? reorder.Value : DefaultLowThreshold;

            var lowItems = balanceRows
                .Where(b => b.QuantityOnHand <= Limit(b.Reorder))
                .OrderBy(b => b.QuantityOnHand)
                .Take(10)
                .Select(b => new DashboardStockRow
                {
                    ItemId = b.ItemId,
                    ItemName = b.ItemName,
                    Qty = b.QuantityOnHand,
                    Reorder = b.Reorder ?? 0m
                })
                .ToList();

            // --- Recent invoices today ---
            var recent = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .Where(i => i.Status == "Posted" && i.InvoiceDate == today)
                .OrderByDescending(i => i.Created)
                .Take(10)
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

            // --- Party balances (read-only; guarded so the dashboard never fails on these) ---
            decimal receivables = 0m, payables = 0m;
            try { receivables = await _balances.GetCustomerReceivablesTotalAsync(); } catch { /* non-fatal */ }
            try { payables = await _balances.GetSupplierPayablesTotalAsync(); } catch { /* non-fatal */ }

            var vm = new DashboardVM
            {
                Today = today,
                NetSalesTodayLyd = grossToday - returnsToday,
                CashSalesTodayLyd = cashToday,
                InvoiceCountToday = sales.Count,
                OpenShiftCount = openShiftOpening.Count,
                OpenShiftOpeningCashLyd = openShiftOpening.Sum(),
                LowStockCount = balanceRows.Count(b => b.QuantityOnHand <= Limit(b.Reorder)),
                OutOfStockCount = balanceRows.Count(b => b.QuantityOnHand <= 0),
                ReceivablesLyd = receivables,
                PayablesLyd = payables,
                LowStockItems = lowItems,
                RecentInvoices = recent
            };

            return View(vm);
        }
    }
}
