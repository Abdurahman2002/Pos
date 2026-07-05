using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Services;

namespace NewsApp2.Controllers
{
    /// <summary>
    /// نقطة دخول مخصّصة للكاشير لتسجيل المصروفات والسلف من شاشة البيع (POS) عبر AJAX/JSON.
    /// مقيّدة بـ «مصروف عام» و«سلفة» فقط، وتربط الحركة بالوردية المفتوحة. لا تفتح الشاشة
    /// الإدارية الكاملة (مرتبات/تسويات) للكاشير.
    /// </summary>
    [Authorize(Policy = "ApprovedUserPolicy")]
    [Authorize(Roles = "Cashier,Employee,SalesOfficer,SalesManager,Admin,Prog")]
    public class PosExpensesController : Controller
    {
        private const string GeneralKind = ExpenseService.GeneralKind;
        private const string AdvanceKind = ExpenseService.AdvanceKind;
        private const string CashierRole = "Cashier";

        private readonly AppDbContext _context;
        private readonly ExpenseService _expenseService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PosExpensesController(
            AppDbContext context,
            ExpenseService expenseService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _expenseService = expenseService;
            _userManager = userManager;
        }

        /// <summary>
        /// قائمة الموظفين المسموح منحهم سلفة (الكاشير نفسه + أي موظف غير كاشير آخر)،
        /// مع تحديد موظف الكاشير نفسه ليكون الافتراضي.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Lookups()
        {
            var userId = _userManager.GetUserId(User);

            var cashierUsers = await _userManager.GetUsersInRoleAsync(CashierRole);
            var cashierIds = cashierUsers.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);

            var employees = await _context.Set<Employee>()
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .Select(e => new { e.Id, e.Name, e.UserId })
                .ToListAsync();

            var allowed = employees
                .Where(e => e.UserId == null
                            || e.UserId == userId
                            || !cashierIds.Contains(e.UserId))
                .Select(e => new { id = e.Id, name = e.Name })
                .ToList();

            var selfEmployeeId = employees
                .FirstOrDefault(e => e.UserId != null && e.UserId == userId)?.Id;

            return Json(new { success = true, employees = allowed, selfEmployeeId });
        }

        /// <summary>
        /// ملخّص الوردية المفتوحة + حركات المصروف/السلفة التي سجّلها المستخدم في هذه الوردية
        /// (للعرض والحذف قبل الإغلاق).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Shift()
        {
            var userId = _userManager.GetUserId(User);
            var shiftId = await _expenseService.GetOpenShiftIdAsync(userId);

            if (shiftId == null)
                return Json(new { success = true, hasShift = false, entries = Array.Empty<object>(), total = 0m });

            var entries = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.PosShiftId == shiftId && e.CreatedByUserId == userId)
                .Where(e => e.ExpenseKind == GeneralKind || e.ExpenseKind == AdvanceKind)
                .OrderByDescending(e => e.Created)
                .Select(e => new
                {
                    id = e.Id,
                    kind = e.ExpenseKind,
                    amount = e.Amount,
                    category = e.Category,
                    employeeName = e.Employee != null ? e.Employee.Name : null,
                    note = e.Note
                })
                .ToListAsync();

            var total = entries.Sum(e => e.amount);

            return Json(new { success = true, hasShift = true, entries, total });
        }

        /// <summary>تسجيل مصروف عام أو سلفة من شاشة البيع.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string kind, decimal amount, string? category, Guid? employeeId, string? note)
        {
            var userId = _userManager.GetUserId(User);
            var userName = User.Identity?.Name;

            kind = (kind ?? string.Empty).Trim();
            if (kind != GeneralKind && kind != AdvanceKind)
                return BadRequest(new { success = false, error = "نوع الحركة غير مسموح." });

            if (kind == AdvanceKind)
            {
                if (!employeeId.HasValue)
                    return BadRequest(new { success = false, error = "اختر الموظف المستفيد من السلفة." });

                var validationError = await ValidateAdvanceEmployeeAsync(employeeId.Value, userId);
                if (validationError != null)
                    return BadRequest(new { success = false, error = validationError });
            }
            else
            {
                employeeId = null;
            }

            try
            {
                var entity = await _expenseService.CreatePosExpenseAsync(userId, userName, kind, amount, category, employeeId, note);
                return Json(new { success = true, id = entity.Id });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>حذف حركة مصروف/سلفة سجّلها الكاشير قبل إغلاق ورديته.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            try
            {
                await _expenseService.DeletePosExpenseAsync(id, userId);
                return Json(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>يتحقّق من أن موظف السلفة موجود وليس كاشيراً آخر. يُعيد رسالة خطأ أو null.</summary>
        private async Task<string?> ValidateAdvanceEmployeeAsync(Guid employeeId, string? currentUserId)
        {
            var employee = await _context.Set<Employee>()
                .AsNoTracking()
                .Where(e => e.Id == employeeId)
                .Select(e => new { e.Id, e.UserId })
                .FirstOrDefaultAsync();

            if (employee == null)
                return "الموظف غير موجود.";

            // موظف بلا حساب مستخدم أو هو الكاشير نفسه => مسموح
            if (employee.UserId == null || employee.UserId == currentUserId)
                return null;

            var cashierUsers = await _userManager.GetUsersInRoleAsync(CashierRole);
            if (cashierUsers.Any(u => u.Id == employee.UserId))
                return "لا يمكن منح سلفة لكاشير آخر.";

            return null;
        }
    }
}
