using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels.Customers;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class CustomersController : Controller
    {
        private const string DailySalesCustomerName = "مبيعات يومية";
        private const string StatusOpen = "Open";
        private const string StatusClosed = "Closed";
        private const string JournalSourceType = "CustomerReceipt";
        private const string AccountCashCode = "1101";
        private const string AccountCashName = "الصندوق";
        private const string AccountBankCode = "1102";
        private const string AccountBankName = "البنك";
        private const string AccountCustomerCode = "1201";
        private const string AccountCustomerName = "ذمم العملاء";
        private static readonly Guid DailySalesCustomerSeedId = Guid.Parse("7e2efb6c-0cb2-430f-92af-6e0ad720f105");

        private readonly IUnitOfWork<Customer> _customers;
        private readonly AppDbContext _context;

        public CustomersController(IUnitOfWork<Customer> customers, AppDbContext context)
        {
            _customers = customers;
            _context = context;
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Index(string? search)
        {
            var query = _customers.Repository.GetAll()
                .Where(c => c.Id != DailySalesCustomerSeedId && c.Name != DailySalesCustomerName);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
            }

            var list = await query.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Search = search;
            return View(list);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public IActionResult Create(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Create(Customer customer, string? returnUrl = null)
        {
            customer.Name = customer.Name?.Trim() ?? string.Empty;
            customer.Phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();
            customer.Note = string.IsNullOrWhiteSpace(customer.Note) ? null : customer.Note.Trim();
            ViewBag.ReturnUrl = returnUrl;

            if (string.Equals(customer.Name, DailySalesCustomerName, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(customer.Name), "هذا الاسم محجوز لعميل النظام.");
            }

            if (!ModelState.IsValid)
                return View(customer);

            var exists = await _customers.Repository.GetAll().AnyAsync(c => c.Name == customer.Name);
            if (exists)
            {
                ViewBag.Message = $"العميل '{customer.Name}' موجود بالفعل.";
                return View(customer);
            }

            _customers.Repository.Insert(customer);
            await _customers.SaveAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                var target = returnUrl.Replace("__customerId__", customer.Id.ToString(), StringComparison.Ordinal);
                if (Url.IsLocalUrl(target))
                    return Redirect(target);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var customer = await _customers.Repository.GetByIdAsync(id);
            if (customer == null)
                return View("NotFound");

            if (customer.Id == DailySalesCustomerSeedId || string.Equals(customer.Name, DailySalesCustomerName, StringComparison.Ordinal))
                return View("NotFound");

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> Edit(Guid id, Customer customer)
        {
            if (id != customer.Id)
                return View("NotFound");

            customer.Name = customer.Name?.Trim() ?? string.Empty;
            customer.Phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();
            customer.Note = string.IsNullOrWhiteSpace(customer.Note) ? null : customer.Note.Trim();

            if (customer.Id == DailySalesCustomerSeedId || string.Equals(customer.Name, DailySalesCustomerName, StringComparison.Ordinal))
            {
                return View("NotFound");
            }

            if (!ModelState.IsValid)
                return View(customer);

            var exists = await _customers.Repository.GetAll().AnyAsync(c => c.Name == customer.Name && c.Id != customer.Id);
            if (exists)
            {
                ViewBag.Message = $"العميل '{customer.Name}' موجود بالفعل.";
                return View(customer);
            }

            _customers.Repository.Update(customer);
            await _customers.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var customer = await _customers.Repository.GetByIdAsync(id);
            if (customer == null)
                return View("NotFound");

            if (customer.Id == DailySalesCustomerSeedId || string.Equals(customer.Name, DailySalesCustomerName, StringComparison.Ordinal))
                return View("NotFound");

            var usage = await GetCustomerUsageAsync(customer.Id);
            ViewBag.DeleteBlockedReason = usage.BlockedReason;
            ViewBag.CanDelete = string.IsNullOrWhiteSpace(usage.BlockedReason);

            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var customer = await _customers.Repository.GetByIdAsync(id);
            if (customer == null)
                return View("NotFound");

            if (customer.Id == DailySalesCustomerSeedId || string.Equals(customer.Name, DailySalesCustomerName, StringComparison.Ordinal))
                return View("NotFound");

            var usage = await GetCustomerUsageAsync(customer.Id);
            if (!string.IsNullOrWhiteSpace(usage.BlockedReason))
            {
                ViewBag.DeleteBlockedReason = usage.BlockedReason;
                ViewBag.CanDelete = false;
                return View("Delete", customer);
            }

            try
            {
                customer.IsDeleted = true;
                customer.DeletedAt = DateTime.UtcNow;
                await _customers.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ViewBag.Message = "تعذر تعطيل العميل (حدث خطأ).";
                return View("Delete", customer);
            }
        }

        private async Task<(string? BlockedReason, int SalesInvoices, int Receipts)> GetCustomerUsageAsync(Guid customerId)
        {
            var sales = await _context.Set<SalesInvoice>().IgnoreQueryFilters().CountAsync(i => i.CustomerId == customerId);
            var receipts = await _context.Set<CustomerReceipt>().IgnoreQueryFilters().CountAsync(r => r.CustomerId == customerId);

            var blockers = new List<string>();
            if (sales > 0) blockers.Add($"{sales} invoice(s)");
            if (receipts > 0) blockers.Add($"{receipts} receipt(s)");

            var reason = blockers.Count > 0 ? $"لا يمكن حذف العميل لأنه مرتبط ببيانات تشغيلية موجودة: {string.Join(", ", blockers)}." : null;
            return (reason, sales, receipts);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Debts(string? search)
        {
            var customersQuery = _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id != DailySalesCustomerSeedId && c.Name != DailySalesCustomerName);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                customersQuery = customersQuery.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
            }

            var customers = await customersQuery
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Phone })
                .ToListAsync();

            var customerIds = customers.Select(c => c.Id).ToList();

            var creditSales = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => customerIds.Contains(i.CustomerId ?? Guid.Empty))
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .GroupBy(i => i.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Sum = g.Sum(i => i.TotalDinar),
                    LastDate = g.Max(i => i.InvoiceDate)
                })
                .ToListAsync();

            var payments = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => customerIds.Contains(r.CustomerId))
                .GroupBy(r => r.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Sum = g.Sum(r => r.Amount),
                    LastDate = g.Max(r => r.ReceiptDate)
                })
                .ToListAsync();

            var salesByCustomer = creditSales.ToDictionary(x => x.CustomerId, x => (x.Sum, x.LastDate));
            var paymentsByCustomer = payments.ToDictionary(x => x.CustomerId, x => (x.Sum, x.LastDate));

            var rows = customers
                .Select(c =>
                {
                    var sales = salesByCustomer.TryGetValue(c.Id, out var s) ? s.Sum : 0m;
                    var paid = paymentsByCustomer.TryGetValue(c.Id, out var p) ? p.Sum : 0m;
                    return new CustomerDebtRowVM
                    {
                        CustomerId = c.Id,
                        CustomerName = c.Name,
                        Phone = c.Phone,
                        DebtAmount = Math.Max(0m, sales - paid),
                        LastCreditSaleDate = salesByCustomer.TryGetValue(c.Id, out var sx) ? sx.LastDate : null,
                        LastPaymentDate = paymentsByCustomer.TryGetValue(c.Id, out var px) ? px.LastDate : null
                    };
                })
                .Where(r => r.DebtAmount > 0)
                .OrderByDescending(r => r.DebtAmount)
                .ThenBy(r => r.CustomerName)
                .ToList();

            var vm = new CustomerDebtReportVM
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
            var customer = await _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Where(c => c.Id != DailySalesCustomerSeedId && c.Name != DailySalesCustomerName)
                .Select(c => new { c.Id, c.Name, c.Phone })
                .FirstOrDefaultAsync();

            if (customer == null)
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

            var salesBefore = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.CustomerId == customer.Id)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .Where(i => i.InvoiceDate < fromDate)
                .SumAsync(i => (decimal?)i.TotalDinar) ?? 0m;

            var paymentsBefore = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => r.CustomerId == customer.Id)
                .Where(r => r.ReceiptDate < fromDate)
                .SumAsync(r => (decimal?)r.Amount) ?? 0m;

            var openingBalance = salesBefore - paymentsBefore;

            var paymentRows = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => r.CustomerId == customer.Id)
                .Where(r => r.ReceiptDate >= fromDate && r.ReceiptDate <= toDate)
                .Select(r => new
                {
                    ReceiptId = (Guid?)r.Id,
                    At = r.ReceiptDate.ToDateTime(TimeOnly.MinValue),
                    CreatedAt = r.Created,
                    Ref = "قبض",
                    Debit = 0m,
                    Credit = r.Amount,
                    // PaymentMethod = r.PaymentMethod,
                    PaymentMethod = (string?)r.PaymentMethod,
                    Note = r.Note,
                    IsLockedByClosedShift = r.PosShiftId.HasValue
                        && r.PosShift != null
                        && r.PosShift.Status == StatusClosed
                })
                .ToListAsync();

            var salesRows = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.CustomerId == customer.Id)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .Where(i => i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
                .Select(i => new
                {
                    ReceiptId = (Guid?)null,
                    At = i.InvoiceDate.ToDateTime(TimeOnly.MinValue),
                    CreatedAt = i.Created,
                    Ref = i.Number,
                    Debit = i.TotalDinar,
                    Credit = 0m,
                    PaymentMethod = (string?)null,
                    Note = i.Note,
                    IsLockedByClosedShift = false
                })
                .ToListAsync();

            var rowsRaw = salesRows
                .Concat(paymentRows)
                .OrderBy(x => x.At)
                .ThenBy(x => x.CreatedAt)
                .ToList();

            var runningBalance = openingBalance;
            var rows = new List<CustomerStatementRowVM>(rowsRaw.Count);
            foreach (var row in rowsRaw)
            {
                runningBalance += row.Debit;
                runningBalance -= row.Credit;

                rows.Add(new CustomerStatementRowVM
                {
                    ReceiptId = row.ReceiptId,
                    IsLockedByClosedShift = row.IsLockedByClosedShift,
                    CreatedAtLocal = row.At.ToLocalTime(),
                    Reference = row.Ref,
                    Type = row.Debit > 0 ? "بيع آجل" : $"تحصيل ({ToArabicPaymentMethod(row.PaymentMethod)})",
                    Debit = row.Debit,
                    Credit = row.Credit,
                    Balance = runningBalance,
                    Note = row.Note
                });
            }

            var vm = new CustomerStatementVM
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                Phone = customer.Phone,
                From = fromDate,
                To = toDate,
                OpeningBalance = openingBalance,
                TotalDebit = rows.Sum(r => r.Debit),
                TotalCredit = rows.Sum(r => r.Credit),
                ClosingBalance = runningBalance,
                PaymentInput = new CustomerDebtPaymentInputVM
                {
                    CustomerId = customer.Id,
                    ReceiptDate = today,
                    PaymentMethod = "Cash"
                },
                Rows = rows
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> AddPayment(CustomerDebtPaymentInputVM input)
        {
            var customer = await _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id == input.CustomerId)
                .Where(c => c.Id != DailySalesCustomerSeedId && c.Name != DailySalesCustomerName)
                .Select(c => new { c.Id })
                .FirstOrDefaultAsync();

            if (customer == null)
                return View("NotFound");

            if (input.Amount <= 0)
            {
                TempData["ErrorMessage"] = "قيمة التحصيل يجب أن تكون أكبر من صفر.";
                return RedirectToAction(nameof(Statement), new { id = input.CustomerId });
            }

            var method = (input.PaymentMethod ?? string.Empty).Trim();
            if (!string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "Card", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "Transfer", StringComparison.OrdinalIgnoreCase))
            {
                method = "Cash";
            }

            var openShiftId = await GetOpenShiftIdForCurrentUserAsync();

            var receipt = new CustomerReceipt
            {
                Id = Guid.NewGuid(),
                CustomerId = input.CustomerId,
                ReceiptDate = input.ReceiptDate,
                Amount = Math.Round(input.Amount, 2, MidpointRounding.ToEven),
                PaymentMethod = method,
                Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                CreatedByUserName = User.Identity?.Name,
                PosShiftId = openShiftId
            };

            _context.Set<CustomerReceipt>().Add(receipt);

            AddFinancialEntriesForReceipt(receipt);

            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Create",
                EntityType = "CustomerReceipt",
                EntityId = receipt.Id,
                Description = $"Receipt amount: {receipt.Amount:0.00}, customer: {input.CustomerId}",
                CreatedByUserId = receipt.CreatedByUserId,
                CreatedByUserName = receipt.CreatedByUserName
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل التحصيل بنجاح.";
            return RedirectToAction(nameof(Statement), new { id = input.CustomerId });
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Receipt(Guid id)
        {
            var receipt = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Include(r => r.Customer)
                .Where(r => r.Id == id)
                .Where(r => r.Customer != null)
                .Where(r => r.Customer!.Id != DailySalesCustomerSeedId && r.Customer.Name != DailySalesCustomerName)
                .FirstOrDefaultAsync();

            if (receipt == null)
                return View("NotFound");

            return View(receipt);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> EditPayment(Guid id)
        {
            var receipt = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Include(r => r.Customer)
                .Where(r => r.Id == id)
                .Where(r => r.Customer != null)
                .Where(r => r.Customer!.Id != DailySalesCustomerSeedId && r.Customer.Name != DailySalesCustomerName)
                .FirstOrDefaultAsync();

            if (receipt == null)
                return View("NotFound");

            if (await IsReceiptLockedByClosedShiftAsync(receipt))
            {
                TempData["ErrorMessage"] = "لا يمكن تعديل هذا السند لأنه تابع لوردية مغلقة.";
                return RedirectToAction(nameof(Statement), new { id = receipt.CustomerId });
            }

            return View(new CustomerReceiptEditVM
            {
                ReceiptId = receipt.Id,
                CustomerId = receipt.CustomerId,
                CustomerName = receipt.Customer?.Name ?? "-",
                ReceiptDate = receipt.ReceiptDate,
                Amount = receipt.Amount,
                PaymentMethod = receipt.PaymentMethod,
                Note = receipt.Note,
                IsLockedByClosedShift = false
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> EditPayment(CustomerReceiptEditVM input)
        {
            var receipt = await _context.Set<CustomerReceipt>()
                .Include(r => r.Customer)
                .Where(r => r.Id == input.ReceiptId)
                .Where(r => r.Customer != null)
                .Where(r => r.Customer!.Id != DailySalesCustomerSeedId && r.Customer.Name != DailySalesCustomerName)
                .FirstOrDefaultAsync();

            if (receipt == null)
                return View("NotFound");

            if (await IsReceiptLockedByClosedShiftAsync(receipt))
            {
                TempData["ErrorMessage"] = "لا يمكن تعديل هذا السند لأنه تابع لوردية مغلقة.";
                return RedirectToAction(nameof(Statement), new { id = receipt.CustomerId });
            }

            if (input.Amount <= 0)
            {
                TempData["ErrorMessage"] = "قيمة التحصيل يجب أن تكون أكبر من صفر.";
                return RedirectToAction(nameof(EditPayment), new { id = input.ReceiptId });
            }

            var method = (input.PaymentMethod ?? string.Empty).Trim();
            if (!string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "Card", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "Transfer", StringComparison.OrdinalIgnoreCase))
            {
                method = "Cash";
            }

            receipt.ReceiptDate = input.ReceiptDate;
            receipt.Amount = Math.Round(input.Amount, 2, MidpointRounding.ToEven);
            receipt.PaymentMethod = method;
            receipt.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
            receipt.Modified = DateTime.UtcNow;

            await RemoveFinancialEntriesAsync(receipt.Id);
            AddFinancialEntriesForReceipt(receipt);

            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Edit",
                EntityType = "CustomerReceipt",
                EntityId = receipt.Id,
                Description = $"Receipt updated. Amount: {receipt.Amount:0.00}",
                CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                CreatedByUserName = User.Identity?.Name
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعديل التحصيل بنجاح.";
            return RedirectToAction(nameof(Statement), new { id = receipt.CustomerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOrProgPolicy")]
        public async Task<IActionResult> DeletePayment(Guid id, Guid customerId)
        {
            var receipt = await _context.Set<CustomerReceipt>()
                .Include(r => r.Customer)
                .Where(r => r.Id == id)
                .Where(r => r.Customer != null)
                .Where(r => r.Customer!.Id != DailySalesCustomerSeedId && r.Customer.Name != DailySalesCustomerName)
                .FirstOrDefaultAsync();

            if (receipt == null)
                return View("NotFound");

            if (await IsReceiptLockedByClosedShiftAsync(receipt))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف هذا السند لأنه تابع لوردية مغلقة.";
                return RedirectToAction(nameof(Statement), new { id = receipt.CustomerId });
            }

            await RemoveFinancialEntriesAsync(receipt.Id);
            _context.Set<CustomerReceipt>().Remove(receipt);
            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Delete",
                EntityType = "CustomerReceipt",
                EntityId = receipt.Id,
                Description = $"Receipt deleted. Amount: {receipt.Amount:0.00}",
                CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                CreatedByUserName = User.Identity?.Name
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف التحصيل بنجاح.";
            return RedirectToAction(nameof(Statement), new { id = customerId });
        }

        private void AddFinancialEntriesForReceipt(CustomerReceipt receipt)
        {
            var amount = Math.Round(receipt.Amount, 2, MidpointRounding.ToEven);
            if (amount <= 0)
                return;

            var (debitCode, debitName) = ResolveReceiptDebitAccount(receipt.PaymentMethod);

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = receipt.ReceiptDate,
                SourceType = JournalSourceType,
                SourceId = receipt.Id,
                DocumentNo = $"CR-{receipt.ReceiptDate:yyyyMMdd}-{receipt.Id.ToString("N")[..6].ToUpperInvariant()}",
                AccountCode = debitCode,
                AccountName = debitName,
                Debit = amount,
                Credit = 0m,
                Note = receipt.Note,
                CreatedByUserId = receipt.CreatedByUserId,
                CreatedByUserName = receipt.CreatedByUserName
            });

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = receipt.ReceiptDate,
                SourceType = JournalSourceType,
                SourceId = receipt.Id,
                DocumentNo = $"CR-{receipt.ReceiptDate:yyyyMMdd}-{receipt.Id.ToString("N")[..6].ToUpperInvariant()}",
                AccountCode = AccountCustomerCode,
                AccountName = AccountCustomerName,
                Debit = 0m,
                Credit = amount,
                Note = receipt.Note,
                CreatedByUserId = receipt.CreatedByUserId,
                CreatedByUserName = receipt.CreatedByUserName
            });
        }

        private async Task RemoveFinancialEntriesAsync(Guid receiptId)
        {
            var entries = await _context.Set<FinJournalEntry>()
                .Where(e => e.SourceType == JournalSourceType && e.SourceId == receiptId)
                .ToListAsync();

            if (entries.Count > 0)
                _context.Set<FinJournalEntry>().RemoveRange(entries);
        }

        private static (string Code, string Name) ResolveReceiptDebitAccount(string? paymentMethod)
        {
            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return (AccountBankCode, AccountBankName);

            return (AccountCashCode, AccountCashName);
        }

        private static string ToArabicPaymentMethod(string? paymentMethod)
        {
            var value = (paymentMethod ?? string.Empty).Trim();
            if (value.Equals("Card", StringComparison.OrdinalIgnoreCase))
                return "بطاقة";
            if (value.Equals("Transfer", StringComparison.OrdinalIgnoreCase))
                return "تحويل";
            return "نقدي";
        }

        private async Task<Guid?> GetOpenShiftIdForCurrentUserAsync()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedByUserId == userId && s.Status == StatusOpen)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> IsReceiptLockedByClosedShiftAsync(CustomerReceipt receipt)
        {
            if (!receipt.PosShiftId.HasValue)
                return false;

            return await _context.Set<PosShift>()
                .AsNoTracking()
                .AnyAsync(s => s.Id == receipt.PosShiftId.Value && s.Status == StatusClosed);
        }
    }
}
