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
        private readonly IConfiguration _configuration;

        public ItemsController(
            IUnitOfWork<Item> items,
            IUnitOfWork<Category> categories,
            AppDbContext context,
            IConfiguration configuration)
        {
            _items = items;
            _categories = categories;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, Guid? categoryId)
        {
            IQueryable<Item> query = _items.Repository.GetAll()
                .Include(i => i.Category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(i => i.Name.Contains(term) || (i.Barcode != null && i.Barcode.Contains(term)));
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

            return RedirectToAction(nameof(PrintLabel), new { id = item.Id });
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
                Price = price ?? item.DefaultSalePriceLyd,
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
                existingItem.DefaultSalePriceLyd = item.DefaultSalePriceLyd;

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

            var usage = await GetItemUsageAsync(item.Id);
            ViewBag.DeleteBlockedReason = usage.BlockedReason;
            ViewBag.CanDelete = string.IsNullOrWhiteSpace(usage.BlockedReason);

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var item = await _context.Set<Item>().FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
                return View("NotFound");

            var usage = await GetItemUsageAsync(item.Id);
            if (!string.IsNullOrWhiteSpace(usage.BlockedReason))
            {
                item.Category = await _context.Set<Category>().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == item.CategoryId);
                ViewBag.DeleteBlockedReason = usage.BlockedReason;
                ViewBag.CanDelete = false;
                return View("Delete", item);
            }

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

        private async Task<(string? BlockedReason, int PurchaseLines, int SalesLines, int StockBalances, int StockLedgers, int BarcodeMappings)> GetItemUsageAsync(Guid itemId)
        {
            var purchaseLines = await _context.Set<PurchaseLine>().IgnoreQueryFilters().CountAsync(l => l.ItemId == itemId);
            var salesLines = await _context.Set<SalesLine>().IgnoreQueryFilters().CountAsync(l => l.ItemId == itemId);
            var stockBalances = await _context.Set<InvStockBalance>().IgnoreQueryFilters().CountAsync(l => l.ItemId == itemId);
            var stockLedgers = await _context.Set<InvStockLedger>().IgnoreQueryFilters().CountAsync(l => l.ItemId == itemId);
            var barcodeMappings = await _context.Set<BarcodeMapping>().IgnoreQueryFilters().CountAsync(m => m.ItemId == itemId);

            var blockers = new List<string>();
            if (purchaseLines > 0) blockers.Add($"{purchaseLines} purchase line(s)");
            if (salesLines > 0) blockers.Add($"{salesLines} sales line(s)");
            if (stockBalances > 0) blockers.Add($"{stockBalances} stock balance record(s)");
            if (stockLedgers > 0) blockers.Add($"{stockLedgers} stock ledger entry(ies)");
            if (barcodeMappings > 0) blockers.Add($"{barcodeMappings} barcode mapping(s)");

            var reason = blockers.Count > 0
                ? $"لا يمكن حذف الصنف لأنه مرتبط ببيانات تشغيلية موجودة: {string.Join(", ", blockers)}. يمكن فقط تعطيله منطقيًا بدون حذف فعلي."
                : null;

            return (reason, purchaseLines, salesLines, stockBalances, stockLedgers, barcodeMappings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItemPrice(Guid itemId, decimal? price)
        {
            if (itemId == Guid.Empty)
                return BadRequest(new { message = "معرّف الصنف غير صالح." });

            var item = await _context.Set<Item>().FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null)
                return NotFound(new { message = "الصنف غير موجود." });

            if (price.HasValue && price.Value >= 0)
            {
                item.DefaultSalePriceLyd = price.Value > 0 ? price.Value : (decimal?)null;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "تم حفظ السعر." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public async Task<IActionResult> PrintLabelRaw(Guid itemId, string code, string? price, int copies = 1)
        {
            if (itemId == Guid.Empty)
                return BadRequest(new { message = "معرّف الصنف غير صالح." });

            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { message = "الباركود مطلوب." });

            copies = Math.Clamp(copies, 1, 200);

            var item = await _context.Set<Item>()
                .Where(i => i.Id == itemId)
                .Select(i => new { i.Name })
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound(new { message = "الصنف غير موجود." });

            // Save sell price to item if provided
            if (decimal.TryParse(price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedPrice) && parsedPrice > 0)
            {
                var itemToUpdate = await _context.Set<Item>().FindAsync(itemId);
                if (itemToUpdate != null)
                {
                    itemToUpdate.DefaultSalePriceLyd = parsedPrice;
                    await _context.SaveChangesAsync();
                }
            }

            var siteInfo = await _context.Set<SiteInfo>()
                .AsNoTracking()
                .OrderByDescending(s => s.Created)
                .Select(s => new { s.Name })
                .FirstOrDefaultAsync();

            var printing = _configuration.GetSection("Printing");
            var printerName = printing["LabelPrinterName"] ?? string.Empty;
            var widthMm    = printing.GetValue<int>("LabelWidthMm",  38);
            var heightMm   = printing.GetValue<int>("LabelHeightMm", 25);
            var gapMm      = printing.GetValue<int>("LabelGapMm",     2);
            var speed      = printing.GetValue<int>("LabelSpeed",      4);
            var density    = printing.GetValue<int>("LabelDensity",   10);

            var shopName  = siteInfo?.Name ?? string.Empty;
            var priceText = string.IsNullOrWhiteSpace(price) ? string.Empty : price.Trim();

            byte[] labelBytes;
            try
            {
                var builder = new NewsApp2.Classes.Helpers.TsplLabelBuilder(
                    dpi: 203,
                    labelWidthMm:  widthMm,
                    labelHeightMm: heightMm,
                    labelGapMm:    gapMm,
                    speed:         speed,
                    density:       density);

                labelBytes = builder.BuildLabel(item.Name, code, priceText, shopName, null, priceLabel: "السعر", copies: copies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"فشل بناء الطباعة: {ex.Message}" });
            }

            if (!NewsApp2.Classes.Helpers.RawPrinterHelper.SendBytesToPrinter(printerName, labelBytes, out var printError))
                return StatusCode(500, new { message = $"فشل إرسال مهمة الطباعة: {printError}" });

            return Ok(new { message = "تم إرسال مهمة الطباعة بنجاح." });
        }

        private async Task<string> GenerateInternalBarcodeAsync(Guid categoryId)        {
            var categoryName = await _categories.Repository.GetAll()
                .Where(c => c.Id == categoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync() ?? string.Empty;

            var normalized = categoryName.ToLowerInvariant();
            var prefix = normalized.Contains("شنط") || normalized.Contains("bag")
                ? "20"
                : "30";

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
