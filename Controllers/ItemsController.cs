using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.Models.Services;
using NewsApp2.Classes.Helpers;
using NewsApp2.ViewModels.Inventory;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "InventoryEditPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class ItemsController : Controller
    {
        private readonly IUnitOfWork<Item> _items;
        private readonly IUnitOfWork<Category> _categories;
        private readonly AppDbContext _context;

        public ItemsController(
            IUnitOfWork<Item> items,
            IUnitOfWork<Category> categories,
            AppDbContext context)
        {
            _items = items;
            _categories = categories;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, Guid? categoryId)
        {
            IQueryable<Item> query = _items.Repository.GetAll()
                .Include(i => i.Category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(i => i.Name.Contains(term));
            }

            if (categoryId.HasValue && categoryId != Guid.Empty)
            {
                query = query.Where(i => i.CategoryId == categoryId.Value);
            }

            var list = await query.OrderBy(i => i.Name).ToListAsync();
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            await LoadCategories(categoryId);
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var item = await _items.Repository
                .GetWhere(i => i.Id == id)
                .Include(i => i.Category)
                .FirstOrDefaultAsync();
            if (item == null)
                return View("NotFound");

            return View(item);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Prog,SalesManager")]
        public async Task<IActionResult> Create(string? barcode, string? returnUrl)
        {
            await LoadCategories();
            var model = new Item
            {
                Barcode = NormalizeBarcode(barcode)
            };

            ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Prog,SalesManager")]
        public async Task<IActionResult> Create(Item item, string? returnUrl)
        {
            await LoadCategories(item.CategoryId);
            ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            item.Name = item.Name?.Trim() ?? string.Empty;
            item.Barcode = NormalizeBarcode(item.Barcode);

            if (!ModelState.IsValid)
                return View(item);

            var nameExists = await _items.Repository
                                 .GetAll()
                                 .IgnoreQueryFilters()
                                 .Where(i => i.Name == item.Name)
                                 .FirstOrDefaultAsync();
            if (nameExists != null)
            {
                ViewBag.Message = $"الصنف '{item.Name}' موجود بالفعل.";
                return View(item);
            }

            if (string.IsNullOrWhiteSpace(item.Barcode))
            {
                item.Barcode = await GenerateInternalBarcodeAsync(item.CategoryId);
            }

            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                var barcodeInUse = await _items.Repository
                    .GetAll()
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.Barcode == item.Barcode);

                if (barcodeInUse)
                {
                    ModelState.AddModelError(nameof(Item.Barcode), "الباركود موجود بالفعل.");
                    return View(item);
                }

                var mappingExists = await _context.Set<BarcodeMapping>()
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.Code == item.Barcode && m.CodeType == BarcodeCodeType.Raw.ToString());

                if (mappingExists)
                {
                    ModelState.AddModelError(nameof(Item.Barcode), "الباركود مرتبط بصنف آخر بالفعل.");
                    return View(item);
                }
            }

            _items.Repository.Insert(item);
            await _items.SaveAsync();

            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                _context.Set<BarcodeMapping>().Add(new BarcodeMapping
                {
                    ItemId = item.Id,
                    Code = item.Barcode,
                    CodeType = BarcodeCodeType.Raw.ToString(),
                    Note = "Primary item barcode"
                });
                await _context.SaveChangesAsync();
            }

            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> PrintLabel(Guid id, string? code = null, decimal? price = null, int copies = 1)
        {
            var item = await _items.Repository
                .GetWhere(i => i.Id == id)
                .Include(i => i.Category)
                .FirstOrDefaultAsync();

            if (item == null)
                return View("NotFound");

            var codes = await _context.Set<BarcodeMapping>()
                .AsNoTracking()
                .Where(m => m.ItemId == id && m.CodeType == BarcodeCodeType.Raw.ToString())
                .Select(m => m.Code)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(item.Barcode) && !codes.Contains(item.Barcode))
            {
                codes.Insert(0, item.Barcode);
            }

            var selectedCode = string.IsNullOrWhiteSpace(code)
                ? (item.Barcode ?? codes.FirstOrDefault() ?? string.Empty)
                : code.Trim();

            var siteInfo = await _context.Set<SiteInfo>()
                .AsNoTracking()
                .OrderByDescending(s => s.Created)
                .FirstOrDefaultAsync();

            var vm = new ItemLabelPrintVM
            {
                ItemId = item.Id,
                ItemName = item.Name,
                CategoryName = item.Category?.Name,
                SelectedCode = selectedCode,
                AvailableCodes = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Price = price,
                Copies = Math.Clamp(copies, 1, 200),
                ShopName = siteInfo?.Name,
                ShopLogoUrl = siteInfo?.LogoUrl
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var item = await _items.Repository
                .GetWhere(i => i.Id == id)
                .FirstOrDefaultAsync();
            if (item == null)
                return View("NotFound");

            await LoadCategories(item.CategoryId);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Item item)
        {
            if (id != item.Id)
                return View("NotFound");

            await LoadCategories(item.CategoryId);

            item.Name = item.Name?.Trim() ?? string.Empty;
            item.Barcode = NormalizeBarcode(item.Barcode);

            if (!ModelState.IsValid)
                return View(item);

            var existingItem = await _items.Repository.GetByIdAsync(id);
            if (existingItem == null)
                return View("NotFound");

            var nameExists = await _items.Repository
                                 .GetAll()
                                 .IgnoreQueryFilters()
                                 .Where(i => i.Name == item.Name && i.Id != item.Id)
                                 .FirstOrDefaultAsync();
            if (nameExists != null)
            {
                ViewBag.Message = $"الصنف '{item.Name}' موجود بالفعل.";
                return View(item);
            }

            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                var barcodeInUse = await _items.Repository
                    .GetAll()
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.Barcode == item.Barcode && i.Id != item.Id);

                if (barcodeInUse)
                {
                    ModelState.AddModelError(nameof(Item.Barcode), "الباركود موجود بالفعل.");
                    return View(item);
                }

                var rawType = BarcodeCodeType.Raw.ToString();
                var conflictingMap = await _context.Set<BarcodeMapping>()
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.Code == item.Barcode && m.CodeType == rawType && m.ItemId != item.Id);

                if (conflictingMap)
                {
                    ModelState.AddModelError(nameof(Item.Barcode), "الباركود مرتبط بصنف آخر بالفعل.");
                    return View(item);
                }
            }

            try
            {
                existingItem.Name = item.Name;
                existingItem.CategoryId = item.CategoryId;
                existingItem.ReorderLevel = item.ReorderLevel;
                existingItem.Barcode = item.Barcode;

                _items.Repository.Update(existingItem);
                await _items.SaveAsync();

                await SyncRawBarcodeMappingAsync(existingItem.Id, existingItem.Barcode);

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                var exists = (_items.Repository.GetAll()?.Any(e => e.Id == item.Id)).GetValueOrDefault();
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

            var item = await _items.Repository
                .GetWhere(i => i.Id == id)
                .Include(i => i.Category)
                .FirstOrDefaultAsync();
            if (item == null)
                return View("NotFound");

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var item = await _context.Set<Item>().FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
                return View("NotFound");

            try
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View("Error");
            }
        }

        private async Task LoadCategories(Guid? selected = null)
        {
            var categories = await _categories.Repository.GetAll()
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewData["Categories"] = new SelectList(categories, "Id", "Name", selected);
        }

        private static string? NormalizeBarcode(string? barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            return barcode.Trim();
        }

        private async Task SyncRawBarcodeMappingAsync(Guid itemId, string? barcode)
        {
            var rawType = BarcodeCodeType.Raw.ToString();

            var primaryMapping = await _context.Set<BarcodeMapping>()
                .Where(m => m.ItemId == itemId
                            && m.CodeType == rawType
                            && m.Note == "Primary item barcode")
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(barcode))
            {
                if (primaryMapping != null)
                {
                    _context.Set<BarcodeMapping>().Remove(primaryMapping);
                    await _context.SaveChangesAsync();
                }
                return;
            }

            if (primaryMapping == null)
            {
                _context.Set<BarcodeMapping>().Add(new BarcodeMapping
                {
                    ItemId = itemId,
                    Code = barcode,
                    CodeType = rawType,
                    Note = "Primary item barcode"
                });
                await _context.SaveChangesAsync();
                return;
            }

            primaryMapping.Code = barcode;

            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateInternalBarcodeAsync(Guid categoryId)
        {
            var categoryName = await _categories.Repository.GetAll()
                .Where(c => c.Id == categoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync() ?? string.Empty;

            var normalized = categoryName.ToLowerInvariant();
            var prefix = normalized.Contains("شنط") || normalized.Contains("bag")
                ? "BAG"
                : "ACC";

            var dayStamp = DateTime.UtcNow.ToString("yyMMdd");

            for (var i = 1; i <= 9999; i++)
            {
                var candidate = $"{prefix}{dayStamp}{i:0000}";

                var inItems = await _items.Repository.GetAll()
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.Barcode == candidate);
                if (inItems)
                    continue;

                var inMappings = await _context.Set<BarcodeMapping>()
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.Code == candidate && m.CodeType == BarcodeCodeType.Raw.ToString());
                if (!inMappings)
                    return candidate;
            }

            throw new InvalidOperationException("تعذر إنشاء باركود داخلي فريد.");
        }
    }
}
