using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Expenses;
using System.Security.Claims;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class ExpensesController : Controller
    {
        private const string SalaryKind = "Salary";
        private const string AdvanceKind = "Advance";
        private const string AdvanceSettlementKind = "AdvanceSettlement";
        private const string DeductionKind = "Deduction";
        private const string CommissionWithdrawalKind = "CommissionWithdrawal";
        private const string JournalSourceType = "ExpenseEntry";

        private const string AccountCashCode = "1101";
        private const string AccountCashName = "الصندوق";
        private const string AccountBankCode = "1102";
        private const string AccountBankName = "البنك";
        private const string AccountGeneralExpenseCode = "5101";
        private const string AccountGeneralExpenseName = "مصروفات عامة";
        private const string AccountSalaryExpenseCode = "5102";
        private const string AccountSalaryExpenseName = "مصروفات الرواتب";
        private const string AccountEmployeeAdvanceCode = "1202";
        private const string AccountEmployeeAdvanceName = "سلف الموظفين";
        private const string AccountPayrollClearingCode = "2102";
        private const string AccountPayrollClearingName = "تسويات رواتب";

        private static readonly string[] AllowedKinds = { "General", SalaryKind, AdvanceKind, DeductionKind, CommissionWithdrawalKind };
        private static readonly string[] AllowedPaymentMethods = { "Cash", "Card", "Transfer", "Internal" };

        private readonly AppDbContext _context;

        public ExpensesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, string? expenseKind, Guid? employeeId, string? search)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            if (HttpContext != null)
                return RedirectToAction("CashierPerformance", "PosShifts", new { from, to, employeeId });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);
            if (fromDate > toDate)
            {
                var t = fromDate;
                fromDate = toDate;
                toDate = t;
            }

            var normalizedKind = NormalizeKind(expenseKind);

            var query = _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Include(e => e.Employee)
                .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate);

            if (!string.IsNullOrWhiteSpace(normalizedKind))
            {
                query = query.Where(e => e.ExpenseKind == normalizedKind);
            }

            if (employeeId.HasValue)
            {
                query = query.Where(e => e.EmployeeId == employeeId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    e.Category.Contains(term) ||
                    (e.Note != null && e.Note.Contains(term)) ||
                    (e.ReferenceNo != null && e.ReferenceNo.Contains(term)) ||
                    (e.Employee != null && e.Employee.Name.Contains(term)));
            }

            var rows = await query
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.Created)
                .Select(e => new ExpenseEntryRowVM
                {
                    Id = e.Id,
                    ExpenseDate = e.ExpenseDate,
                    ExpenseKind = e.ExpenseKind,
                    Category = e.Category,
                    EmployeeName = e.Employee != null ? e.Employee.Name : null,
                    PaymentMethod = e.PaymentMethod,
                    Amount = e.Amount,
                    ReferenceNo = e.ReferenceNo,
                    Note = e.Note,
                    Created = e.Created,
                    CreatedByUserName = e.CreatedByUserName
                })
                .ToListAsync();

            var vm = new ExpenseEntryIndexVM
            {
                From = fromDate,
                To = toDate,
                ExpenseKind = normalizedKind,
                EmployeeId = employeeId,
                Search = search,
                GeneralExpensesTotal = Round2(rows.Where(r => r.ExpenseKind == "General").Sum(r => r.Amount)),
                SalariesTotal = Round2(rows.Where(r => r.ExpenseKind == SalaryKind).Sum(r => r.Amount)),
                AdvancesTotal = Round2(rows.Where(r => r.ExpenseKind == AdvanceKind).Sum(r => r.Amount)),
                SettlementsTotal = Round2(rows.Where(r => r.ExpenseKind == AdvanceSettlementKind).Sum(r => r.Amount)),
                OverallTotal = Round2(rows.Where(r => r.ExpenseKind == "General" || r.ExpenseKind == SalaryKind || r.ExpenseKind == AdvanceKind).Sum(r => r.Amount)),
                Rows = rows
            };

            await PopulateLookupsAsync(employeeId, normalizedKind, null);
            return View(vm);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> EmployeeSummary(DateOnly? from, DateOnly? to, Guid? employeeId)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);
            if (fromDate > toDate)
            {
                var t = fromDate;
                fromDate = toDate;
                toDate = t;
            }

            var query = _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Include(e => e.Employee)
                .Where(e => e.EmployeeId != null)
                .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
                .Where(e => e.ExpenseKind == SalaryKind || e.ExpenseKind == AdvanceKind || e.ExpenseKind == AdvanceSettlementKind);

            if (employeeId.HasValue)
            {
                query = query.Where(e => e.EmployeeId == employeeId);
            }

            var entries = await query
                .Select(e => new
                {
                    EmployeeId = e.EmployeeId!.Value,
                    EmployeeName = e.Employee != null ? e.Employee.Name : "-",
                    e.ExpenseKind,
                    e.Amount,
                    e.ExpenseDate
                })
                .ToListAsync();

            var rows = entries
                .GroupBy(e => new { e.EmployeeId, e.EmployeeName })
                .Select(g =>
                {
                    var salaries = Round2(g.Where(x => x.ExpenseKind == SalaryKind).Sum(x => x.Amount));
                    var advances = Round2(g.Where(x => x.ExpenseKind == AdvanceKind).Sum(x => x.Amount));
                    var settlements = Round2(g.Where(x => x.ExpenseKind == AdvanceSettlementKind).Sum(x => x.Amount));
                    var outstanding = Round2(Math.Max(0m, advances - settlements));
                    return new EmployeeExpenseSummaryRowVM
                    {
                        EmployeeId = g.Key.EmployeeId,
                        EmployeeName = g.Key.EmployeeName,
                        SalariesTotal = salaries,
                        AdvancesTotal = advances,
                        SettlementsTotal = settlements,
                        OutstandingAdvanceBalance = outstanding,
                        NetDue = Round2(salaries - outstanding),
                        LastMovementDate = g.Max(x => x.ExpenseDate)
                    };
                })
                .OrderByDescending(r => r.NetDue)
                .ThenBy(r => r.EmployeeName)
                .ToList();

            var vm = new EmployeeExpenseSummaryVM
            {
                From = fromDate,
                To = toDate,
                EmployeeId = employeeId,
                TotalSalaries = Round2(rows.Sum(r => r.SalariesTotal)),
                TotalAdvances = Round2(rows.Sum(r => r.AdvancesTotal)),
                TotalSettlements = Round2(rows.Sum(r => r.SettlementsTotal)),
                TotalOutstandingAdvances = Round2(rows.Sum(r => r.OutstandingAdvanceBalance)),
                TotalNetDue = Round2(rows.Sum(r => r.NetDue)),
                Rows = rows
            };

            var employees = await _context.Set<Employee>()
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            ViewBag.Employees = new SelectList(employees, "Id", "Name", employeeId);

            return View(vm);
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Create()
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            await PopulateLookupsAsync(null, "General", "Cash");
            return View(new ExpenseEntryFormVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> Create(ExpenseEntryFormVM vm)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            vm.ExpenseKind = NormalizeKind(vm.ExpenseKind) ?? "General";
            vm.PaymentMethod = NormalizePaymentMethod(vm.PaymentMethod);
            vm.Category = vm.Category?.Trim() ?? string.Empty;
            vm.ReferenceNo = string.IsNullOrWhiteSpace(vm.ReferenceNo) ? null : vm.ReferenceNo.Trim();
            vm.Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim();

            ValidateEmployeeRequirement(vm);

            var outstandingAdvance = await GetOutstandingAdvanceBalanceAsync(vm.EmployeeId, null);
            var maxDeduction = Round2(Math.Min(vm.Amount, outstandingAdvance));
            NormalizeSettlementFields(vm, maxDeduction);
            ValidateSettlement(vm, maxDeduction);

            if (!ModelState.IsValid)
            {
                ViewBag.OutstandingAdvance = outstandingAdvance;
                ViewBag.SuggestedDeduction = maxDeduction;
                await PopulateLookupsAsync(vm.EmployeeId, vm.ExpenseKind, vm.PaymentMethod);
                return View(vm);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var trx = await _context.Database.BeginTransactionAsync();

                var entity = new ExpenseEntry
                {
                    ExpenseDate = vm.ExpenseDate,
                    Amount = vm.Amount,
                    ExpenseKind = vm.ExpenseKind,
                    Category = vm.Category,
                    EmployeeId = vm.EmployeeId,
                    PaymentMethod = vm.PaymentMethod,
                    ReferenceNo = vm.ReferenceNo,
                    Note = vm.Note,
                    CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedByUserName = User.Identity?.Name,
                    PosShiftId = await GetOpenShiftIdForCurrentUserAsync()
                };

                _context.Set<ExpenseEntry>().Add(entity);
                await _context.SaveChangesAsync();

                AddFinancialEntriesForExpense(entity);

                await UpsertLinkedSettlementAsync(entity, vm.AdvanceDeduction);

                await _context.SaveChangesAsync();

                await trx.CommitAsync();

                return RedirectToAction(nameof(Index));
            });
        }

        [HttpGet]
        [Authorize(Policy = "InventoryEditPolicy")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            var entity = await _context.Set<ExpenseEntry>().FindAsync(id);
            if (entity == null)
            {
                return View("NotFound");
            }

            var vm = new ExpenseEntryFormVM
            {
                Id = entity.Id,
                ExpenseDate = entity.ExpenseDate,
                Amount = entity.Amount,
                ExpenseKind = entity.ExpenseKind,
                Category = entity.Category,
                EmployeeId = entity.EmployeeId,
                PaymentMethod = entity.PaymentMethod,
                ReferenceNo = entity.ReferenceNo,
                Note = entity.Note
            };

            var linkedSettlement = await GetLinkedSettlementAsync(entity.Id);
            if (linkedSettlement != null)
            {
                vm.ApplyAdvanceSettlement = true;
                vm.AdvanceDeduction = linkedSettlement.Amount;
            }

            var outstandingAdvance = await GetOutstandingAdvanceBalanceAsync(vm.EmployeeId, vm.Id);
            ViewBag.OutstandingAdvance = outstandingAdvance;
            ViewBag.SuggestedDeduction = Round2(Math.Min(vm.Amount, outstandingAdvance));

            await PopulateLookupsAsync(vm.EmployeeId, vm.ExpenseKind, vm.PaymentMethod);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryEditPolicy")]
        public async Task<IActionResult> Edit(Guid id, ExpenseEntryFormVM vm)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            if (id != vm.Id)
            {
                return View("NotFound");
            }

            vm.ExpenseKind = NormalizeKind(vm.ExpenseKind) ?? "General";
            vm.PaymentMethod = NormalizePaymentMethod(vm.PaymentMethod);
            vm.Category = vm.Category?.Trim() ?? string.Empty;
            vm.ReferenceNo = string.IsNullOrWhiteSpace(vm.ReferenceNo) ? null : vm.ReferenceNo.Trim();
            vm.Note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim();

            ValidateEmployeeRequirement(vm);

            var outstandingAdvance = await GetOutstandingAdvanceBalanceAsync(vm.EmployeeId, vm.Id);
            var maxDeduction = Round2(Math.Min(vm.Amount, outstandingAdvance));
            NormalizeSettlementFields(vm, maxDeduction);
            ValidateSettlement(vm, maxDeduction);

            if (!ModelState.IsValid)
            {
                ViewBag.OutstandingAdvance = outstandingAdvance;
                ViewBag.SuggestedDeduction = maxDeduction;
                await PopulateLookupsAsync(vm.EmployeeId, vm.ExpenseKind, vm.PaymentMethod);
                return View(vm);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var trx = await _context.Database.BeginTransactionAsync();

                var entity = await _context.Set<ExpenseEntry>().FindAsync(id);
                if (entity == null)
                    return View("NotFound");

                entity.ExpenseDate = vm.ExpenseDate;
                entity.Amount = vm.Amount;
                entity.ExpenseKind = vm.ExpenseKind;
                entity.Category = vm.Category;
                entity.EmployeeId = vm.EmployeeId;
                entity.PaymentMethod = vm.PaymentMethod;
                entity.ReferenceNo = vm.ReferenceNo;
                entity.Note = vm.Note;
                entity.PosShiftId ??= await GetOpenShiftIdForCurrentUserAsync();

                await RemoveFinancialEntriesAsync(entity.Id);
                AddFinancialEntriesForExpense(entity);

                await _context.SaveChangesAsync();

                await UpsertLinkedSettlementAsync(entity, vm.AdvanceDeduction);

                await _context.SaveChangesAsync();

                await trx.CommitAsync();
                return RedirectToAction(nameof(Index));
            });
        }

        [HttpGet]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> EmployeeAdvanceBalance(Guid? employeeId, Guid? ignoreExpenseId)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            var balance = await GetOutstandingAdvanceBalanceAsync(employeeId, ignoreExpenseId);
            return Json(new { success = true, balance = Round2(balance) });
        }

        [HttpGet]
        [Authorize(Policy = "InventoryDeletePolicy")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            var entity = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Include(e => e.Employee)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entity == null)
            {
                return View("NotFound");
            }

            return View(entity);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryDeletePolicy")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var deny = DenyCashierAccess();
            if (deny != null) return deny;

            var entity = await _context.Set<ExpenseEntry>().FindAsync(id);
            if (entity == null)
            {
                return View("NotFound");
            }

            var linkedSettlement = await GetLinkedSettlementAsync(entity.Id);

            if (linkedSettlement != null)
            {
                await RemoveFinancialEntriesAsync(linkedSettlement.Id);
                _context.Set<ExpenseEntry>().Remove(linkedSettlement);
            }

            await RemoveFinancialEntriesAsync(entity.Id);
            _context.Set<ExpenseEntry>().Remove(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLookupsAsync(Guid? employeeId, string? selectedExpenseKind, string? selectedPaymentMethod)
        {
            var employees = await _context.Set<Employee>()
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            ViewBag.Employees = new SelectList(employees, "Id", "Name", employeeId);
            ViewBag.ExpenseKinds = new SelectList(new[]
            {
                new { Value = "General", Text = "مصروف عام" },
                new { Value = "Salary", Text = "مرتب موظف" },
                new { Value = "Advance", Text = "سلفة موظف" },
                new { Value = "CommissionWithdrawal", Text = "سحب عمولة" },
                new { Value = "Deduction", Text = "خصم موظف" }
            }, "Value", "Text", selectedExpenseKind);
            ViewBag.PaymentMethods = new SelectList(new[]
            {
                new { Value = "Cash", Text = "نقدي" },
                new { Value = "Card", Text = "بطاقة" },
                new { Value = "Transfer", Text = "تحويل" }
            }, "Value", "Text", selectedPaymentMethod);
        }

        private static string? NormalizeKind(string? kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return null;
            var v = kind.Trim();
            return AllowedKinds.FirstOrDefault(k => k.Equals(v, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizePaymentMethod(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Cash";
            var normalized = AllowedPaymentMethods.FirstOrDefault(k => k.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
            return normalized ?? "Cash";
        }

        private void ValidateEmployeeRequirement(ExpenseEntryFormVM vm)
        {
            if ((vm.ExpenseKind == SalaryKind ||
                 vm.ExpenseKind == AdvanceKind ||
                 vm.ExpenseKind == DeductionKind ||
                 vm.ExpenseKind == CommissionWithdrawalKind) && !vm.EmployeeId.HasValue)
            {
                ModelState.AddModelError(nameof(vm.EmployeeId), "يجب اختيار الموظف عند تسجيل عملية مالية خاصة به.");
            }

            if (vm.ExpenseKind == "General")
            {
                vm.EmployeeId = null;
            }
        }

        private void NormalizeSettlementFields(ExpenseEntryFormVM vm, decimal maxDeduction)
        {
            if (vm.ExpenseKind != SalaryKind)
            {
                vm.ApplyAdvanceSettlement = false;
                vm.AdvanceDeduction = 0;
                return;
            }

            if (!vm.ApplyAdvanceSettlement)
            {
                vm.AdvanceDeduction = 0;
                return;
            }

            if (vm.AdvanceDeduction < 0)
            {
                vm.AdvanceDeduction = 0;
            }

            if (vm.AdvanceDeduction > maxDeduction)
            {
                vm.AdvanceDeduction = maxDeduction;
            }
        }

        private void ValidateSettlement(ExpenseEntryFormVM vm, decimal maxDeduction)
        {
            if (vm.ExpenseKind != SalaryKind || !vm.ApplyAdvanceSettlement)
            {
                return;
            }

            if (!vm.EmployeeId.HasValue)
            {
                ModelState.AddModelError(nameof(vm.EmployeeId), "يجب اختيار الموظف قبل تسوية السلفة.");
                return;
            }

            if (vm.AdvanceDeduction <= 0)
            {
                ModelState.AddModelError(nameof(vm.AdvanceDeduction), "قيمة الخصم يجب أن تكون أكبر من صفر.");
                return;
            }

            if (vm.AdvanceDeduction > maxDeduction)
            {
                ModelState.AddModelError(nameof(vm.AdvanceDeduction), "قيمة الخصم تتجاوز الحد المسموح (رصيد السلف أو قيمة المرتب).");
            }
        }

        private async Task<decimal> GetOutstandingAdvanceBalanceAsync(Guid? employeeId, Guid? ignoreSalaryExpenseId)
        {
            if (!employeeId.HasValue)
            {
                return 0m;
            }

            var employee = employeeId.Value;

            var advances = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.EmployeeId == employee && e.ExpenseKind == AdvanceKind)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            var settlementsQuery = _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.EmployeeId == employee && e.ExpenseKind == AdvanceSettlementKind);

            if (ignoreSalaryExpenseId.HasValue)
            {
                var refNo = BuildSettlementRef(ignoreSalaryExpenseId.Value);
                settlementsQuery = settlementsQuery.Where(e => e.ReferenceNo != refNo);
            }

            var settlements = await settlementsQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;

            return Math.Max(0m, advances - settlements);
        }

        private async Task<ExpenseEntry?> GetLinkedSettlementAsync(Guid salaryExpenseId)
        {
            var refNo = BuildSettlementRef(salaryExpenseId);
            return await _context.Set<ExpenseEntry>()
                .FirstOrDefaultAsync(e => e.ExpenseKind == AdvanceSettlementKind && e.ReferenceNo == refNo);
        }

        private async Task UpsertLinkedSettlementAsync(ExpenseEntry salaryEntry, decimal deductionAmount)
        {
            var linkedSettlement = await GetLinkedSettlementAsync(salaryEntry.Id);

            if (salaryEntry.ExpenseKind != SalaryKind || !salaryEntry.EmployeeId.HasValue || deductionAmount <= 0)
            {
                if (linkedSettlement != null)
                {
                    await RemoveFinancialEntriesAsync(linkedSettlement.Id);
                    _context.Set<ExpenseEntry>().Remove(linkedSettlement);
                }

                return;
            }

            var referenceNo = BuildSettlementRef(salaryEntry.Id);
            var settlementNote = $"تسوية سلفة من مرتب بتاريخ {salaryEntry.ExpenseDate:yyyy-MM-dd}";

            if (linkedSettlement == null)
            {
                linkedSettlement = new ExpenseEntry
                {
                    ExpenseDate = salaryEntry.ExpenseDate,
                    Amount = deductionAmount,
                    ExpenseKind = AdvanceSettlementKind,
                    Category = "تسوية سلفة",
                    EmployeeId = salaryEntry.EmployeeId,
                    PaymentMethod = "Internal",
                    ReferenceNo = referenceNo,
                    Note = settlementNote,
                    CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedByUserName = User.Identity?.Name
                };
                _context.Set<ExpenseEntry>().Add(linkedSettlement);
                await _context.SaveChangesAsync();
                AddFinancialEntriesForExpense(linkedSettlement);
            }
            else
            {
                linkedSettlement.ExpenseDate = salaryEntry.ExpenseDate;
                linkedSettlement.Amount = deductionAmount;
                linkedSettlement.EmployeeId = salaryEntry.EmployeeId;
                linkedSettlement.Note = settlementNote;

                await RemoveFinancialEntriesAsync(linkedSettlement.Id);
                AddFinancialEntriesForExpense(linkedSettlement);
            }
        }

        private async Task RemoveFinancialEntriesAsync(Guid expenseId)
        {
            var entries = await _context.Set<FinJournalEntry>()
                .Where(e => e.SourceType == JournalSourceType && e.SourceId == expenseId)
                .ToListAsync();

            if (entries.Count > 0)
                _context.Set<FinJournalEntry>().RemoveRange(entries);
        }

        private void AddFinancialEntriesForExpense(ExpenseEntry expense)
        {
            var amount = Round2(expense.Amount);
            if (amount <= 0)
                return;

            if (string.Equals(expense.ExpenseKind, DeductionKind, StringComparison.OrdinalIgnoreCase))
                return;

            var documentNo = !string.IsNullOrWhiteSpace(expense.ReferenceNo)
                ? expense.ReferenceNo
                : $"EX-{expense.ExpenseDate:yyyyMMdd}-{expense.Id.ToString("N")[..6].ToUpperInvariant()}";

            var (debitCode, debitName) = ResolveExpenseDebitAccount(expense.ExpenseKind);
            var (creditCode, creditName) = ResolveExpenseCreditAccount(expense.ExpenseKind, expense.PaymentMethod);

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = expense.ExpenseDate,
                SourceType = JournalSourceType,
                SourceId = expense.Id,
                DocumentNo = documentNo,
                AccountCode = debitCode,
                AccountName = debitName,
                Debit = amount,
                Credit = 0m,
                Note = expense.Note,
                CreatedByUserId = expense.CreatedByUserId,
                CreatedByUserName = expense.CreatedByUserName
            });

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = expense.ExpenseDate,
                SourceType = JournalSourceType,
                SourceId = expense.Id,
                DocumentNo = documentNo,
                AccountCode = creditCode,
                AccountName = creditName,
                Debit = 0m,
                Credit = amount,
                Note = expense.Note,
                CreatedByUserId = expense.CreatedByUserId,
                CreatedByUserName = expense.CreatedByUserName
            });
        }

        private static (string Code, string Name) ResolveExpenseDebitAccount(string? expenseKind)
        {
            if (string.Equals(expenseKind, SalaryKind, StringComparison.OrdinalIgnoreCase))
                return (AccountSalaryExpenseCode, AccountSalaryExpenseName);

            if (string.Equals(expenseKind, AdvanceKind, StringComparison.OrdinalIgnoreCase))
                return (AccountEmployeeAdvanceCode, AccountEmployeeAdvanceName);

            if (string.Equals(expenseKind, CommissionWithdrawalKind, StringComparison.OrdinalIgnoreCase))
                return (AccountEmployeeAdvanceCode, AccountEmployeeAdvanceName);

            if (string.Equals(expenseKind, AdvanceSettlementKind, StringComparison.OrdinalIgnoreCase))
                return (AccountPayrollClearingCode, AccountPayrollClearingName);

            return (AccountGeneralExpenseCode, AccountGeneralExpenseName);
        }

        private static (string Code, string Name) ResolveExpenseCreditAccount(string? expenseKind, string? paymentMethod)
        {
            if (string.Equals(expenseKind, AdvanceSettlementKind, StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "Internal", StringComparison.OrdinalIgnoreCase))
                return (AccountEmployeeAdvanceCode, AccountEmployeeAdvanceName);

            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return (AccountBankCode, AccountBankName);

            return (AccountCashCode, AccountCashName);
        }

        private static string BuildSettlementRef(Guid salaryExpenseId)
            => $"SAL:{salaryExpenseId:N}";

        private static decimal Round2(decimal value)
            => Math.Round(value, 2, MidpointRounding.ToEven);

        private IActionResult? DenyCashierAccess()
        {
            if (User.IsInRole("Cashier"))
            {
                return Forbid();
            }

            return null;
        }

        private async Task<Guid?> GetOpenShiftIdForCurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedByUserId == userId && s.Status == "Open")
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }
    }
}
