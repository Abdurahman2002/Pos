using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "InventoryEditPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class CategoriesController : Controller
    {
        private readonly IUnitOfWork<Category> _categories;
        private readonly IUnitOfWork<Item> _items;

        public CategoriesController(IUnitOfWork<Category> categories, IUnitOfWork<Item> items)
        {
            _categories = categories;
            _items = items;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var query = _categories.Repository.GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c => c.Name.Contains(term));
            }

            var list = await query.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Search = search;
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var category = await _categories.Repository.GetByIdAsync(id);
            if (category == null)
                return View("NotFound");

            return View(category);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (!ModelState.IsValid)
                return View(category);

            var nameExists = await _categories.Repository
                .GetAll()
                .IgnoreQueryFilters()
                .Where(c => c.Name == category.Name.Trim())
                .FirstOrDefaultAsync();
            if (nameExists != null)
            {
                ViewBag.Message = $"Category '{category.Name}' already exists.";
                return View(category);
            }

            category.Name = category.Name.Trim();
            _categories.Repository.Insert(category);
            await _categories.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var category = await _categories.Repository.GetByIdAsync(id);
            if (category == null)
                return View("NotFound");

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Category category)
        {
            if (id != category.Id)
                return View("NotFound");

            if (!ModelState.IsValid)
                return View(category);

            var nameExists = await _categories.Repository
                .GetAll()
                .IgnoreQueryFilters()
                .Where(c => c.Name == category.Name.Trim() && c.Id != category.Id)
                .FirstOrDefaultAsync();
            if (nameExists != null)
            {
                ViewBag.Message = $"Category '{category.Name}' already exists.";
                return View(category);
            }

            try
            {
                category.Name = category.Name.Trim();
                _categories.Repository.Update(category);
                await _categories.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                var exists = (_categories.Repository.GetAll()?.Any(e => e.Id == category.Id)).GetValueOrDefault();
                if (!exists)
                    return View("NotFound");
                return View("Error");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var category = await _categories.Repository.GetByIdAsync(id);
            if (category == null)
                return View("NotFound");

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var category = await _categories.Repository.GetByIdAsync(id);
            if (category == null)
                return View("NotFound");

            try
            {
                var hasItems = await _items.Repository
                    .GetWhere(i => i.CategoryId == category.Id)
                    .AnyAsync();

                if (hasItems)
                {
                    ViewBag.Message = "Cannot delete category with existing items.";
                    return View(category);
                }

                category.IsDeleted = true;
                category.DeletedAt = DateTime.UtcNow;
                _categories.Repository.Update(category);
                await _categories.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View("Error");
            }
        }
    }
}
