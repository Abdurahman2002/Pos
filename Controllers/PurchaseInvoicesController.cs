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
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
        {
            var query = _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == null || (i.Status != "Cancelled" && i.Status != "Canceled"));

            if (from.HasValue)
                query = query.Where(i => i.InvoiceDate >= from.Value);
            if (to.HasValue)
                query = query.Where(i => i.InvoiceDate <= to.Value);

            var list = await query.OrderByDescending(i => i.Created).Take(200).ToListAsync();
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
                .Include(l => l.Item)
                .OrderBy(l => l.LineOrder)
                .ThenBy(l => l.Created)
                .ThenBy(l => l.Id)
                .ToListAsync();

            ViewBag.Lines = lines;
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

            if (string.Equals(vm.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase))
            {
                if (!vm.SupplierId.HasValue || vm.SupplierId == Guid.Empty)
                    ModelState.AddModelError(nameof(vm.SupplierId), "حدد المورد عند الشراء الآجل.");

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
                PaymentMethod = PaymentCash
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

            if (string.Equals(vm.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase))
            {
                if (!vm.SupplierId.HasValue || vm.SupplierId == Guid.Empty)
                    ModelState.AddModelError(nameof(vm.SupplierId), "حدد المورد عند الشراء الآجل.");

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
                .Where(i => i.Status == null || (i.Status != "Cancelled" && i.Status != "Canceled"));
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
                    TotalDinar = i.TotalDinar,
                    Status = i.Status
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
                "d/M/yyyy",
                "MM/dd/yyyy",
                "M/d/yyyy"
            };

            return DateOnly.TryParseExact(rawDate, acceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out invoiceDate)
                || DateOnly.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out invoiceDate)
                || DateOnly.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out invoiceDate);
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

            return PaymentCash;
        }
    }
}
