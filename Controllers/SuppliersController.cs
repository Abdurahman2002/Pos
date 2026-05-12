using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Suppliers;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class SuppliersController : Controller
    {
        private const string JournalSourceType = "SupplierPayment";
        private const string AccountCashCode = "1101";
        private const string AccountCashName = "الصندوق";
        private const string AccountBankCode = "1102";
        private const string AccountBankName = "البنك";
        private const string AccountSupplierCode = "2101";
        private const string AccountSupplierName = "ذمم الموردين";

        private readonly AppDbContext _context;

        public SuppliersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Set<Supplier>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
            }

            ViewBag.Search = search;
            var suppliers = await query.OrderBy(s => s.Name).ToListAsync();
            return View(suppliers);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public IActionResult Create(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new Supplier());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Create(Supplier supplier, string? returnUrl = null)
        {
            supplier.Name = supplier.Name?.Trim() ?? string.Empty;
            supplier.Phone = string.IsNullOrWhiteSpace(supplier.Phone) ? null : supplier.Phone.Trim();
            supplier.Note = string.IsNullOrWhiteSpace(supplier.Note) ? null : supplier.Note.Trim();
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(supplier);

            var exists = await _context.Set<Supplier>().AnyAsync(s => s.Name == supplier.Name);
            if (exists)
            {
                ViewBag.Message = $"المورد '{supplier.Name}' موجود بالفعل.";
                return View(supplier);
            }

            _context.Set<Supplier>().Add(supplier);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                var target = returnUrl.Replace("__supplierId__", supplier.Id.ToString(), StringComparison.Ordinal);
                if (Url.IsLocalUrl(target))
                    return Redirect(target);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var supplier = await _context.Set<Supplier>().FindAsync(id);
            if (supplier == null)
                return View("NotFound");

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> Edit(Guid id, Supplier supplier)
        {
            if (id != supplier.Id)
                return View("NotFound");

            supplier.Name = supplier.Name?.Trim() ?? string.Empty;
            supplier.Phone = string.IsNullOrWhiteSpace(supplier.Phone) ? null : supplier.Phone.Trim();
            supplier.Note = string.IsNullOrWhiteSpace(supplier.Note) ? null : supplier.Note.Trim();

            if (!ModelState.IsValid)
                return View(supplier);

            var exists = await _context.Set<Supplier>().AnyAsync(s => s.Name == supplier.Name && s.Id != supplier.Id);
            if (exists)
            {
                ViewBag.Message = $"المورد '{supplier.Name}' موجود بالفعل.";
                return View(supplier);
            }

            _context.Set<Supplier>().Update(supplier);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var supplier = await _context.Set<Supplier>().FindAsync(id);
            if (supplier == null)
                return View("NotFound");

            var usage = await GetSupplierUsageAsync(supplier.Id);
            ViewBag.DeleteBlockedReason = usage.BlockedReason;
            ViewBag.CanDelete = string.IsNullOrWhiteSpace(usage.BlockedReason);

            return View(supplier);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var supplier = await _context.Set<Supplier>().FindAsync(id);
            if (supplier == null)
                return View("NotFound");

            var usage = await GetSupplierUsageAsync(supplier.Id);
            if (!string.IsNullOrWhiteSpace(usage.BlockedReason))
            {
                ViewBag.DeleteBlockedReason = usage.BlockedReason;
                ViewBag.CanDelete = false;
                return View("Delete", supplier);
            }

            supplier.IsDeleted = true;
            supplier.DeletedAt = DateTime.UtcNow;
            _context.Set<Supplier>().Update(supplier);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task<(string? BlockedReason, int PurchaseInvoices, int Payments)> GetSupplierUsageAsync(Guid supplierId)
        {
            var purchases = await _context.Set<PurchaseInvoice>().IgnoreQueryFilters().CountAsync(i => i.SupplierId == supplierId);
            var payments = await _context.Set<SupplierPayment>().IgnoreQueryFilters().CountAsync(p => p.SupplierId == supplierId);

            var blockers = new List<string>();
            if (purchases > 0) blockers.Add($"{purchases} purchase invoice(s)");
            if (payments > 0) blockers.Add($"{payments} payment(s)");

            var reason = blockers.Count > 0 ? $"لا يمكن حذف المورد لأنه مرتبط ببيانات تشغيلية موجودة: {string.Join(", ", blockers)}." : null;
            return (reason, purchases, payments);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Debts(string? search)
        {
            var suppliersQuery = _context.Set<Supplier>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                suppliersQuery = suppliersQuery.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
            }

            var suppliers = await suppliersQuery
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.Phone })
                .ToListAsync();

            var supplierIds = suppliers.Select(s => s.Id).ToList();

            var creditPurchases = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.SupplierId.HasValue && supplierIds.Contains(i.SupplierId.Value))
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .GroupBy(i => i.SupplierId!.Value)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    Sum = g.Sum(i => i.TotalDinar),
                    LastDate = g.Max(i => i.InvoiceDate)
                })
                .ToListAsync();

            var payments = await _context.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(r => supplierIds.Contains(r.SupplierId))
                .GroupBy(r => r.SupplierId)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    Sum = g.Sum(r => r.Amount),
                    LastDate = g.Max(r => r.PaymentDate)
                })
                .ToListAsync();

            var purchasesBySupplier = creditPurchases.ToDictionary(x => x.SupplierId, x => (x.Sum, x.LastDate));
            var paymentsBySupplier = payments.ToDictionary(x => x.SupplierId, x => (x.Sum, x.LastDate));

            var rows = suppliers
                .Select(s =>
                {
                    var purchases = purchasesBySupplier.TryGetValue(s.Id, out var p) ? p.Sum : 0m;
                    var paid = paymentsBySupplier.TryGetValue(s.Id, out var r) ? r.Sum : 0m;
                    return new SupplierDebtRowVM
                    {
                        SupplierId = s.Id,
                        SupplierName = s.Name,
                        Phone = s.Phone,
                        DebtAmount = Math.Max(0m, purchases - paid),
                        LastCreditPurchaseDate = purchasesBySupplier.TryGetValue(s.Id, out var px) ? px.LastDate : null,
                        LastPaymentDate = paymentsBySupplier.TryGetValue(s.Id, out var rx) ? rx.LastDate : null
                    };
                })
                .Where(r => r.DebtAmount > 0)
                .OrderByDescending(r => r.DebtAmount)
                .ThenBy(r => r.SupplierName)
                .ToList();

            var vm = new SupplierDebtReportVM
            {
                Search = search,
                Rows = rows,
                TotalDebt = rows.Sum(r => r.DebtAmount)
            };

            return View(vm);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Statement(Guid id, DateOnly? from, DateOnly? to)
        {
            var supplier = await _context.Set<Supplier>()
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => new { s.Id, s.Name, s.Phone })
                .FirstOrDefaultAsync();

            if (supplier == null)
                return View("NotFound");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);
            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var purchasesBefore = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.SupplierId == supplier.Id)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .Where(i => i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalDinar) ?? 0m;

            var paymentsBefore = await _context.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(r => r.SupplierId == supplier.Id)
                .Where(r => r.PaymentDate < fromDate)
                .SumAsync(r => (decimal?)r.Amount) ?? 0m;

            var openingBalance = purchasesBefore - paymentsBefore;

            var purchaseRows = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.SupplierId == supplier.Id)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
                .Select(i => new
                {
                    // Old behavior: statement used Created datetime; accounting should use invoice date.
                    AtDate = i.InvoiceDate,
                    Ref = i.Number,
                    Debit = i.TotalDinar,
                    Credit = 0m,
                    Note = i.Note
                })
                .ToListAsync();

            var paymentRows = await _context.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(r => r.SupplierId == supplier.Id)
                .Where(r => r.PaymentDate >= fromDate && r.PaymentDate <= toDate)
                .Select(r => new
                {
                    // Old behavior: statement used Created datetime and generic reference text.
                    AtDate = r.PaymentDate,
                    Ref = r.Number,
                    Debit = 0m,
                    Credit = r.Amount,
                    Note = r.Note
                })
                .ToListAsync();

            var merged = purchaseRows
                .Select(x => new SupplierStatementEntryVM
                {
                    At = x.AtDate.ToDateTime(TimeOnly.MinValue),
                    Reference = x.Ref,
                    Debit = x.Debit,
                    Credit = x.Credit,
                    Note = x.Note
                })
                .Concat(paymentRows.Select(x => new SupplierStatementEntryVM
                {
                    At = x.AtDate.ToDateTime(TimeOnly.MinValue),
                    Reference = x.Ref,
                    Debit = x.Debit,
                    Credit = x.Credit,
                    Note = x.Note
                }))
                .OrderBy(x => x.At)
                .ToList();

            var running = openingBalance;
            foreach (var row in merged)
            {
                running += row.Debit;
                running -= row.Credit;
                row.RunningBalance = running;
            }

            var vm = new SupplierStatementVM
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                Phone = supplier.Phone,
                From = fromDate,
                To = toDate,
                OpeningBalance = openingBalance,
                ClosingBalance = running,
                Entries = merged
            };

            return View(vm);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Pay(Guid supplierId, string? returnUrl = null)
        {
            var supplier = await _context.Set<Supplier>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId);
            if (supplier == null)
                return View("NotFound");

            ViewBag.SupplierName = supplier.Name;
            ViewBag.ReturnUrl = returnUrl;
            return View(new SupplierPayment
            {
                SupplierId = supplierId,
                PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentMethod = "Cash"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Pay(SupplierPayment model, string? returnUrl = null)
        {
            if (TryResolvePaymentDateFromRequest(out var resolvedPaymentDate))
            {
                model.PaymentDate = resolvedPaymentDate;
                ModelState.Remove(nameof(model.PaymentDate));
            }

            if (TryResolveAmountFromRequest(out var resolvedAmount))
            {
                model.Amount = resolvedAmount;
                ModelState.Remove(nameof(model.Amount));
            }

            model.PaymentMethod = NormalizePaymentMethod(model.PaymentMethod);
            model.Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
            model.Number = string.Empty;
            model.CreatedByUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            model.CreatedByUserName = User?.Identity?.Name;

            var supplier = await _context.Set<Supplier>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == model.SupplierId);
            if (supplier == null)
                return View("NotFound");

            ViewBag.SupplierName = supplier.Name;
            ViewBag.ReturnUrl = returnUrl;

            if (model.Amount <= 0)
                ModelState.AddModelError(nameof(model.Amount), "قيمة السداد يجب أن تكون أكبر من صفر.");

            if (!ModelState.IsValid)
            {
                var errorMessage = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                    ?? "تعذر حفظ السداد. تحقق من صحة التاريخ والمبلغ.";

                TempData["ErrorMessage"] = errorMessage;
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return View(model);
            }

            model.Number = await GenerateSupplierPaymentNumberAsync(model.PaymentDate);

            _context.Set<SupplierPayment>().Add(model);

            AddFinancialEntriesForSupplierPayment(model);

            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Create",
                EntityType = "SupplierPayment",
                EntityId = model.Id,
                EntityNumber = model.Number,
                Description = $"Supplier payment amount: {model.Amount:0.00}, supplier: {model.SupplierId}",
                CreatedByUserId = model.CreatedByUserId,
                CreatedByUserName = model.CreatedByUserName
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل السداد بنجاح.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Statement), new { id = model.SupplierId });
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return "Cash";

            if (string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return "Transfer";

            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase))
                return "Card";

            return "Cash";
        }

        private bool TryResolvePaymentDateFromRequest(out DateOnly paymentDate)
        {
            paymentDate = default;

            var rawDate = Request.Form["PaymentDate"].FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(rawDate))
                return false;

            var acceptedFormats = new[]
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "M/d/yyyy"
            };

            return DateOnly.TryParseExact(rawDate, acceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out paymentDate)
                || DateOnly.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out paymentDate)
                || DateOnly.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out paymentDate);
        }

        private bool TryResolveAmountFromRequest(out decimal amount)
        {
            amount = 0m;

            var rawAmount = Request.Form["Amount"].FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(rawAmount))
                return false;

            return decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
                || decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                || decimal.TryParse(rawAmount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                || decimal.TryParse(rawAmount.Replace('.', ','), NumberStyles.Number, CultureInfo.GetCultureInfo("ar-IQ"), out amount);
        }

        private void AddFinancialEntriesForSupplierPayment(SupplierPayment payment)
        {
            var amount = Math.Round(payment.Amount, 2, MidpointRounding.ToEven);
            if (amount <= 0)
                return;

            var (creditCode, creditName) = ResolveSupplierPaymentCreditAccount(payment.PaymentMethod);

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = payment.PaymentDate,
                SourceType = JournalSourceType,
                SourceId = payment.Id,
                DocumentNo = payment.Number,
                AccountCode = AccountSupplierCode,
                AccountName = AccountSupplierName,
                Debit = amount,
                Credit = 0m,
                Note = payment.Note,
                CreatedByUserId = payment.CreatedByUserId,
                CreatedByUserName = payment.CreatedByUserName
            });

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = payment.PaymentDate,
                SourceType = JournalSourceType,
                SourceId = payment.Id,
                DocumentNo = payment.Number,
                AccountCode = creditCode,
                AccountName = creditName,
                Debit = 0m,
                Credit = amount,
                Note = payment.Note,
                CreatedByUserId = payment.CreatedByUserId,
                CreatedByUserName = payment.CreatedByUserName
            });
        }

        private static (string Code, string Name) ResolveSupplierPaymentCreditAccount(string? paymentMethod)
        {
            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return (AccountBankCode, AccountBankName);

            return (AccountCashCode, AccountCashName);
        }

        private async Task<string> GenerateSupplierPaymentNumberAsync(DateOnly paymentDate)
        {
            var prefix = $"SP-{paymentDate:yyyyMMdd}-";

            var lastNumber = await _context.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(p => p.Number.StartsWith(prefix))
                .OrderByDescending(p => p.Number)
                .Select(p => p.Number)
                .FirstOrDefaultAsync();

            var nextSequence = 1;
            if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.Length >= prefix.Length + 4)
            {
                var suffix = lastNumber.Substring(prefix.Length);
                if (int.TryParse(suffix, out var parsed) && parsed >= 1)
                    nextSequence = parsed + 1;
            }

            return $"{prefix}{nextSequence:0000}";
        }
    }
}
