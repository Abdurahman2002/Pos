using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "AdminOrProgPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class BanksController : Controller
    {
        private readonly AppDbContext _context;

        public BanksController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rows = await _context.Set<Bank>()
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .ToListAsync();
            return View(rows);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Bank());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Bank model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _context.Set<Bank>().Add(model);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تمت إضافة المصرف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var bank = await _context.Set<Bank>().FirstOrDefaultAsync(b => b.Id == id);
            if (bank == null) return View("NotFound");
            return View(bank);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Bank model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var bank = await _context.Set<Bank>().FirstOrDefaultAsync(b => b.Id == model.Id);
            if (bank == null) return View("NotFound");

            bank.Name = model.Name?.Trim() ?? string.Empty;
            bank.Note = model.Note;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث المصرف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var bank = await _context.Set<Bank>().FirstOrDefaultAsync(b => b.Id == id);
            if (bank == null) return RedirectToAction(nameof(Index));

            var isUsed = await _context.Set<SalesInvoice>().AnyAsync(i => i.BankId == id)
                         || await _context.Set<PurchaseInvoice>().AnyAsync(i => i.BankId == id);
            if (isUsed)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف المصرف لأنه مستخدم في فواتير.";
                return RedirectToAction(nameof(Index));
            }

            _context.Set<Bank>().Remove(bank);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف المصرف.";
            return RedirectToAction(nameof(Index));
        }
    }
}
