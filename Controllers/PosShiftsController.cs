using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Pos;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    [Authorize(Roles = "Cashier,Employee,SalesOfficer,SalesManager,Admin,Prog")]
    public class PosShiftsController : Controller
    {
        private const string DailySalesCustomerName = "مبيعات يومية";
        private const string StatusOpen = "Open";
        private const string StatusClosed = "Closed";
        private const string PaymentCash = "Cash";
        private const string PaymentCard = "Card";
        private const string PaymentTransfer = "Transfer";
        private const string PaymentCredit = "Credit";

        private readonly AppDbContext _context;

        public PosShiftsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Current(string? returnUrl = null)
        {
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var activeShift = await _context.Set<PosShift>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OpenedByUserId == userId && s.Status == StatusOpen);

            var vm = new PosShiftCurrentVM
            {
                ActiveShift = activeShift,
                OpeningCashLyd = 0m,
                ClosingCashLyd = activeShift?.ClosingCashLyd ?? 0m
            };

            var latestClosedShiftId = await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedByUserId == userId && s.Status == StatusClosed)
                .OrderByDescending(s => s.ClosedAtUtc)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();

            ViewBag.LatestClosedShiftId = latestClosedShiftId;
            ViewBag.ReturnUrl = returnUrl;

            if (activeShift != null)
            {
                var salesQuery = _context.Set<SalesInvoice>()
                    .AsNoTracking()
                    .Where(i => i.CreatedByUserId == userId)
                    .Where(i => i.Created >= activeShift.OpenedAtUtc)
                    .Where(i => i.Status == "Posted")
                    .Where(i => i.PosShiftId == activeShift.Id);

                vm.SalesCount = await salesQuery.CountAsync();
                vm.SalesTotalLyd = await salesQuery.SumAsync(i => i.TotalDinar);
                vm.CashSalesLyd = await salesQuery
                    .Where(i => i.PaymentMethod == null || i.PaymentMethod == PaymentCash)
                    .SumAsync(i => i.TotalDinar);
                vm.CardSalesLyd = await salesQuery
                    .Where(i => i.PaymentMethod == PaymentCard)
                    .SumAsync(i => i.TotalDinar);
                vm.TransferSalesLyd = await salesQuery
                    .Where(i => i.PaymentMethod == PaymentTransfer)
                    .SumAsync(i => i.TotalDinar);
                vm.CreditSalesLyd = await salesQuery
                    .Where(i => i.PaymentMethod == PaymentCredit)
                    .SumAsync(i => i.TotalDinar);
                vm.ExpectedCashLyd = activeShift.OpeningCashLyd + vm.CashSalesLyd;

                if (activeShift.ClosingCashLyd.HasValue)
                    vm.CashDifferenceLyd = activeShift.ClosingCashLyd.Value - vm.ExpectedCashLyd;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(PosShiftCurrentVM vm, string? returnUrl = null)
        {
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var hasOpenShift = await _context.Set<PosShift>()
                .AnyAsync(s => s.OpenedByUserId == userId && s.Status == StatusOpen);

            if (hasOpenShift)
            {
                TempData["ErrorMessage"] = "يوجد لديك وردية مفتوحة بالفعل.";
                return RedirectToAction(nameof(Current));
            }

            var shift = new PosShift
            {
                OpenedByUserId = userId,
                OpenedByUserName = User?.Identity?.Name,
                OpeningCashLyd = vm.OpeningCashLyd,
                OpenedAtUtc = DateTime.UtcNow,
                Status = StatusOpen
            };

            _context.Set<PosShift>().Add(shift);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم فتح الوردية بنجاح.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Current));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpenQuick(string? returnUrl = null)
        {
            var vm = new PosShiftCurrentVM { OpeningCashLyd = 0m };
            return await Open(vm, returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(PosShiftCurrentVM vm)
        {
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var shift = await _context.Set<PosShift>()
                .FirstOrDefaultAsync(s => s.OpenedByUserId == userId && s.Status == StatusOpen);

            if (shift == null)
            {
                TempData["ErrorMessage"] = "لا توجد وردية مفتوحة لإغلاقها.";
                return RedirectToAction(nameof(Current));
            }

            var expectedCash = shift.OpeningCashLyd;
            var shiftCashSales = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.PosShiftId == shift.Id)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == null || i.PaymentMethod == PaymentCash)
                .SumAsync(i => i.TotalDinar);

            expectedCash += shiftCashSales;

            shift.ClosingCashLyd = vm.ClosingCashLyd;
            shift.ClosedAtUtc = DateTime.UtcNow;
            shift.Status = StatusClosed;
            shift.Note = $"المتوقع: {expectedCash:0.00} دينار، الفعلي: {vm.ClosingCashLyd:0.00} دينار، الفرق: {(vm.ClosingCashLyd - expectedCash):0.00} دينار";
            shift.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إغلاق الوردية بنجاح.";
            return RedirectToAction(nameof(Report), new { id = shift.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Report(Guid id)
        {
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var shift = await _context.Set<PosShift>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shift == null)
                return View("NotFound");

            var principal = User;
            var canViewAnyShift = principal?.IsInRole("Admin") == true
                || principal?.IsInRole("Prog") == true
                || principal?.IsInRole("SalesManager") == true;
            if (!canViewAnyShift && shift.OpenedByUserId != userId)
                return Forbid();

            var invoices = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .Where(i => i.PosShiftId == shift.Id)
                .Where(i => i.Status == "Posted")
                .OrderBy(i => i.Created)
                .Select(i => new PosShiftInvoiceRowVM
                {
                    Number = i.Number,
                    InvoiceDate = i.InvoiceDate,
                    CustomerName = i.Customer == null || i.Customer.Name == DailySalesCustomerName ? "-" : i.Customer.Name,
                    TotalLyd = i.TotalDinar,
                    PaymentMethod = NormalizePaymentMethod(i.PaymentMethod),
                    IsReturn = (i.Note != null && i.Note.Contains("[POS-RETURN]")) || i.TotalDinar < 0,
                    CreatedLocal = i.Created.ToLocalTime()
                })
                .ToListAsync();

            var grossSales = invoices.Where(i => !i.IsReturn).Sum(i => i.TotalLyd);
            var returns = invoices.Where(i => i.IsReturn).Sum(i => Math.Abs(i.TotalLyd));
            var netSales = grossSales - returns;
            var cashSales = invoices.Where(i => !i.IsReturn && i.PaymentMethod == "نقدي").Sum(i => i.TotalLyd);
            var cardSales = invoices.Where(i => !i.IsReturn && i.PaymentMethod == "بطاقة").Sum(i => i.TotalLyd);
            var transferSales = invoices.Where(i => !i.IsReturn && i.PaymentMethod == "تحويل").Sum(i => i.TotalLyd);
            var creditSales = invoices.Where(i => !i.IsReturn && i.PaymentMethod == "آجل").Sum(i => i.TotalLyd);
            var expectedCash = shift.OpeningCashLyd + cashSales;
            var actualCash = shift.ClosingCashLyd;
            var diff = actualCash.HasValue ? actualCash.Value - expectedCash : 0m;

            var vm = new PosShiftReportVM
            {
                ShiftId = shift.Id,
                OpenedByUserName = shift.OpenedByUserName ?? "-",
                OpenedAtLocal = shift.OpenedAtUtc.ToLocalTime(),
                ClosedAtLocal = shift.ClosedAtUtc?.ToLocalTime(),
                OpeningCashLyd = shift.OpeningCashLyd,
                GrossSalesLyd = grossSales,
                ReturnsLyd = returns,
                NetSalesLyd = netSales,
                CashSalesLyd = cashSales,
                CardSalesLyd = cardSales,
                TransferSalesLyd = transferSales,
                CreditSalesLyd = creditSales,
                ExpectedCashLyd = expectedCash,
                ActualCashLyd = actualCash,
                CashDifferenceLyd = diff,
                InvoiceCount = invoices.Count,
                Invoices = invoices
            };

            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Prog,SalesManager")]
        public async Task<IActionResult> CashierPerformance(DateOnly? from, DateOnly? to, string? userId, decimal? commissionPercent)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-7);
            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var percent = commissionPercent ?? 0m;
            if (percent < 0m) percent = 0m;
            if (percent > 100m) percent = 100m;

            var invoicesQuery = _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted")
                .Where(i => i.PosShiftId != null)
                .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate);

            if (!string.IsNullOrWhiteSpace(userId))
                invoicesQuery = invoicesQuery.Where(i => i.CreatedByUserId == userId);

            var invoices = await invoicesQuery
                .Select(i => new
                {
                    i.CreatedByUserId,
                    i.CreatedByUserName,
                    i.TotalDinar,
                    i.PaymentMethod,
                    IsReturn = (i.Note != null && i.Note.Contains("[POS-RETURN]")) || i.TotalDinar < 0
                })
                .ToListAsync();

            var shiftsQuery = _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedAtUtc >= fromDate.ToDateTime(TimeOnly.MinValue) && s.OpenedAtUtc < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue));

            if (!string.IsNullOrWhiteSpace(userId))
                shiftsQuery = shiftsQuery.Where(s => s.OpenedByUserId == userId);

            var shiftCounts = await shiftsQuery
                .GroupBy(s => new { s.OpenedByUserId, s.OpenedByUserName })
                .Select(g => new
                {
                    UserId = g.Key.OpenedByUserId,
                    UserName = g.Key.OpenedByUserName,
                    Count = g.Count()
                })
                .ToListAsync();

            var rows = invoices
                .GroupBy(i => new { UserId = i.CreatedByUserId ?? string.Empty, UserName = i.CreatedByUserName ?? "-" })
                .Select(g =>
                {
                    var gross = g.Where(x => !x.IsReturn).Sum(x => x.TotalDinar);
                    var returns = g.Where(x => x.IsReturn).Sum(x => Math.Abs(x.TotalDinar));
                    var net = gross - returns;

                    var shiftCount = shiftCounts
                        .Where(s => s.UserId == g.Key.UserId)
                        .Select(s => s.Count)
                        .FirstOrDefault();

                    return new CashierPerformanceRowVM
                    {
                        UserId = g.Key.UserId,
                        UserName = g.Key.UserName,
                        ShiftCount = shiftCount,
                        SoldInvoiceCount = g.Count(x => !x.IsReturn),
                        ReturnInvoiceCount = g.Count(x => x.IsReturn),
                        GrossSalesLyd = gross,
                        ReturnsLyd = returns,
                        NetSalesLyd = net,
                        CashSalesLyd = g.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "نقدي").Sum(x => x.TotalDinar),
                        CardSalesLyd = g.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "بطاقة").Sum(x => x.TotalDinar),
                        TransferSalesLyd = g.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "تحويل").Sum(x => x.TotalDinar),
                        CreditSalesLyd = g.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "آجل").Sum(x => x.TotalDinar),
                        CommissionAmountLyd = Math.Round(net * (percent / 100m), 2, MidpointRounding.ToEven)
                    };
                })
                .OrderByDescending(r => r.NetSalesLyd)
                .ThenBy(r => r.UserName)
                .ToList();

            var users = await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => !string.IsNullOrEmpty(s.OpenedByUserId))
                .GroupBy(s => new { s.OpenedByUserId, s.OpenedByUserName })
                .Select(g => new { g.Key.OpenedByUserId, g.Key.OpenedByUserName })
                .OrderBy(x => x.OpenedByUserName)
                .ToListAsync();

            ViewData["CashierUsers"] = users
                .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = u.OpenedByUserId,
                    Text = string.IsNullOrWhiteSpace(u.OpenedByUserName) ? u.OpenedByUserId : u.OpenedByUserName,
                    Selected = u.OpenedByUserId == userId
                })
                .ToList();

            var vm = new CashierPerformanceVM
            {
                From = fromDate,
                To = toDate,
                UserId = userId,
                CommissionPercent = percent,
                Rows = rows,
                TotalShiftCount = rows.Sum(r => r.ShiftCount),
                TotalSoldInvoiceCount = rows.Sum(r => r.SoldInvoiceCount),
                TotalNetSalesLyd = rows.Sum(r => r.NetSalesLyd),
                TotalCommissionLyd = rows.Sum(r => r.CommissionAmountLyd)
            };

            return View(vm);
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            var value = (paymentMethod ?? string.Empty).Trim();
            if (value.Equals(PaymentCard, StringComparison.OrdinalIgnoreCase))
                return "بطاقة";
            if (value.Equals(PaymentTransfer, StringComparison.OrdinalIgnoreCase))
                return "تحويل";
            if (value.Equals(PaymentCredit, StringComparison.OrdinalIgnoreCase))
                return "آجل";
            return "نقدي";
        }
    }
}
