using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    /// <summary>
    /// المنطق المحاسبي المركزي للمصروفات والسلف والمرتبات: بناء قيود اليومية المزدوجة،
    /// حساب رصيد السلف، وربط الوردية. يُستخدم من الشاشة الإدارية (ExpensesController)
    /// ومن شاشة الكاشير (PosExpensesController) معاً كمصدر وحيد للحقيقة.
    /// </summary>
    public class ExpenseService
    {
        public const string GeneralKind = "General";
        public const string SalaryKind = "Salary";
        public const string AdvanceKind = "Advance";
        public const string AdvanceSettlementKind = "AdvanceSettlement";
        public const string DeductionKind = "Deduction";

        private const string JournalSourceType = "ExpenseEntry";
        private const string StatusOpen = "Open";
        private const string PaymentCash = "Cash";

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

        private readonly AppDbContext _context;
        private readonly ILogger<ExpenseService> _logger;

        public ExpenseService(AppDbContext context, ILogger<ExpenseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ==================================================================
        //  دوال عالية المستوى لشاشة الكاشير (POS)
        // ==================================================================

        /// <summary>
        /// تسجيل مصروف عام أو سلفة من شاشة الكاشير وربطه بالوردية المفتوحة للمستخدم،
        /// مع إنشاء قيود اليومية داخل معاملة واحدة. طريقة الدفع دائماً نقدي (من الصندوق).
        /// </summary>
        public async Task<ExpenseEntry> CreatePosExpenseAsync(
            string? userId,
            string? userName,
            string kind,
            decimal amount,
            string? category,
            Guid? employeeId,
            string? note)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("تعذّر تحديد المستخدم الحالي.");

            kind = (kind ?? string.Empty).Trim();
            if (kind != GeneralKind && kind != AdvanceKind)
                throw new InvalidOperationException("نوع الحركة غير مسموح للكاشير.");

            amount = Round2(amount);
            if (amount <= 0)
                throw new InvalidOperationException("أدخل مبلغاً صحيحاً أكبر من صفر.");

            if (kind == GeneralKind)
            {
                employeeId = null;
            }
            else if (!employeeId.HasValue)
            {
                throw new InvalidOperationException("اختر الموظف المستفيد من السلفة.");
            }

            category = string.IsNullOrWhiteSpace(category)
                ? (kind == AdvanceKind ? "سلفة" : "مصروف نقطة بيع")
                : category.Trim();
            if (category.Length > 100)
                category = category[..100];

            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note != null && note.Length > 500)
                note = note[..500];

            var shiftId = await GetOpenShiftIdAsync(userId);
            if (shiftId == null)
                throw new InvalidOperationException("يجب فتح وردية أولاً قبل تسجيل المصروف أو السلفة.");

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var trx = await _context.Database.BeginTransactionAsync();

                var entity = new ExpenseEntry
                {
                    ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Amount = amount,
                    ExpenseKind = kind,
                    Category = category,
                    EmployeeId = employeeId,
                    PaymentMethod = PaymentCash,
                    ReferenceNo = null,
                    Note = note,
                    CreatedByUserId = userId,
                    CreatedByUserName = userName,
                    PosShiftId = shiftId
                };

                _context.Set<ExpenseEntry>().Add(entity);
                await _context.SaveChangesAsync();

                AddFinancialEntriesForExpense(entity);
                await _context.SaveChangesAsync();

                await trx.CommitAsync();

                _logger.LogInformation("POS expense {Kind} of {Amount} created by {User} on shift {Shift}", kind, amount, userName, shiftId);
                return entity;
            });
        }

        /// <summary>
        /// حذف مصروف/سلفة سجّله الكاشير، شرط أن يكون من إنشائه، من نوع مسموح، ومربوطاً
        /// بوردية ما زالت مفتوحة له. يعكس قيود اليومية ضمن معاملة. (يفرض قاعدة الحذف قبل الإغلاق.)
        /// </summary>
        public async Task DeletePosExpenseAsync(Guid id, string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("تعذّر تحديد المستخدم الحالي.");

            var openShiftId = await GetOpenShiftIdAsync(userId);
            if (openShiftId == null)
                throw new InvalidOperationException("لا توجد وردية مفتوحة. لا يمكن الحذف بعد إغلاق الوردية.");

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var trx = await _context.Database.BeginTransactionAsync();

                var entity = await _context.Set<ExpenseEntry>().FindAsync(id);
                if (entity == null)
                    throw new InvalidOperationException("الحركة غير موجودة.");

                if (entity.ExpenseKind != GeneralKind && entity.ExpenseKind != AdvanceKind)
                    throw new InvalidOperationException("لا يمكن حذف هذا النوع من الحركات من شاشة الكاشير.");

                if (!string.Equals(entity.CreatedByUserId, userId, StringComparison.Ordinal))
                    throw new InvalidOperationException("لا يمكنك حذف حركة سجّلها مستخدم آخر.");

                if (entity.PosShiftId == null || entity.PosShiftId != openShiftId)
                    throw new InvalidOperationException("لا يمكن حذف الحركة بعد إغلاق الوردية.");

                await RemoveFinancialEntriesAsync(entity.Id);
                _context.Set<ExpenseEntry>().Remove(entity);
                await _context.SaveChangesAsync();

                await trx.CommitAsync();
                _logger.LogInformation("POS expense {Id} deleted by {User}", id, userId);
            });
        }

        /// <summary>معرّف الوردية المفتوحة للمستخدم الحالي، أو null إن لم توجد.</summary>
        public async Task<Guid?> GetOpenShiftIdAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedByUserId == userId && s.Status == StatusOpen)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        // ==================================================================
        //  المنطق المحاسبي المشترك (يُستخدم أيضاً من الشاشة الإدارية)
        // ==================================================================

        public async Task<decimal> GetOutstandingAdvanceBalanceAsync(Guid? employeeId, Guid? ignoreSalaryExpenseId)
        {
            if (!employeeId.HasValue)
                return 0m;

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

        public async Task RemoveFinancialEntriesAsync(Guid expenseId)
        {
            var entries = await _context.Set<FinJournalEntry>()
                .Where(e => e.SourceType == JournalSourceType && e.SourceId == expenseId)
                .ToListAsync();

            if (entries.Count > 0)
                _context.Set<FinJournalEntry>().RemoveRange(entries);
        }

        public void AddFinancialEntriesForExpense(ExpenseEntry expense)
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

            if (string.Equals(expenseKind, "CommissionWithdrawal", StringComparison.OrdinalIgnoreCase))
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

        public static string BuildSettlementRef(Guid salaryExpenseId)
            => $"SAL:{salaryExpenseId:N}";

        public static decimal Round2(decimal value)
            => Math.Round(value, 2, MidpointRounding.ToEven);
    }
}
