using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using NewsApp2.Classes;
using NewsApp2.Classes.Helpers;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Services;
using NewsApp2.ViewModels.Purchasing;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "AdminOrProgPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class PurchaseInvoicesController : Controller
    {
        private const string PaymentCash = "Cash";
        private const string PaymentCredit = "Credit";
        private const string PaymentTransfer = "Transfer";

        private readonly AppDbContext _context;
        private readonly PurchaseService _purchaseService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PurchaseInvoicesController(AppDbContext context, PurchaseService purchaseService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _purchaseService = purchaseService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int page = 1)
        {
            var query = _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Status != "Cancelled");

            if (from.HasValue)
                query = query.Where(i => i.InvoiceDate >= from.Value);
            if (to.HasValue)
                query = query.Where(i => i.InvoiceDate <= to.Value);

            const int pageSize = 50;
            page = NewsApp2.ViewModels.Common.PaginationVM.NormalizePage(page);
            var totalCount = await query.CountAsync();
            var list = await query
                .OrderByDescending(i => i.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.Pagination = new NewsApp2.ViewModels.Common.PaginationVM { Page = page, PageSize = pageSize, TotalCount = totalCount };
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var invoice = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            var lines = await _context.Set<PurchaseLine>()
                .AsNoTracking()
                .Where(l => l.PurchaseInvoiceId == id)
                .IgnoreQueryFilters()
                .Include(l => l.Item)
                .OrderBy(l => l.LineOrder)
                .ThenBy(l => l.Created)
                .ThenBy(l => l.Id)
                .ToListAsync();

            ViewBag.Lines = lines;
            return View(invoice);
        }

        [HttpGet]
        public async Task<IActionResult> Pdf(Guid id)
        {
            var invoice = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            var lines = await _context.Set<PurchaseLine>()
                .AsNoTracking()
                .Where(l => l.PurchaseInvoiceId == id)
                .IgnoreQueryFilters()
                .Include(l => l.Item)
                .OrderBy(l => l.LineOrder)
                .ThenBy(l => l.Created)
                .ThenBy(l => l.Id)
                .ToListAsync();

            var siteInfo = await _context.Set<SiteInfo>()
                .AsNoTracking()
                .OrderByDescending(s => s.Created)
                .FirstOrDefaultAsync();

            ViewBag.Lines = lines;
            ViewBag.ShopName = siteInfo?.Name;
            ViewBag.ShopLogo = siteInfo?.LogoUrl;
            return View(invoice);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var invoice = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            if (!string.Equals(invoice.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "يمكن تعديل الفواتير المرحلة فقط.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var lines = await _context.Set<PurchaseLine>()
                .AsNoTracking()
                .Where(l => l.PurchaseInvoiceId == id)
                .OrderBy(l => l.LineOrder)
                .ThenBy(l => l.Created)
                .ThenBy(l => l.Id)
                .ToListAsync();

            var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
            var salePriceByItem = await _context.Set<Item>()
                .AsNoTracking()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.DefaultSalePriceLyd);

            await LoadItemsAsync();

            var vm = new PurchaseEditVM
            {
                InvoiceId = invoice.Id,
                InvoiceDate = invoice.InvoiceDate,
                CurrencyCode = "LYD",
                EurToDinarRateSnapshot = 1m,
                SupplierId = invoice.SupplierId,
                PaymentMethod = NormalizePaymentMethod(invoice.PaymentMethod),
                DiscountType = NormalizeDiscountType(invoice.DiscountType),
                DiscountValue = invoice.DiscountValue,
                DueDate = invoice.DueDate,
                Note = invoice.Note,
                Lines = lines.Select(l => new PurchaseEditLineVM
                {
                    ItemId = l.ItemId,
                    Qty = (int)Math.Truncate(l.Qty),
                    UnitPriceEur = l.UnitPriceEur,
                    SellPriceLyd = salePriceByItem.TryGetValue(l.ItemId, out var price) ? price : null
                }).ToList()
            };

            if (!vm.Lines.Any())
                vm.Lines.Add(new PurchaseEditLineVM());

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Edit(PurchaseEditVM vm)
        {
            if (TryResolveInvoiceDateFromRequest(out var resolvedInvoiceDate))
            {
                vm.InvoiceDate = resolvedInvoiceDate;
                ModelState.Remove(nameof(PurchaseEditVM.InvoiceDate));
            }

            vm.Lines = vm.Lines?.Where(l => l != null).ToList() ?? new List<PurchaseEditLineVM>();

            vm.CurrencyCode = "LYD";
            vm.EurToDinarRateSnapshot = 1m;
            vm.PaymentMethod = NormalizePaymentMethod(vm.PaymentMethod);
            vm.DiscountType = NormalizeDiscountType(vm.DiscountType);

            if (!vm.SupplierId.HasValue || vm.SupplierId == Guid.Empty)
                ModelState.AddModelError(nameof(vm.SupplierId), "حدد المورد لفاتورة المشتريات.");

            if (string.Equals(vm.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase))
            {
                if (!vm.DueDate.HasValue)
                    ModelState.AddModelError(nameof(vm.DueDate), "حدد تاريخ الاستحقاق عند الشراء الآجل.");

                if (vm.DueDate.HasValue && vm.DueDate.Value < vm.InvoiceDate)
                    ModelState.AddModelError(nameof(vm.DueDate), "تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ الفاتورة.");
            }
            else
            {
                vm.DueDate = null;
            }

            if (!vm.Lines.Any())
                ModelState.AddModelError("Lines", "أضف سطر صنف واحد على الأقل.");

            if (vm.Lines.Any(l => !l.Qty.HasValue || l.Qty.Value <= 0))
                ModelState.AddModelError("Lines", "يجب أن تكون الكمية أكبر من صفر في جميع السطور.");

            if (vm.Lines.Any(l => !l.UnitPriceEur.HasValue || l.UnitPriceEur.Value < 0))
                ModelState.AddModelError("Lines", "لا يمكن أن يكون سعر الوحدة سالبا.");

            if (vm.Lines.Any(l => l.SellPriceLyd.HasValue && l.SellPriceLyd.Value < 0))
                ModelState.AddModelError("Lines", "لا يمكن أن يكون سعر البيع سالبا.");

            ValidateDiscount(vm.DiscountType, vm.DiscountValue, nameof(vm.DiscountValue));

            if (!ModelState.IsValid)
            {
                await LoadItemsAsync();
                return View(vm);
            }

            try
            {
                var lines = vm.Lines.Select(l => (
                    l.ItemId,
                    (decimal)l.Qty!.Value,
                    l.UnitPriceEur!.Value,
                    l.SellPriceLyd
                ));

                await _purchaseService.UpdatePostedAsync(
                    vm.InvoiceId,
                    vm.InvoiceDate,
                    vm.EurToDinarRateSnapshot!.Value,
                    vm.Note,
                    vm.SupplierId,
                    vm.PaymentMethod,
                    vm.DueDate,
                    vm.DiscountType,
                    vm.DiscountValue,
                    lines,
                    User?.Identity?.Name);

                TempData["SuccessMessage"] = "تم تحديث فاتورة الشراء بنجاح.";
                return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty,
                    UserMessageSanitizer.Sanitize(ex.Message, "تعذر تحديث فاتورة الشراء."));
                await LoadItemsAsync();
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadItemsAsync();
            var vm = new PurchaseCreateVM
            {
                CurrencyCode = "LYD",
                EurToDinarRateSnapshot = 1m,
                PaymentMethod = PaymentCash,
                DiscountType = "Amount",
                DiscountValue = 0m
            };
            vm.Lines.Add(new PurchaseLineInputVM());
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseCreateVM vm)
            {
            if (TryResolveInvoiceDateFromRequest(out var resolvedInvoiceDate))
            {
                vm.InvoiceDate = resolvedInvoiceDate;
                ModelState.Remove(nameof(PurchaseCreateVM.InvoiceDate));
            }

            vm.Lines = vm.Lines?.Where(l => l != null).ToList() ?? new List<PurchaseLineInputVM>();

            vm.CurrencyCode = "LYD";
            vm.EurToDinarRateSnapshot = 1m;
            vm.PaymentMethod = NormalizePaymentMethod(vm.PaymentMethod);
            vm.DiscountType = NormalizeDiscountType(vm.DiscountType);
            var isTransferPayment = string.Equals(vm.PaymentMethod, PaymentTransfer, StringComparison.OrdinalIgnoreCase);
            if (isTransferPayment && (!vm.BankId.HasValue || vm.BankId == Guid.Empty))
                ModelState.AddModelError(nameof(vm.BankId), "حدد المصرف عند الشراء بالتحويل.");
            if (!isTransferPayment)
                vm.BankId = null;

            if (!vm.SupplierId.HasValue || vm.SupplierId == Guid.Empty)
                ModelState.AddModelError(nameof(vm.SupplierId), "حدد المورد لفاتورة المشتريات.");

            if (string.Equals(vm.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase))
            {
                if (!vm.DueDate.HasValue)
                    ModelState.AddModelError(nameof(vm.DueDate), "حدد تاريخ الاستحقاق عند الشراء الآجل.");

                if (vm.DueDate.HasValue && vm.DueDate.Value < vm.InvoiceDate)
                    ModelState.AddModelError(nameof(vm.DueDate), "تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ الفاتورة.");
            }
            else
            {
                vm.DueDate = null;
            }

            if (!vm.Lines.Any())
                ModelState.AddModelError("Lines", "أضف سطر صنف واحد على الأقل.");

            if (vm.Lines.Any(l => !l.Qty.HasValue || l.Qty.Value <= 0))
                ModelState.AddModelError("Lines", "يجب أن تكون الكمية أكبر من صفر في جميع السطور.");

            if (vm.Lines.Any(l => !l.UnitPriceEur.HasValue || l.UnitPriceEur.Value < 0))
                ModelState.AddModelError("Lines", "لا يمكن أن يكون سعر الوحدة سالبا.");

            if (vm.Lines.Any(l => l.SellPriceLyd.HasValue && l.SellPriceLyd.Value < 0))
                ModelState.AddModelError("Lines", "لا يمكن أن يكون سعر البيع سالبا.");

            ValidateDiscount(vm.DiscountType, vm.DiscountValue, nameof(vm.DiscountValue));

            if (!ModelState.IsValid)
            {
                await LoadItemsAsync();
                return View(vm);
            }

            var invoice = new PurchaseInvoice
            {
                InvoiceDate = vm.InvoiceDate,
                CurrencyCode = "LYD",
                EurToDinarRateSnapshot = vm.EurToDinarRateSnapshot!.Value,
                SupplierId = vm.SupplierId,
                PaymentMethod = vm.PaymentMethod,
                DiscountType = vm.DiscountType,
                DiscountValue = vm.DiscountValue,
                BankId = vm.BankId,
                DueDate = vm.DueDate,
                Note = vm.Note,
                CreatedByUserId = _userManager.GetUserId(User),
                CreatedByUserName = User?.Identity?.Name
            };

            var lines = vm.Lines.Select(l => (
                l.ItemId,
                (decimal)l.Qty!.Value,
                l.UnitPriceEur!.Value,
                l.SellPriceLyd
            ));

            try
            {
                var id = await _purchaseService.CreateAsync(invoice, lines);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty,
                    UserMessageSanitizer.Sanitize(ex.Message, "تعذر حفظ فاتورة الشراء."));
                await LoadItemsAsync();
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> CreateSupplierInline([FromBody] CreateSupplierInlineRequest request)
        {
            var name = request.Name?.Trim() ?? string.Empty;
            var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, error = "اسم المورد مطلوب." });

            if (name.Length > 200)
                return BadRequest(new { success = false, error = "اسم المورد طويل جدا." });

            var exists = await _context.Set<Supplier>()
                .AsNoTracking()
                .AnyAsync(c => c.Name == name);

            if (exists)
                return Conflict(new { success = false, error = "المورد موجود بالفعل." });

            var supplier = new Supplier
            {
                Name = name,
                Phone = phone
            };

            _context.Set<Supplier>().Add(supplier);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                supplierId = supplier.Id,
                supplierName = supplier.Name
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            try
            {
                await _purchaseService.CancelAsync(id, User?.Identity?.Name);
                TempData["SuccessMessage"] = "تم إلغاء فاتورة الشراء.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = UserMessageSanitizer.Sanitize(ex.Message, "تعذر إلغاء فاتورة الشراء.");
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Report(DateOnly? from, DateOnly? to)
        {
            var query = _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Status != "Cancelled");
            if (from.HasValue)
                query = query.Where(i => i.InvoiceDate >= from.Value);
            if (to.HasValue)
                query = query.Where(i => i.InvoiceDate <= to.Value);

            var rows = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new PurchaseReportRowVM
                {
                    Number = i.Number,
                    InvoiceDate = i.InvoiceDate,
                    SupplierName = i.Supplier != null ? i.Supplier.Name : "-",
                    TotalDinar = i.TotalDinar,
                    Status = i.Status,
                    PaymentMethod = ToArabicPaymentMethod(i.PaymentMethod),
                    BankName = i.Bank != null ? i.Bank.Name : null
                })
                .ToListAsync();

            var vm = new PurchaseReportVM
            {
                From = from,
                To = to,
                Rows = rows,
                SumDinar = rows.Sum(r => r.TotalDinar)
            };

            return View(vm);
        }

        private async Task LoadItemsAsync()
        {
            var items = await _context.Set<Item>()
                .AsNoTracking()
                .OrderBy(i => i.Name)
                .ToListAsync();

            ViewData["Items"] = new SelectList(items, "Id", "Name");

            var suppliers = await _context.Set<Supplier>()
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewData["Suppliers"] = new SelectList(suppliers, "Id", "Name");

            var banks = await _context.Set<Bank>()
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .ToListAsync();
            ViewData["Banks"] = new SelectList(banks, "Id", "Name");

            var recentPrices = await _context.Set<PurchaseLine>()
                .AsNoTracking()
                .Where(l => l.PurchaseInvoice != null && l.PurchaseInvoice.Status == "Posted")
                .OrderByDescending(l => l.PurchaseInvoice!.InvoiceDate)
                .ThenByDescending(l => l.Created)
                .Select(l => new { l.ItemId, l.UnitPriceEur })
                .ToListAsync();

            var defaultPriceByItem = items.ToDictionary(
                i => i.Id.ToString().ToLowerInvariant(),
                i => 0m);

            var salePriceByItem = items.ToDictionary(
                i => i.Id.ToString().ToLowerInvariant(),
                i => i.DefaultSalePriceLyd ?? 0m);

            foreach (var price in recentPrices)
            {
                var key = price.ItemId.ToString().ToLowerInvariant();
                if (!defaultPriceByItem.ContainsKey(key) || defaultPriceByItem[key] > 0m)
                    continue;

                defaultPriceByItem[key] = price.UnitPriceEur;
            }

            ViewBag.ItemDefaultPricesJson = JsonSerializer.Serialize(defaultPriceByItem);
            ViewBag.ItemSalePricesJson = JsonSerializer.Serialize(salePriceByItem);
        }

        private bool TryResolveInvoiceDateFromRequest(out DateOnly invoiceDate)
        {
            invoiceDate = default;

            var rawDate = Request.Form["InvoiceDate"].FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(rawDate))
                return false;

            var acceptedFormats = new[]
            {
                "yyyy-MM-dd",
                "dd/MM/yyyy",
                "d/M/yyyy"
            };

            return DateOnly.TryParseExact(rawDate, acceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out invoiceDate);
        }

        private string GetSecondaryCurrencyCode()
        {
            return "دينار";
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return PaymentCash;

            if (string.Equals(paymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase))
                return PaymentCredit;

            if (string.Equals(paymentMethod, PaymentTransfer, StringComparison.OrdinalIgnoreCase))
                return PaymentTransfer;

            return PaymentCash;
        }

        private static string NormalizeDiscountType(string? discountType)
        {
            return string.Equals(discountType, "Percent", StringComparison.OrdinalIgnoreCase)
                ? "Percent"
                : "Amount";
        }

        private void ValidateDiscount(string discountType, decimal discountValue, string fieldName)
        {
            if (discountValue < 0)
            {
                ModelState.AddModelError(fieldName, "لا يمكن أن يكون الخصم سالبا.");
                return;
            }

            if (string.Equals(discountType, "Percent", StringComparison.OrdinalIgnoreCase) && discountValue > 100)
                ModelState.AddModelError(fieldName, "نسبة الخصم لا يمكن أن تتجاوز 100%.");
        }

        private static string ToArabicPaymentMethod(string? paymentMethod)
        {
            if (string.Equals(paymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase))
                return "آجل";
            if (string.Equals(paymentMethod, PaymentTransfer, StringComparison.OrdinalIgnoreCase))
                return "تحويل";
            return "نقدي";
        }

        public sealed class CreateSupplierInlineRequest
        {
            public string? Name { get; set; }
            public string? Phone { get; set; }
        }
    }
}
