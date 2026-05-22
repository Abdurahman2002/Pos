using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NewsApp2.Classes;
using NewsApp2.Classes.Helpers;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Services;
using NewsApp2.ViewModels.Sales;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "InventoryCreatePolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class SalesInvoicesController : Controller
    {
        private const string PaymentCash = "Cash";
        private const string PaymentCard = "Card";
        private const string PaymentTransfer = "Transfer";
        private const string PaymentCredit = "Credit";
        internal const string DailySalesCustomerName = "مبيعات يومية";
        private static readonly Guid DailySalesCustomerSeedId = Guid.Parse("7e2efb6c-0cb2-430f-92af-6e0ad720f105");

        private readonly AppDbContext _context;
        private readonly SalesService _salesService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SalesInvoicesController(AppDbContext context, SalesService salesService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _salesService = salesService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, Guid? customerId)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            var userId = _userManager.GetUserId(User);
            var canViewAll = CanViewAllInvoices();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var query = _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status != "Cancelled");

            if (!canViewAll)
            {
                query = query.Where(i => i.CreatedByUserId == userId && i.InvoiceDate == today);
            }

            if (from.HasValue)
                query = query.Where(i => i.InvoiceDate >= from.Value);
            if (to.HasValue)
                query = query.Where(i => i.InvoiceDate <= to.Value);
            if (customerId.HasValue && customerId.Value != Guid.Empty)
                query = query.Where(i => i.CustomerId == customerId.Value);

            var list = await query
                .Include(i => i.Customer)
                .OrderByDescending(i => i.Created)
                .Take(200)
                .ToListAsync();

            await LoadCustomersAsync(customerId);
            ViewBag.CustomerId = customerId;
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id, Guid? selectedCustomerId = null)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            var invoice = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            if (!CanViewAllInvoices() && invoice.CreatedByUserId != _userManager.GetUserId(User))
                return Forbid();

            var lines = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => l.SalesInvoiceId == id)
                .IgnoreQueryFilters()
                .Include(l => l.Item)
                .OrderBy(l => l.LineOrder)
                .ThenBy(l => l.Created)
                .ThenBy(l => l.Id)
                .ToListAsync();

            ViewBag.Lines = lines;
            ViewBag.CanApprovePending = CanApprovePendingInvoices();

            if (string.Equals(invoice.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase) && CanApprovePendingInvoices())
            {
                await LoadCustomersAsync(selectedCustomerId);
            }

            return View(invoice);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Edit(Guid id)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            var invoice = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            if (!string.Equals(invoice.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "يمكن تعديل الفواتير المرحلة فقط.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var lines = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => l.SalesInvoiceId == id)
                .OrderBy(l => l.LineOrder)
                .ThenBy(l => l.Created)
                .ThenBy(l => l.Id)
                .ToListAsync();

            await LoadItemsAsync();
            await LoadCustomersAsync(invoice.CustomerId);

            var vm = new SalesEditVM
            {
                InvoiceId = invoice.Id,
                InvoiceDate = invoice.InvoiceDate,
                CurrencyCode = invoice.CurrencyCode,
                EurToDinarRateSnapshot = invoice.EurToDinarRateSnapshot,
                CustomerId = invoice.CustomerId,
                Note = invoice.Note,
                Lines = lines.Select(l => new SalesEditLineVM
                {
                    ItemId = l.ItemId,
                    Qty = (int)Math.Truncate(l.Qty),
                    UnitPriceEur = l.UnitPriceEur
                }).ToList()
            };

            if (!vm.Lines.Any())
                vm.Lines.Add(new SalesEditLineVM());

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Edit(SalesEditVM vm)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            vm.Lines = vm.Lines?.Where(l => l != null).ToList() ?? new List<SalesEditLineVM>();

            // Keep edit flow LYD-only for simplified POS behavior.
            vm.CurrencyCode = "LYD";
            vm.EurToDinarRateSnapshot = 1m;

            if (!vm.Lines.Any())
                ModelState.AddModelError("Lines", "أضف سطر صنف واحد على الأقل.");

            if (vm.CustomerId.HasValue && vm.CustomerId.Value == Guid.Empty)
                vm.CustomerId = null;

            var invoicePaymentMethod = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Id == vm.InvoiceId)
                .Select(i => i.PaymentMethod)
                .FirstOrDefaultAsync();

            if (string.Equals(invoicePaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase)
                && !vm.CustomerId.HasValue)
            {
                ModelState.AddModelError(nameof(vm.CustomerId), "لا يمكن ترك العميل فارغا في فاتورة بيع آجل.");
            }

            if (vm.Lines.Any(l => !l.Qty.HasValue || l.Qty.Value <= 0))
                ModelState.AddModelError("Lines", "يجب أن تكون الكمية أكبر من صفر في جميع السطور.");

            if (vm.Lines.Any(l => !l.UnitPriceEur.HasValue || l.UnitPriceEur.Value < 0))
                ModelState.AddModelError("Lines", "لا يمكن أن يكون سعر الوحدة سالبا.");

            if (!ModelState.IsValid)
            {
                await LoadItemsAsync();
                await LoadCustomersAsync(vm.CustomerId);
                ViewBag.HeldDrafts = await LoadHeldDraftsAsync();
                return View(vm);
            }

            try
            {
                var lines = vm.Lines.Select(l => (
                    l.ItemId,
                    (decimal)l.Qty!.Value,
                    l.UnitPriceEur!.Value
                ));

                await _salesService.UpdatePostedAsync(
                    vm.InvoiceId,
                    vm.InvoiceDate,
                    vm.CustomerId,
                    vm.EurToDinarRateSnapshot!.Value,
                    vm.Note,
                    lines,
                    User?.Identity?.Name);

                TempData["SuccessMessage"] = "تم تحديث فاتورة البيع بنجاح.";
                return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty,
                    UserMessageSanitizer.Sanitize(ex.Message, "تعذر تحديث فاتورة البيع."));
                await LoadItemsAsync();
                await LoadCustomersAsync(vm.CustomerId);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(Guid? customerId = null, Guid? draftId = null)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            ViewBag.SimplePosMode = IsSimplePosMode();
            if (TempData["AutoPrintInvoiceId"] is string autoPrintInvoiceIdText && Guid.TryParse(autoPrintInvoiceIdText, out var autoPrintInvoiceId))
            {
                ViewBag.AutoPrintInvoiceId = autoPrintInvoiceId;
            }
            ViewBag.MaxCashierDiscountPercent = await GetMaxCashierDiscountPercentAsync();
            ViewBag.ShiftReturnUrl = Url.Action(nameof(Create), new { customerId, draftId });

            if (IsSimplePosMode() && !await HasOpenShiftAsync())
            {
                TempData["ErrorMessage"] = "يجب فتح وردية قبل بدء البيع.";
                return RedirectToAction("Current", "PosShifts", new
                {
                    returnUrl = Url.Action(nameof(Create), new { customerId, draftId })
                });
            }

            await LoadItemsAsync();
            await LoadCustomersAsync(customerId);
            await LoadBanksAsync();
            ViewBag.HeldDrafts = await LoadHeldDraftsAsync();

            var vm = draftId.HasValue
                ? await LoadDraftAsync(draftId.Value)
                : new SalesCreateVM { CustomerId = customerId };

            if (vm == null)
                return View("NotFound");

            if (!draftId.HasValue)
                vm.CustomerId ??= customerId;

            vm.PaymentMethod = NormalizePaymentMethod(vm.PaymentMethod);
            vm.IsOnAccount = string.Equals(vm.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase);

            // POS in cashier mode is LYD-only with fixed 1:1 internal rate.
            vm.CurrencyCode = "LYD";
            vm.EurToDinarRateSnapshot = 1m;

            if (IsSimplePosMode())
            {
                vm.DiscountType = "Percent";
                vm.DiscountValue ??= 0m;
                vm.Note = null;
                vm.AutoPrintReceipt = true;
            }

            if (!vm.Lines.Any())
                vm.Lines.Add(new SalesLineInputVM());

            ViewBag.CanSetPricesOnCreate = true;
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Drafts(string? search, DateOnly? from, DateOnly? to, bool includeReturns = true)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();

            var query = _context.Set<SalesInvoiceDraft>()
                .AsNoTracking();

            if (!CanSeeAllDrafts())
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(d => d.CreatedByUserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(d =>
                    d.Title.Contains(term) ||
                    (d.CustomerNameSnapshot != null && d.CustomerNameSnapshot.Contains(term)) ||
                    (d.Note != null && d.Note.Contains(term)));
            }

            if (from.HasValue)
                query = query.Where(d => d.InvoiceDate >= from.Value);

            if (to.HasValue)
                query = query.Where(d => d.InvoiceDate <= to.Value);

            if (!includeReturns)
                query = query.Where(d => !d.IsReturn);

            var drafts = await query
                .OrderByDescending(d => d.Modified ?? d.Created)
                .ThenByDescending(d => d.Created)
                .Select(d => new SalesInvoiceDraftSummaryVM
                {
                    Id = d.Id,
                    Title = d.Title,
                    InvoiceDate = d.InvoiceDate,
                    CustomerName = d.CustomerNameSnapshot,
                    IsReturn = d.IsReturn,
                    LineCount = d.LineCount,
                    NetEur = d.NetEur,
                    NetDinar = d.NetDinar,
                    Created = d.Created,
                    Modified = d.Modified
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.IncludeReturns = includeReturns;
            return View(drafts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesCreateVM vm)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            ViewBag.CanSetPricesOnCreate = true;
            var simplePosMode = IsSimplePosMode();
            ViewBag.SimplePosMode = simplePosMode;
            var maxCashierDiscountPercent = await GetMaxCashierDiscountPercentAsync();
            ViewBag.MaxCashierDiscountPercent = maxCashierDiscountPercent;

            // Old behavior kept in comment for traceability: InvoiceDate relied on posted value and could fail due client-side format differences.
            ModelState.Remove(nameof(vm.InvoiceDate));
            vm.InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var activeShiftId = await GetOpenShiftIdAsync();
            if (simplePosMode && !activeShiftId.HasValue)
            {
                TempData["ErrorMessage"] = "يجب فتح وردية قبل ترحيل البيع.";
                return RedirectToAction("Current", "PosShifts");
            }

            vm.Lines = vm.Lines?.Where(l => l != null).ToList() ?? new List<SalesLineInputVM>();

            // Force single-currency POS behavior regardless of posted form values.
            vm.CurrencyCode = "LYD";
            vm.EurToDinarRateSnapshot = 1m;
            vm.PaymentMethod = NormalizePaymentMethod(vm.PaymentMethod);
            vm.IsOnAccount = vm.IsOnAccount || string.Equals(vm.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase);

            if (vm.IsOnAccount)
                vm.PaymentMethod = PaymentCredit;

            var isTransferPayment = string.Equals(vm.PaymentMethod, PaymentTransfer, StringComparison.OrdinalIgnoreCase);
            if (isTransferPayment && (!vm.BankId.HasValue || vm.BankId == Guid.Empty))
                ModelState.AddModelError(nameof(vm.BankId), "حدد المصرف عند الدفع بالتحويل.");
            if (!isTransferPayment)
                vm.BankId = null;

            if (simplePosMode)
            {
                vm.DiscountValue ??= 0m;
                vm.Note = null;

                if (string.Equals(vm.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase))
                {
                    var requestedDiscountPercent = vm.DiscountValue ?? 0m;
                    if (requestedDiscountPercent > maxCashierDiscountPercent)
                        ModelState.AddModelError(nameof(vm.DiscountValue), $"الحد الأقصى لخصم الكاشير هو {maxCashierDiscountPercent:0.##}%.");
                }

                var itemIds = vm.Lines
                    .Where(l => l.ItemId != Guid.Empty)
                    .Select(l => l.ItemId)
                    .Distinct()
                    .ToList();

                var itemPriceMap = await _context.Set<Item>()
                    .AsNoTracking()
                    .Where(i => itemIds.Contains(i.Id))
                    .Select(i => new
                    {
                        i.Id,
                        i.Name,
                        Price = i.DefaultSalePriceLyd
                    })
                    .ToDictionaryAsync(i => i.Id, i => (i.Name, i.Price));

                foreach (var line in vm.Lines)
                {
                    if (!itemPriceMap.TryGetValue(line.ItemId, out var itemInfo) || !itemInfo.Price.HasValue || itemInfo.Price.Value <= 0)
                    {
                        var itemName = itemInfo.Name ?? "-";
                        ModelState.AddModelError("Lines", $"يجب تحديد سعر بيع افتراضي للصنف '{itemName}' من شاشة سياسة الأسعار.");
                        continue;
                    }

                    line.UnitPriceEur = itemInfo.Price.Value;
                }
            }

            if (!vm.Lines.Any())
                ModelState.AddModelError("Lines", "أضف سطر صنف واحد على الأقل.");

            if (vm.Lines.Any(l => !l.Qty.HasValue || l.Qty.Value <= 0))
                ModelState.AddModelError("Lines", "يجب أن تكون الكمية أكبر من صفر في جميع السطور.");

            if (vm.IsOnAccount && (!vm.CustomerId.HasValue || vm.CustomerId == Guid.Empty))
                ModelState.AddModelError(nameof(vm.CustomerId), "حدد العميل عند البيع الآجل.");

            if (!vm.IsOnAccount)
                vm.CustomerId = await GetOrCreateDailySalesCustomerIdAsync();

            if (vm.Lines.Any(l => !l.UnitPriceEur.HasValue))
                ModelState.AddModelError("Lines", "سعر الوحدة مطلوب لجميع السطور.");

            if (vm.Lines.Any(l => l.UnitPriceEur.HasValue && l.UnitPriceEur.Value < 0))
                ModelState.AddModelError("Lines", "لا يمكن أن يكون سعر الوحدة سالبا.");

            var discountValue = vm.DiscountValue ?? 0m;
            if (discountValue < 0)
                ModelState.AddModelError(nameof(vm.DiscountValue), "لا يمكن أن تكون قيمة الخصم سالبة.");

            if (!string.Equals(vm.DiscountType, "Amount", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(vm.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(vm.DiscountType), "نوع الخصم غير صالح.");

            if (string.Equals(vm.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase) && discountValue > 100)
                ModelState.AddModelError(nameof(vm.DiscountValue), "نسبة الخصم لا يمكن أن تتجاوز 100.");

            if (!ModelState.IsValid)
            {
                await LoadItemsAsync();
                await LoadCustomersAsync(vm.CustomerId);
                await LoadBanksAsync();
                return View(vm);
            }

            var baseLines = vm.Lines.Select(l => (
                l.ItemId,
                (decimal)l.Qty!.Value,
                l.UnitPriceEur!.Value
            )).ToList();

            var pricedLines = ApplyDiscount(baseLines, vm.DiscountType, discountValue, out var discountApplied);

            var notePrefix = vm.IsReturn ? "[POS-RETURN]" : "[POS-SALE]";
            var discountNote = discountApplied > 0
                ? $"خصم {vm.DiscountType}: {discountApplied:0.00} LYD"
                : "بدون خصم";
            var paymentNote = $"الدفع: {vm.PaymentMethod}";

            var invoice = new SalesInvoice
            {
                InvoiceDate = vm.InvoiceDate,
                CurrencyCode = vm.CurrencyCode,
                EurToDinarRateSnapshot = vm.EurToDinarRateSnapshot!.Value,
                Note = string.IsNullOrWhiteSpace(vm.Note)
                    ? $"{notePrefix} {paymentNote} | {discountNote}"
                    : $"{notePrefix} {vm.Note} | {paymentNote} | {discountNote}",
                CustomerId = vm.CustomerId,
                PaymentMethod = vm.PaymentMethod,
                BankId = vm.BankId,
                PosShiftId = activeShiftId,
                CreatedByUserId = _userManager.GetUserId(User),
                CreatedByUserName = User?.Identity?.Name
            };

            try
            {
                var id = vm.IsReturn
                    ? await _salesService.CreateReturnAsync(invoice, pricedLines)
                    : await _salesService.CreateAsync(invoice, pricedLines);

                if (vm.DraftId.HasValue)
                {
                    await DeleteDraftAsync(vm.DraftId.Value);
                }

                TempData["SuccessMessage"] = vm.IsReturn
                    ? "تم ترحيل فاتورة المرتجع بنجاح."
                    : "تم ترحيل فاتورة البيع بنجاح.";

                if (simplePosMode)
                {
                    // Old behavior kept in comment for traceability: always redirected to Create without any print hint.
                    if (vm.AutoPrintReceipt)
                    {
                        TempData["AutoPrintInvoiceId"] = id.ToString();
                    }
                    return RedirectToAction(nameof(Create));
                }

                if (vm.AutoPrintReceipt)
                {
                    return RedirectToAction(nameof(Receipt), new { id, autoPrint = true });
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty,
                    UserMessageSanitizer.Sanitize(ex.Message, "تعذر حفظ فاتورة البيع."));
                await LoadItemsAsync();
                await LoadCustomersAsync(vm.CustomerId);
                await LoadBanksAsync();
                ViewBag.HeldDrafts = await LoadHeldDraftsAsync();
                return View(vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "InventoryCreatePolicy")]
        public async Task<IActionResult> CreateCustomerInline([FromBody] CreateCustomerInlineRequest request)
        {
            var name = request.Name?.Trim() ?? string.Empty;
            var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { success = false, error = "اسم العميل مطلوب." });

            if (name.Length > 200)
                return BadRequest(new { success = false, error = "اسم العميل طويل جدا." });

            var exists = await _context.Set<Customer>()
                .AsNoTracking()
                .AnyAsync(c => c.Name == name);

            if (exists)
                return Conflict(new { success = false, error = "العميل موجود بالفعل." });

            var customer = new Customer
            {
                Name = name,
                Phone = phone
            };

            _context.Set<Customer>().Add(customer);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                customerId = customer.Id,
                customerName = customer.Name
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hold(SalesCreateVM vm)
        {
            try
            {
                // Old behavior kept in comment for traceability: InvoiceDate relied on posted value and could fail due client-side format differences.
                vm.InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

                var payload = new SalesDraftPayloadVM
                {
                    InvoiceDate = vm.InvoiceDate,
                    CurrencyCode = string.IsNullOrWhiteSpace(vm.CurrencyCode) ? "LYD" : vm.CurrencyCode.Trim().ToUpperInvariant(),
                    EurToDinarRateSnapshot = vm.EurToDinarRateSnapshot,
                    Note = vm.Note?.Trim(),
                    CustomerId = vm.CustomerId,
                    PaymentMethod = NormalizePaymentMethod(vm.IsOnAccount ? PaymentCredit : vm.PaymentMethod),
                    DiscountType = string.Equals(vm.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase) ? "Percent" : "Amount",
                    DiscountValue = vm.DiscountValue,
                    IsReturn = vm.IsReturn,
                    AutoPrintReceipt = vm.AutoPrintReceipt,
                    Lines = vm.Lines?
                        .Where(l => l != null)
                        .Select(l => new SalesDraftLineVM
                        {
                            ItemId = l.ItemId,
                            Qty = l.Qty,
                            UnitPriceEur = l.UnitPriceEur
                        })
                        .ToList() ?? new List<SalesDraftLineVM>()
                };

                var totals = CalculateDraftTotals(payload);
                var customerName = await GetCustomerNameAsync(payload.CustomerId);
                var draft = await GetDraftForEditAsync(vm.DraftId);

                draft.Title = BuildDraftTitle(payload, customerName, totals.LineCount);
                draft.InvoiceDate = payload.InvoiceDate;
                draft.CurrencyCode = payload.CurrencyCode;
                draft.EurToDinarRateSnapshot = payload.EurToDinarRateSnapshot ?? 1m;
                draft.DiscountType = payload.DiscountType;
                draft.DiscountValue = payload.DiscountValue ?? 0m;
                draft.IsReturn = payload.IsReturn;
                draft.CustomerId = payload.CustomerId;
                draft.CustomerNameSnapshot = customerName;
                draft.Note = payload.Note;
                draft.PayloadJson = JsonSerializer.Serialize(payload);
                draft.SubtotalEur = totals.SubtotalEur;
                draft.DiscountEur = totals.DiscountEur;
                draft.NetEur = totals.NetEur;
                draft.NetDinar = totals.NetDinar;
                draft.LineCount = totals.LineCount;
                draft.CreatedByUserId = _userManager.GetUserId(User);
                draft.CreatedByUserName = User?.Identity?.Name;

                if (draft.Id == Guid.Empty)
                {
                    _context.Set<SalesInvoiceDraft>().Add(draft);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    draftId = draft.Id,
                    message = "تم تعليق الفاتورة بنجاح.",
                    redirectUrl = Url.Action(nameof(Drafts))
                });
            }
            catch (Exception ex)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new { success = false, error = UserMessageSanitizer.Sanitize(ex.Message, "تعذر تعليق الفاتورة.") });
            }
        }

        [HttpGet]
        public IActionResult RestoreDraft(Guid id)
        {
            return RedirectToAction(nameof(Create), new { draftId = id });
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(Guid id, bool autoPrint = true)
        {
            var invoice = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return View("NotFound");

            if (!CanViewAllInvoices() && invoice.CreatedByUserId != _userManager.GetUserId(User))
                return Forbid();

            var lines = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => l.SalesInvoiceId == id)
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
            ViewBag.AutoPrint = autoPrint;
            ViewBag.ShopName = siteInfo?.Name;
            ViewBag.ShopLogo = siteInfo?.LogoUrl;
            ViewBag.ShopActivity = siteInfo?.Activity;
            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDraft(Guid id)
        {
            var draft = await _context.Set<SalesInvoiceDraft>().FirstOrDefaultAsync(d => d.Id == id);
            if (draft == null)
                return NotFound();

            if (!CanAccessDraft(draft))
                return Forbid();

            _context.Set<SalesInvoiceDraft>().Remove(draft);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الفاتورة المعلقة.";
            return RedirectToAction(nameof(Create));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Approve(SalesApproveVM vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "يرجى تعبئة جميع حقول الاعتماد المطلوبة.";
                return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
            }

            if (vm.LinePrices == null || vm.LinePrices.Count == 0)
            {
                TempData["ErrorMessage"] = "مطلوب سعر سطر واحد على الأقل.";
                return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
            }

            if (vm.LinePrices.Any(l => l.UnitPriceEur < 0))
            {
                TempData["ErrorMessage"] = "لا يمكن أن يكون سعر الوحدة سالبا.";
                return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
            }

            if (vm.LinePrices.GroupBy(l => l.LineId).Any(g => g.Count() > 1))
            {
                TempData["ErrorMessage"] = "تم اكتشاف إدخالات مكررة لأسعار السطور.";
                return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
            }

            try
            {
                var linePrices = vm.LinePrices.Select(l => (l.LineId, l.UnitPriceEur));
                await _salesService.ApprovePendingAsync(
                    vm.InvoiceId,
                    vm.CustomerId,
                    vm.EurToDinarRateSnapshot,
                    linePrices,
                    User?.Identity?.Name);

                TempData["SuccessMessage"] = "تم اعتماد فاتورة البيع بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = UserMessageSanitizer.Sanitize(ex.Message, "تعذر اعتماد فاتورة البيع.");
            }

            return RedirectToAction(nameof(Details), new { id = vm.InvoiceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Prog")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            try
            {
                await _salesService.CancelAsync(id, User?.Identity?.Name);
                TempData["SuccessMessage"] = "تم إلغاء فاتورة البيع.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = UserMessageSanitizer.Sanitize(ex.Message, "تعذر إلغاء فاتورة البيع.");
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Report(DateOnly? from, DateOnly? to, Guid? customerId, string? paymentMethod)
        {
            ViewBag.SecondaryCurrencyCode = GetSecondaryCurrencyCode();
            var userId = _userManager.GetUserId(User);
            var canViewAll = CanViewAllInvoices();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var query = _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted");

            if (canViewAll)
            {
                if (from.HasValue)
                    query = query.Where(i => i.InvoiceDate >= from.Value);
                if (to.HasValue)
                    query = query.Where(i => i.InvoiceDate <= to.Value);
            }
            else
            {
                query = query.Where(i => i.CreatedByUserId == userId && i.InvoiceDate == today);
            }
            if (customerId.HasValue && customerId.Value != Guid.Empty)
                query = query.Where(i => i.CustomerId == customerId.Value);

            var normalizedPayment = (paymentMethod ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedPayment) && !normalizedPayment.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => (i.PaymentMethod ?? "Cash") == normalizedPayment);
            }

            var rows = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new SalesReportRowVM
                {
                    Number = i.Number,
                    InvoiceDate = i.InvoiceDate,
                    TotalDinar = i.TotalDinar,
                    Status = i.Status,
                    IsReturn = (i.Note != null && i.Note.Contains("[POS-RETURN]")) || i.TotalDinar < 0,
                    PaymentMethod = ToArabicPaymentMethod(i.PaymentMethod),
                    BankName = i.Bank != null ? i.Bank.Name : null,
                    CustomerName = i.Customer == null || i.Customer.Name == DailySalesCustomerName ? "-" : i.Customer.Name
                })
                .ToListAsync();

            var grossSales = rows.Where(r => !r.IsReturn).Sum(r => r.TotalDinar);
            var returns = rows.Where(r => r.IsReturn).Sum(r => Math.Abs(r.TotalDinar));
            var netSales = grossSales - returns;

            var vm = new SalesReportVM
            {
                From = from,
                To = to,
                CustomerId = customerId,
                PaymentMethod = normalizedPayment,
                Rows = rows,
                SumDinar = grossSales,
                ReturnsLyd = returns,
                NetSalesLyd = netSales,
                CashSalesLyd = rows.Where(r => !r.IsReturn && r.PaymentMethod == "نقدي").Sum(r => r.TotalDinar),
                CardSalesLyd = rows.Where(r => !r.IsReturn && r.PaymentMethod == "بطاقة").Sum(r => r.TotalDinar),
                TransferSalesLyd = rows.Where(r => !r.IsReturn && r.PaymentMethod == "تحويل").Sum(r => r.TotalDinar),
                CreditSalesLyd = rows.Where(r => !r.IsReturn && r.PaymentMethod == "آجل").Sum(r => r.TotalDinar)
            };

            await LoadCustomersAsync(customerId);
            ViewData["PaymentMethods"] = new SelectList(new[]
            {
                new { Value = "All", Text = "كل الطرق" },
                new { Value = "Cash", Text = "نقدي" },
                new { Value = "Card", Text = "بطاقة" },
                new { Value = "Transfer", Text = "تحويل" },
                new { Value = "Credit", Text = "آجل" }
            }, "Value", "Text", string.IsNullOrWhiteSpace(normalizedPayment) ? "All" : normalizedPayment);

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> TodayReport(DateOnly? date)
        {
            var reportDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var userId = _userManager.GetUserId(User);

            var rows = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.InvoiceDate == reportDate)
                .Where(i => i.Status != "Cancelled")
                .Where(i => CanViewAllInvoices() || i.CreatedByUserId == userId)
                .OrderByDescending(i => i.Created)
                .Select(i => new SalesDailyInvoiceRowVM
                {
                    InvoiceId = i.Id,
                    Number = i.Number,
                    InvoiceDate = i.InvoiceDate,
                    CreatedAt = i.Created,
                    CustomerName = i.Customer == null || i.Customer.Name == DailySalesCustomerName ? "-" : i.Customer.Name,
                    Status = i.Status,
                    PaymentMethod = ToArabicPaymentMethod(i.PaymentMethod),
                    BankName = i.Bank != null ? i.Bank.Name : null,
                    IsReturn = (i.Note != null && i.Note.Contains("[POS-RETURN]")) || i.TotalDinar < 0,
                    TotalDinar = i.TotalDinar
                })
                .ToListAsync();

            var vm = new SalesDailyInvoicesVM
            {
                ReportDate = reportDate,
                Rows = rows
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
            ViewBag.ItemDefaultPricesJson = JsonSerializer.Serialize(
                items.ToDictionary(i => i.Id.ToString().ToLowerInvariant(), i => i.DefaultSalePriceLyd ?? 0m)
            );
        }

        private async Task LoadCustomersAsync(Guid? selectedCustomerId = null)
        {
            var customers = await _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id != DailySalesCustomerSeedId && c.Name != DailySalesCustomerName)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewData["Customers"] = new SelectList(customers, "Id", "Name", selectedCustomerId);
        }

        private async Task LoadBanksAsync(Guid? selectedBankId = null)
        {
            var banks = await _context.Set<Bank>()
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .ToListAsync();
            ViewData["Banks"] = new SelectList(banks, "Id", "Name", selectedBankId);
        }

        private bool CanApprovePendingInvoices()
        {
            return User.IsInRole("Admin") || User.IsInRole("Prog");
        }

        private bool IsSimplePosMode()
        {
            return User.IsInRole("Cashier") || User.IsInRole("Employee") || User.IsInRole("SalesOfficer");
        }

        private async Task<bool> HasOpenShiftAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _context.Set<PosShift>()
                .AsNoTracking()
                .AnyAsync(s => s.OpenedByUserId == userId && s.Status == "Open");
        }

        private async Task<Guid?> GetOpenShiftIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _context.Set<PosShift>()
                .AsNoTracking()
                .Where(s => s.OpenedByUserId == userId && s.Status == "Open")
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        public sealed class CreateCustomerInlineRequest
        {
            public string? Name { get; set; }
            public string? Phone { get; set; }
        }

        private string GetSecondaryCurrencyCode()
        {
            return _context.Set<InventorySettings>()
                       .AsNoTracking()
                       .Select(s => s.DinarCurrencyCode)
                       .FirstOrDefault()
                   ?? "USD";
        }

        private async Task<decimal> GetMaxCashierDiscountPercentAsync()
        {
            var value = await _context.Set<InventorySettings>()
                .AsNoTracking()
                .Select(s => s.MaxCashierDiscountPercent)
                .FirstOrDefaultAsync();

            if (value <= 0)
                return 10m;

            return Math.Min(value, 100m);
        }

        private async Task<List<SalesInvoiceDraftSummaryVM>> LoadHeldDraftsAsync()
        {
            var query = _context.Set<SalesInvoiceDraft>().AsNoTracking();
            if (!CanSeeAllDrafts())
            {
                var userId = _userManager.GetUserId(User);
                query = query.Where(d => d.CreatedByUserId == userId);
            }

            var drafts = await query
                .OrderByDescending(d => d.Modified ?? d.Created)
                .ThenByDescending(d => d.Created)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.InvoiceDate,
                    d.CustomerNameSnapshot,
                    d.IsReturn,
                    d.LineCount,
                    d.NetEur,
                    d.NetDinar,
                    d.Created,
                    d.Modified,
                    d.PayloadJson
                })
                .ToListAsync();

            var result = new List<SalesInvoiceDraftSummaryVM>();
            var allItemIds = new HashSet<Guid>();
            var draftPayloads = new Dictionary<Guid, SalesDraftPayloadVM>();

            foreach (var d in drafts)
            {
                var vm = new SalesInvoiceDraftSummaryVM
                {
                    Id = d.Id,
                    Title = d.Title,
                    InvoiceDate = d.InvoiceDate,
                    CustomerName = d.CustomerNameSnapshot,
                    PaymentMethod = "Cash",
                    IsReturn = d.IsReturn,
                    LineCount = d.LineCount,
                    NetEur = d.NetEur,
                    NetDinar = d.NetDinar,
                    Created = d.Created,
                    Modified = d.Modified
                };

                try
                {
                    var payload = JsonSerializer.Deserialize<SalesDraftPayloadVM>(d.PayloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (payload != null && payload.Lines != null)
                    {
                        vm.PaymentMethod = payload.PaymentMethod ?? "Cash";
                        draftPayloads[d.Id] = payload;
                        foreach (var line in payload.Lines)
                        {
                            allItemIds.Add(line.ItemId);
                        }
                    }
                }
                catch { }

                result.Add(vm);
            }

            if (allItemIds.Any())
            {
                var itemNames = await _context.Items
                    .Where(i => allItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.Name })
                    .ToDictionaryAsync(i => i.Id, i => i.Name);

                foreach (var vm in result)
                {
                    if (draftPayloads.TryGetValue(vm.Id, out var payload))
                    {
                        var namesForDraft = payload.Lines
                            .Select(l => itemNames.TryGetValue(l.ItemId, out var n) ? n : "")
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList();
                        vm.ItemNames = namesForDraft;
                    }
                }
            }

            return result;
        }

        private async Task<SalesCreateVM?> LoadDraftAsync(Guid draftId)
        {
            var draft = await _context.Set<SalesInvoiceDraft>()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == draftId);

            if (draft == null || !CanAccessDraft(draft))
                return null;

            var payload = JsonSerializer.Deserialize<SalesDraftPayloadVM>(draft.PayloadJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
                return null;

            return new SalesCreateVM
            {
                DraftId = draft.Id,
                InvoiceDate = payload.InvoiceDate,
                CurrencyCode = payload.CurrencyCode,
                EurToDinarRateSnapshot = payload.EurToDinarRateSnapshot,
                Note = payload.Note,
                CustomerId = payload.CustomerId,
                PaymentMethod = NormalizePaymentMethod(payload.PaymentMethod),
                IsOnAccount = string.Equals(payload.PaymentMethod, PaymentCredit, StringComparison.OrdinalIgnoreCase),
                DiscountType = payload.DiscountType,
                DiscountValue = payload.DiscountValue,
                IsReturn = payload.IsReturn,
                AutoPrintReceipt = payload.AutoPrintReceipt,
                Lines = payload.Lines?.Select(l => new SalesLineInputVM
                {
                    ItemId = l.ItemId,
                    Qty = l.Qty,
                    UnitPriceEur = l.UnitPriceEur
                }).ToList() ?? new List<SalesLineInputVM>()
            };
        }

        private async Task<SalesInvoiceDraft> GetDraftForEditAsync(Guid? draftId)
        {
            if (draftId.HasValue)
            {
                var existing = await _context.Set<SalesInvoiceDraft>().FirstOrDefaultAsync(d => d.Id == draftId.Value);
                if (existing != null)
                {
                    if (!CanAccessDraft(existing))
                        throw new UnauthorizedAccessException("ليست لديك صلاحية تعديل هذه الفاتورة المعلقة.");

                    return existing;
                }

                throw new InvalidOperationException("لم يتم العثور على الفاتورة المعلقة.");
            }

            return new SalesInvoiceDraft();
        }

        private async Task DeleteDraftAsync(Guid draftId)
        {
            var draft = await _context.Set<SalesInvoiceDraft>().FirstOrDefaultAsync(d => d.Id == draftId);
            if (draft == null || !CanAccessDraft(draft))
                return;

            _context.Set<SalesInvoiceDraft>().Remove(draft);
            await _context.SaveChangesAsync();
        }

        private async Task<string?> GetCustomerNameAsync(Guid? customerId)
        {
            if (!customerId.HasValue)
                return null;

            return await _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id == customerId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();
        }

        private async Task<Guid> GetOrCreateDailySalesCustomerIdAsync()
        {
            var existingCustomerId = await _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id == DailySalesCustomerSeedId || c.Name == DailySalesCustomerName)
                .OrderBy(c => c.Id == DailySalesCustomerSeedId ? 0 : 1)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            if (existingCustomerId != Guid.Empty)
                return existingCustomerId;

            var customer = new Customer
            {
                Id = DailySalesCustomerSeedId,
                Name = DailySalesCustomerName,
                Note = "عميل افتراضي لمبيعات الكاش اليومية"
            };

            _context.Set<Customer>().Add(customer);

            try
            {
                await _context.SaveChangesAsync();
                return customer.Id;
            }
            catch (DbUpdateException)
            {
                var fallbackId = await _context.Set<Customer>()
                    .AsNoTracking()
                    .Where(c => c.Name == DailySalesCustomerName)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync();

                if (fallbackId != Guid.Empty)
                    return fallbackId;

                throw;
            }
        }

        private bool CanViewAllInvoices()
        {
            return User.IsInRole("Admin") || User.IsInRole("Prog") || User.IsInRole("SalesManager");
        }

        private bool CanSeeAllDrafts()
        {
            return User.IsInRole("Admin") || User.IsInRole("Prog") || User.IsInRole("SalesManager");
        }

        private bool CanAccessDraft(SalesInvoiceDraft draft)
        {
            return CanSeeAllDrafts() || draft.CreatedByUserId == _userManager.GetUserId(User);
        }

        private static (decimal SubtotalEur, decimal DiscountEur, decimal NetEur, decimal NetDinar, int LineCount) CalculateDraftTotals(SalesDraftPayloadVM payload)
        {
            var normalizedLines = payload.Lines?
                .Where(l => l != null)
                .Select(l => (
                    Qty: (decimal)(l.Qty ?? 0),
                    UnitPriceEur: l.UnitPriceEur ?? 0m))
                .Where(l => l.Qty > 0m && l.UnitPriceEur >= 0m)
                .ToList() ?? new List<(decimal Qty, decimal UnitPriceEur)>();

            var subtotal = normalizedLines.Sum(l => RoundMoney(l.Qty * l.UnitPriceEur));
            var discountValue = payload.DiscountValue ?? 0m;
            var discount = string.Equals(payload.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase)
                ? RoundMoney(subtotal * (discountValue / 100m))
                : RoundMoney(discountValue);

            discount = Math.Min(subtotal, Math.Max(0m, discount));
            var net = RoundMoney(subtotal - discount);
            var rate = payload.EurToDinarRateSnapshot.GetValueOrDefault(1m);
            var netDinar = RoundMoney(net * rate);

            return (subtotal, discount, net, netDinar, normalizedLines.Count);
        }

        private static string BuildDraftTitle(SalesDraftPayloadVM payload, string? customerName, int lineCount)
        {
            var kind = payload.IsReturn ? "مرتجع" : "بيع";
            var customer = string.IsNullOrWhiteSpace(customerName) ? "عميل نقدي" : customerName.Trim();
            var paymentMethod = string.IsNullOrWhiteSpace(payload.PaymentMethod)
                ? PaymentCash
                : payload.PaymentMethod.Trim();
            return $"{kind} - {customer} - {ToArabicPaymentMethod(paymentMethod)} - {lineCount} سطر";
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            var normalized = (paymentMethod ?? string.Empty).Trim();
            if (normalized.Equals(PaymentCard, StringComparison.OrdinalIgnoreCase))
                return PaymentCard;
            if (normalized.Equals(PaymentTransfer, StringComparison.OrdinalIgnoreCase))
                return PaymentTransfer;
            if (normalized.Equals(PaymentCredit, StringComparison.OrdinalIgnoreCase))
                return PaymentCredit;

            return PaymentCash;
        }

        private static string ToArabicPaymentMethod(string? paymentMethod)
        {
            var normalized = NormalizePaymentMethod(paymentMethod);
            if (normalized == PaymentCard)
                return "بطاقة";
            if (normalized == PaymentTransfer)
                return "تحويل";
            if (normalized == PaymentCredit)
                return "آجل";

            return "نقدي";
        }

        private static List<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> ApplyDiscount(
            IReadOnlyList<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> lines,
            string? discountType,
            decimal discountValue,
            out decimal appliedDiscount)
        {
            appliedDiscount = 0m;
            var subtotal = lines.Sum(l => RoundMoney(l.Qty * l.UnitPriceEur));
            if (subtotal <= 0 || discountValue <= 0)
                return lines.ToList();

            var isPercent = string.Equals(discountType, "Percent", StringComparison.OrdinalIgnoreCase);
            var requestedDiscount = isPercent
                ? RoundMoney(subtotal * (discountValue / 100m))
                : RoundMoney(discountValue);

            appliedDiscount = Math.Min(subtotal, requestedDiscount);
            if (appliedDiscount <= 0)
                return lines.ToList();

            var adjusted = new List<(Guid ItemId, decimal Qty, decimal UnitPriceEur)>(lines.Count);
            decimal allocated = 0m;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var lineTotal = RoundMoney(line.Qty * line.UnitPriceEur);
                decimal lineDiscount;

                if (i == lines.Count - 1)
                {
                    lineDiscount = RoundMoney(appliedDiscount - allocated);
                }
                else
                {
                    lineDiscount = subtotal <= 0
                        ? 0
                        : RoundMoney(appliedDiscount * (lineTotal / subtotal));
                    allocated += lineDiscount;
                }

                if (lineDiscount > lineTotal)
                    lineDiscount = lineTotal;

                var netLineTotal = RoundMoney(lineTotal - lineDiscount);
                var netUnitPrice = line.Qty <= 0 ? 0 : RoundMoney(netLineTotal / line.Qty);

                adjusted.Add((line.ItemId, line.Qty, netUnitPrice));
            }

            return adjusted;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.ToEven);
        }
    }
}
