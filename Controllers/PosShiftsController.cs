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
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
        {
            var userId = User?.Identity?.IsAuthenticated == true
                ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                : null;

            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);
            if (fromDate > toDate) { var t = fromDate; fromDate = toDate; toDate = t; }

            var principal = User;
            var canViewAny = principal?.IsInRole("Admin") == true
                || principal?.IsInRole("Prog") == true
                || principal?.IsInRole("SalesManager") == true;

            var query = _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedAtUtc >= fromDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime()
                         && s.OpenedAtUtc < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());

            if (!canViewAny)
                query = query.Where(s => s.OpenedByUserId == userId);

            var shifts = await query
                .OrderByDescending(s => s.OpenedAtUtc)
                .Select(s => new
                {
                    s.Id,
                    s.OpenedByUserName,
                    s.OpenedAtUtc,
                    s.ClosedAtUtc,
                    s.OpeningCashLyd,
                    s.ClosingCashLyd,
                    s.Status,
                    s.Note
                })
                .ToListAsync();

            var shiftIds = shifts.Select(s => s.Id).ToList();
            var invoicesByShift = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.PosShiftId != null && shiftIds.Contains(i.PosShiftId.Value) && i.Status == "Posted")
                .Select(i => new
                {
                    ShiftId = i.PosShiftId!.Value,
                    i.TotalDinar,
                    i.PaymentMethod,
                    IsReturn = (i.Note != null && i.Note.Contains("[POS-RETURN]")) || i.TotalDinar < 0
                })
                .ToListAsync();

            var rows = shifts.Select(s =>
            {
                var invs = invoicesByShift.Where(i => i.ShiftId == s.Id).ToList();
                var gross = invs.Where(i => !i.IsReturn).Sum(i => i.TotalDinar);
                var returns = invs.Where(i => i.IsReturn).Sum(i => Math.Abs(i.TotalDinar));
                var net = gross - returns;
                var cashSales = invs.Where(i => !i.IsReturn && (i.PaymentMethod == null || i.PaymentMethod == "Cash")).Sum(i => i.TotalDinar);
                var expectedCash = s.OpeningCashLyd + cashSales;
                var actualCash = s.ClosingCashLyd;
                var diff = actualCash.HasValue ? actualCash.Value - expectedCash : (decimal?)null;

                var diffText = diff.HasValue
                    ? (diff.Value >= 0
                        ? $"+{diff.Value:0.00}"
                        : $"{-diff.Value:0.00}")
                    : "-";

                return new PosShiftListItemVM
                {
                    ShiftId = s.Id,
                    CashierName = s.OpenedByUserName ?? "-",
                    OpenedAtLocal = s.OpenedAtUtc.ToLocalTime(),
                    ClosedAtLocal = s.ClosedAtUtc?.ToLocalTime(),
                    Status = s.Status,
                    OpeningCashLyd = s.OpeningCashLyd,
                    GrossSalesLyd = gross,
                    ReturnsLyd = returns,
                    NetSalesLyd = net,
                    ExpectedCashLyd = expectedCash,
                    ActualCashLyd = actualCash,
                    CashDifferenceLyd = diff ?? 0m,
                    CashDifferenceText = diffText,
                    IsDeficit = diff.HasValue && diff.Value < 0,
                    InvoiceCount = invs.Count
                };
            }).ToList();

            ViewBag.From = fromDate;
            ViewBag.To = toDate;

            return View(rows);
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

            var shiftCashReceipts = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => r.PosShiftId == shift.Id)
                .Where(r => r.PaymentMethod == null || r.PaymentMethod == PaymentCash)
                .SumAsync(r => r.Amount);

            var shiftCashExpenses = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.PosShiftId == shift.Id)
                .Where(e => e.PaymentMethod == null || e.PaymentMethod == PaymentCash)
                .Where(e => e.ExpenseKind != "Deduction")
                .SumAsync(e => e.Amount);

            expectedCash += shiftCashSales + shiftCashReceipts - shiftCashExpenses;

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
            var cashReceipts = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => r.PosShiftId == shift.Id)
                .Where(r => r.PaymentMethod == null || r.PaymentMethod == PaymentCash)
                .SumAsync(r => r.Amount);

            var expenses = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.PosShiftId == shift.Id)
                .OrderBy(e => e.ExpenseDate)
                .ThenBy(e => e.Created)
                .Select(e => new PosShiftExpenseRowVM
                {
                    ExpenseDate = e.ExpenseDate,
                    ExpenseKind = e.ExpenseKind,
                    Category = e.Category,
                    PaymentMethod = e.PaymentMethod,
                    Amount = e.Amount,
                    Note = e.Note
                })
                .ToListAsync();

            var cashExpenses = expenses
                .Where(e => e.ExpenseKind != "Deduction")
                .Where(e => e.PaymentMethod == null || e.PaymentMethod == PaymentCash)
                .Sum(e => e.Amount);
            var totalExpenses = expenses
                .Where(e => e.ExpenseKind != "Deduction")
                .Sum(e => e.Amount);

            var expectedCash = shift.OpeningCashLyd + cashSales + cashReceipts - cashExpenses;
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
                CashReceiptsLyd = cashReceipts,
                CashExpensesLyd = cashExpenses,
                TotalExpensesLyd = totalExpenses,
                ExpectedCashLyd = expectedCash,
                ActualCashLyd = actualCash,
                CashDifferenceLyd = diff,
                InvoiceCount = invoices.Count,
                Invoices = invoices,
                Expenses = expenses
            };

            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Prog,SalesManager")]
        public async Task<IActionResult> CashierPerformance(DateOnly? from, DateOnly? to, Guid? employeeId, string? userId, decimal? commissionPercent)
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

            var settings = await _context.Set<InventorySettings>()
                .AsNoTracking()
                .Select(s => new { s.CommissionSalesStepLyd, s.CommissionAmountPerStepLyd })
                .FirstOrDefaultAsync();

            var commissionStep = settings?.CommissionSalesStepLyd > 0 ? settings.CommissionSalesStepLyd : 1000m;
            var commissionAmountPerStep = settings?.CommissionAmountPerStepLyd ?? 10m;

            if (HttpContext != null)
            {
                var employeesQuery = _context.Set<Employee>()
                    .AsNoTracking()
                    .Include(e => e.ApplicationUser)
                    .OrderBy(e => e.Name)
                    .AsQueryable();

                if (!employeeId.HasValue && !string.IsNullOrWhiteSpace(userId))
                {
                    employeeId = await employeesQuery
                        .Where(e => e.UserId == userId)
                        .Select(e => (Guid?)e.Id)
                        .FirstOrDefaultAsync();
                }

                var allEmployees = await employeesQuery
                    .Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.UserId,
                        e.BaseSalaryLyd,
                        UserEmail = e.ApplicationUser != null ? e.ApplicationUser.Email : null
                    })
                    .ToListAsync();

                var selectedEmployees = allEmployees
                    .Where(e => !employeeId.HasValue || e.Id == employeeId.Value)
                    .ToList();

                var selectedUserIds = selectedEmployees
                    .Select(e => e.UserId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                var reportInvoices = await _context.Set<SalesInvoice>()
                    .AsNoTracking()
                    .Where(i => i.Status == "Posted")
                    .Where(i => i.PosShiftId != null)
                    .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
                    .Where(i => i.CreatedByUserId != null && selectedUserIds.Contains(i.CreatedByUserId))
                    .Select(i => new
                    {
                        i.CreatedByUserId,
                        i.CreatedByUserName,
                        i.TotalDinar,
                        i.PaymentMethod,
                        IsReturn = (i.Note != null && i.Note.Contains("[POS-RETURN]")) || i.TotalDinar < 0
                    })
                    .ToListAsync();

                var reportShiftCounts = await _context.Set<PosShift>()
                    .AsNoTracking()
                    .Where(s => s.OpenedAtUtc >= fromDate.ToDateTime(TimeOnly.MinValue) && s.OpenedAtUtc < toDate.AddDays(1).ToDateTime(TimeOnly.MinValue))
                    .Where(s => s.OpenedByUserId != null && selectedUserIds.Contains(s.OpenedByUserId))
                    .GroupBy(s => s.OpenedByUserId)
                    .Select(g => new { UserId = g.Key ?? string.Empty, Count = g.Count() })
                    .ToListAsync();

                var selectedEmployeeIds = selectedEmployees.Select(e => e.Id).ToList();
                var payrollExpenseRows = await _context.Set<ExpenseEntry>()
                    .AsNoTracking()
                    .Where(e => e.EmployeeId != null && selectedEmployeeIds.Contains(e.EmployeeId.Value))
                    .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
                    .Where(e => e.ExpenseKind == "Salary" ||
                                e.ExpenseKind == "Advance" ||
                                e.ExpenseKind == "CommissionWithdrawal" ||
                                e.ExpenseKind == "Deduction")
                    .Select(e => new { EmployeeId = e.EmployeeId ?? Guid.Empty, e.ExpenseKind, e.Amount })
                    .ToListAsync();

                var employeeMovements = payrollExpenseRows
                    .GroupBy(e => e.EmployeeId)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            SalaryPaid = g.Where(x => x.ExpenseKind == "Salary").Sum(x => x.Amount),
                            Advances = g.Where(x => x.ExpenseKind == "Advance").Sum(x => x.Amount),
                            Withdrawals = g.Where(x => x.ExpenseKind == "CommissionWithdrawal").Sum(x => x.Amount),
                            Deductions = g.Where(x => x.ExpenseKind == "Deduction").Sum(x => x.Amount)
                        });

                var shiftCountsByUser = reportShiftCounts
                    .GroupBy(s => s.UserId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

                var reportRows = selectedEmployees
                    .Select(employee =>
                    {
                        var employeeUserId = employee.UserId ?? string.Empty;
                        var employeeInvoices = reportInvoices
                            .Where(i => !string.IsNullOrWhiteSpace(employeeUserId) && i.CreatedByUserId == employeeUserId)
                            .ToList();

                        var gross = employeeInvoices.Where(x => !x.IsReturn).Sum(x => x.TotalDinar);
                        var returns = employeeInvoices.Where(x => x.IsReturn).Sum(x => Math.Abs(x.TotalDinar));
                        var net = gross - returns;
                        var shiftCount = !string.IsNullOrWhiteSpace(employeeUserId) && shiftCountsByUser.TryGetValue(employeeUserId, out var count) ? count : 0;
                        var commission = commissionStep > 0
                            ? Math.Floor(Math.Max(0m, net) / commissionStep) * commissionAmountPerStep
                            : 0m;
                        var movements = employeeMovements.TryGetValue(employee.Id, out var movement)
                            ? movement
                            : new { SalaryPaid = 0m, Advances = 0m, Withdrawals = 0m, Deductions = 0m };
                        var netDue = employee.BaseSalaryLyd + commission - movements.SalaryPaid - movements.Withdrawals - movements.Advances - movements.Deductions;

                        return new CashierPerformanceRowVM
                        {
                            EmployeeId = employee.Id,
                            UserId = employeeUserId,
                            UserName = string.IsNullOrWhiteSpace(employee.UserEmail) ? employee.Name : employee.UserEmail,
                            EmployeeName = employee.Name,
                            HasSystemAccount = !string.IsNullOrWhiteSpace(employeeUserId),
                            ShiftCount = shiftCount,
                            SoldInvoiceCount = employeeInvoices.Count(x => !x.IsReturn),
                            ReturnInvoiceCount = employeeInvoices.Count(x => x.IsReturn),
                            GrossSalesLyd = gross,
                            ReturnsLyd = returns,
                            NetSalesLyd = net,
                            CashSalesLyd = employeeInvoices.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "Ù†Ù‚Ø¯ÙŠ").Sum(x => x.TotalDinar),
                            CardSalesLyd = employeeInvoices.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "Ø¨Ø·Ø§Ù‚Ø©").Sum(x => x.TotalDinar),
                            TransferSalesLyd = employeeInvoices.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "ØªØ­ÙˆÙŠÙ„").Sum(x => x.TotalDinar),
                            CreditSalesLyd = employeeInvoices.Where(x => !x.IsReturn && NormalizePaymentMethod(x.PaymentMethod) == "Ø¢Ø¬Ù„").Sum(x => x.TotalDinar),
                            CommissionAmountLyd = Math.Round(commission, 2, MidpointRounding.ToEven),
                            BaseSalaryLyd = employee.BaseSalaryLyd,
                            SalaryPaidLyd = movements.SalaryPaid,
                            CommissionWithdrawalsLyd = movements.Withdrawals,
                            AdvancesLyd = movements.Advances,
                            DeductionsLyd = movements.Deductions,
                            NetDueLyd = Math.Round(netDue, 2, MidpointRounding.ToEven)
                        };
                    })
                    .Where(r => employeeId.HasValue ||
                                r.HasSystemAccount ||
                                r.BaseSalaryLyd != 0 ||
                                r.SalaryPaidLyd != 0 ||
                                r.AdvancesLyd != 0 ||
                                r.CommissionWithdrawalsLyd != 0 ||
                                r.DeductionsLyd != 0 ||
                                r.NetSalesLyd != 0 ||
                                r.ShiftCount != 0)
                    .OrderByDescending(r => r.NetSalesLyd)
                    .ThenBy(r => r.EmployeeName)
                    .ToList();

                ViewData["Employees"] = allEmployees
                    .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = string.IsNullOrWhiteSpace(e.UserId) ? $"{e.Name} - بدون حساب" : e.Name,
                        Selected = employeeId.HasValue && e.Id == employeeId.Value
                    })
                    .ToList();

                var reportVm = new CashierPerformanceVM
                {
                    From = fromDate,
                    To = toDate,
                    EmployeeId = employeeId,
                    UserId = userId,
                    CommissionPercent = 0m,
                    CommissionSalesStepLyd = commissionStep,
                    CommissionAmountPerStepLyd = commissionAmountPerStep,
                    Rows = reportRows,
                    TotalShiftCount = reportRows.Sum(r => r.ShiftCount),
                    TotalSoldInvoiceCount = reportRows.Sum(r => r.SoldInvoiceCount),
                    TotalNetSalesLyd = reportRows.Sum(r => r.NetSalesLyd),
                    TotalBaseSalaryLyd = reportRows.Sum(r => r.BaseSalaryLyd),
                    TotalSalaryPaidLyd = reportRows.Sum(r => r.SalaryPaidLyd),
                    TotalCommissionLyd = reportRows.Sum(r => r.CommissionAmountLyd),
                    TotalNetDueLyd = reportRows.Sum(r => r.NetDueLyd)
                };

                return View(reportVm);
            }

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

            var userIds = invoices
                .Select(i => i.CreatedByUserId ?? string.Empty)
                .Concat(shiftCounts.Select(s => s.UserId ?? string.Empty))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var employeeRows = await _context.Set<Employee>()
                .AsNoTracking()
                .Where(e => e.UserId != null && userIds.Contains(e.UserId))
                .Select(e => new { e.Id, e.UserId, e.BaseSalaryLyd })
                .ToListAsync();

            var salaryByUser = employeeRows
                .Where(e => !string.IsNullOrWhiteSpace(e.UserId))
                .ToDictionary(e => e.UserId!, e => e.BaseSalaryLyd);

            var userByEmployeeId = employeeRows
                .Where(e => !string.IsNullOrWhiteSpace(e.UserId))
                .ToDictionary(e => e.Id, e => e.UserId!);

            var employeeExpenseRows = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.EmployeeId != null)
                .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
                .Where(e => e.ExpenseKind == "Advance" ||
                            e.ExpenseKind == "CommissionWithdrawal" ||
                            e.ExpenseKind == "Deduction")
                .Select(e => new { e.EmployeeId, e.ExpenseKind, e.Amount })
                .ToListAsync();

            var employeeMovementsByUser = employeeExpenseRows
                .Where(e => e.EmployeeId.HasValue && userByEmployeeId.ContainsKey(e.EmployeeId.Value))
                .GroupBy(e => userByEmployeeId[e.EmployeeId!.Value])
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Advances = g.Where(x => x.ExpenseKind == "Advance").Sum(x => x.Amount),
                        Withdrawals = g.Where(x => x.ExpenseKind == "CommissionWithdrawal").Sum(x => x.Amount),
                        Deductions = g.Where(x => x.ExpenseKind == "Deduction").Sum(x => x.Amount)
                    });

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

                    var commission = commissionStep > 0
                        ? Math.Floor(Math.Max(0m, net) / commissionStep) * commissionAmountPerStep
                        : 0m;
                    var baseSalary = salaryByUser.TryGetValue(g.Key.UserId, out var salary) ? salary : 0m;
                    var movements = employeeMovementsByUser.TryGetValue(g.Key.UserId, out var movement)
                        ? movement
                        : new { Advances = 0m, Withdrawals = 0m, Deductions = 0m };
                    var netDue = baseSalary + commission - movements.Withdrawals - movements.Advances - movements.Deductions;

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
                        CommissionAmountLyd = Math.Round(commission, 2, MidpointRounding.ToEven),
                        BaseSalaryLyd = baseSalary,
                        CommissionWithdrawalsLyd = movements.Withdrawals,
                        AdvancesLyd = movements.Advances,
                        DeductionsLyd = movements.Deductions,
                        NetDueLyd = Math.Round(netDue, 2, MidpointRounding.ToEven)
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
                CommissionPercent = 0m,
                CommissionSalesStepLyd = commissionStep,
                CommissionAmountPerStepLyd = commissionAmountPerStep,
                Rows = rows,
                TotalShiftCount = rows.Sum(r => r.ShiftCount),
                TotalSoldInvoiceCount = rows.Sum(r => r.SoldInvoiceCount),
                TotalNetSalesLyd = rows.Sum(r => r.NetSalesLyd),
                TotalCommissionLyd = rows.Sum(r => r.CommissionAmountLyd),
                TotalNetDueLyd = rows.Sum(r => r.NetDueLyd)
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
