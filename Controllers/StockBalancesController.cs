using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.Models.Services;
using NewsApp2.ViewModels.Inventory;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "InventoryEditPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    [Authorize(Policy = "NotCashierPolicy")]
    public class StockBalancesController : Controller
    {
        private readonly IUnitOfWork<InvStockBalance> _balances;
        private readonly IUnitOfWork<Category> _categories;
        private readonly IUnitOfWork<Item> _items;
        private readonly AppDbContext _context;
        private readonly InventoryService _inventoryService;

        public StockBalancesController(
            IUnitOfWork<InvStockBalance> balances,
            IUnitOfWork<Category> categories,
            IUnitOfWork<Item> items,
            AppDbContext context,
            InventoryService inventoryService)
        {
            _balances = balances;
            _categories = categories;
            _items = items;
            _context = context;
            _inventoryService = inventoryService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, Guid? categoryId, bool lowOnly = false, decimal? lowThreshold = null, int page = 1)
        {
            var threshold = lowThreshold.HasValue && lowThreshold.Value > 0 ? lowThreshold.Value : 5m;

            var rows = await BuildStockRowsAsync(search, categoryId, threshold);
            if (lowOnly)
                rows = rows.Where(r => r.Status != "جيد").ToList();

            var vm = new StockOverviewVM
            {
                Search = search,
                CategoryId = categoryId,
                LowOnly = lowOnly,
                LowThreshold = threshold,
                CanViewFinancials = CanViewStockFinancials(),
                TotalItems = rows.Count,
                TotalQuantity = rows.Sum(r => r.QuantityOnHand),
                TotalCostValueLyd = rows.Sum(r => r.CostValueLyd),
                TotalSaleValueLyd = rows.Sum(r => r.SaleValueLyd),
                TotalExpectedProfitLyd = rows.Sum(r => r.ExpectedProfitLyd),
                LowCount = rows.Count(r => r.Status == "منخفض"),
                OutOfStockCount = rows.Count(r => r.Status == "نفذ")
            };

            const int pageSize = 50;
            page = NewsApp2.ViewModels.Common.PaginationVM.NormalizePage(page);
            vm.Pagination = new NewsApp2.ViewModels.Common.PaginationVM { Page = page, PageSize = pageSize, TotalCount = rows.Count };
            vm.Rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            await LoadLookups(categoryId);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportStockExcel(string? search, Guid? categoryId, bool lowOnly = false, decimal? lowThreshold = null)
        {
            var threshold = lowThreshold.HasValue && lowThreshold.Value > 0 ? lowThreshold.Value : 5m;
            var canFin = CanViewStockFinancials();
            var rows = await BuildStockRowsAsync(search, categoryId, threshold);
            if (lowOnly)
                rows = rows.Where(r => r.Status != "جيد").ToList();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("المخزون");

            var headers = new List<string> { "التصنيف", "الصنف", "الباركود", "الكمية", "إعادة الطلب" };
            if (canFin)
                headers.AddRange(new[] { "متوسط التكلفة", "سعر البيع", "قيمة التكلفة", "قيمة البيع", "الربح المتوقع" });
            headers.Add("الحالة");

            for (var c = 0; c < headers.Count; c++)
                sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Range(1, 1, 1, headers.Count).Style.Font.Bold = true;

            var r = 2;
            foreach (var row in rows)
            {
                var col = 1;
                sheet.Cell(r, col++).Value = row.CategoryName ?? string.Empty;
                sheet.Cell(r, col++).Value = row.ItemName;
                sheet.Cell(r, col++).Value = row.Barcode ?? string.Empty;
                sheet.Cell(r, col++).Value = row.QuantityOnHand;
                sheet.Cell(r, col++).Value = row.ReorderLevel;
                if (canFin)
                {
                    sheet.Cell(r, col++).Value = row.AverageCostLyd;
                    sheet.Cell(r, col++).Value = row.DefaultSalePriceLyd ?? 0m;
                    sheet.Cell(r, col++).Value = row.CostValueLyd;
                    sheet.Cell(r, col++).Value = row.SaleValueLyd;
                    sheet.Cell(r, col++).Value = row.ExpectedProfitLyd;
                }
                sheet.Cell(r, col).Value = row.Status;
                r++;
            }

            sheet.Cell(r, 3).Value = "الإجمالي";
            sheet.Cell(r, 4).Value = rows.Sum(x => x.QuantityOnHand);
            if (canFin)
            {
                sheet.Cell(r, 8).Value = rows.Sum(x => x.CostValueLyd);
                sheet.Cell(r, 9).Value = rows.Sum(x => x.SaleValueLyd);
                sheet.Cell(r, 10).Value = rows.Sum(x => x.ExpectedProfitLyd);
                sheet.Range(r, 3, r, 10).Style.Font.Bold = true;
            }
            else
            {
                sheet.Range(r, 3, r, 4).Style.Font.Bold = true;
            }

            sheet.Columns().AdjustToContents();

            await using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var fileName = $"stock_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private bool CanViewStockFinancials()
            => User.IsInRole("Admin") || User.IsInRole("Prog") || User.IsInRole("SalesManager");

        private async Task<List<StockOverviewRow>> BuildStockRowsAsync(string? search, Guid? categoryId, decimal threshold)
        {
            IQueryable<InvStockBalance> query = _context.Set<InvStockBalance>()
                .AsNoTracking()
                .Include(b => b.Item)
                .ThenInclude(i => i.Category)
                .Where(b => b.Item != null);

            if (categoryId.HasValue && categoryId != Guid.Empty)
                query = query.Where(b => b.Item!.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b => b.Item!.Name.Contains(term) || (b.Item.Barcode != null && b.Item.Barcode.Contains(term)));
            }

            var list = await query.OrderBy(b => b.Item!.Name).ToListAsync();

            return list.Select(b =>
            {
                var reorder = b.Item?.ReorderLevel ?? 0m;
                var limit = reorder > 0 ? reorder : threshold;
                var qty = b.QuantityOnHand;
                var status = qty <= 0 ? "نفذ" : (qty <= limit ? "منخفض" : "جيد");
                var salePrice = b.Item?.DefaultSalePriceLyd;
                var costValue = qty * b.AverageCostLyd;
                var saleValue = qty * (salePrice ?? 0m);
                return new StockOverviewRow
                {
                    ItemId = b.ItemId,
                    ItemName = b.Item?.Name ?? "-",
                    Barcode = b.Item?.Barcode,
                    CategoryName = b.Item?.Category?.Name,
                    QuantityOnHand = qty,
                    ReorderLevel = reorder,
                    AverageCostLyd = b.AverageCostLyd,
                    DefaultSalePriceLyd = salePrice,
                    CostValueLyd = costValue,
                    SaleValueLyd = saleValue,
                    ExpectedProfitLyd = salePrice.HasValue ? (saleValue - costValue) : 0m,
                    Status = status
                };
            }).ToList();
        }

        [HttpGet]
        public async Task<IActionResult> Movement(DateOnly? from, DateOnly? to, Guid? itemId, string? referenceType)
        {
            if (User.IsInRole("Cashier"))
                return Forbid();

            var query = _context.Set<InvStockLedger>()
                .AsNoTracking()
                .Include(l => l.Item)
                .AsQueryable();

            if (itemId.HasValue && itemId.Value != Guid.Empty)
                query = query.Where(l => l.ItemId == itemId.Value);

            if (!string.IsNullOrWhiteSpace(referenceType))
                query = query.Where(l => l.ReferenceType == referenceType);

            var ledgerRows = await query
                .Select(l => new
                {
                    l.Created,
                    l.ItemId,
                    ItemName = l.Item != null ? l.Item.Name : string.Empty,
                    l.MovementType,
                    l.ReferenceType,
                    l.ReferenceId,
                    l.QuantityChange,
                    l.BalanceAfter,
                    l.Note
                })
                .ToListAsync();

            var businessDateMap = await BuildBusinessDateMapAsync(ledgerRows.Select(l => (l.ReferenceType, l.ReferenceId)));

            var filteredRows = ledgerRows
                .Where(l =>
                {
                    var businessDate = ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDateMap);
                    if (from.HasValue && businessDate < from.Value)
                        return false;
                    if (to.HasValue && businessDate > to.Value)
                        return false;
                    return true;
                })
                .OrderByDescending(l => ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDateMap))
                .ThenByDescending(l => l.Created)
                .ToList();

            var rows = filteredRows
                .Select(l => new StockMovementRowVM
                {
                    Created = ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDateMap).ToDateTime(TimeOnly.MinValue),
                    ItemId = l.ItemId,
                    ItemName = l.ItemName,
                    MovementType = l.MovementType,
                    ReferenceType = l.ReferenceType,
                    ReferenceId = l.ReferenceId,
                    QuantityChange = l.QuantityChange,
                    BalanceAfter = l.BalanceAfter,
                    Note = l.Note
                })
                .ToList();

            var vm = new StockMovementReportVM
            {
                From = from,
                To = to,
                ItemId = itemId,
                ReferenceType = referenceType,
                Rows = rows,
                TotalIn = rows.Where(r => r.QuantityChange > 0).Sum(r => r.QuantityChange),
                TotalOut = rows.Where(r => r.QuantityChange < 0).Sum(r => Math.Abs(r.QuantityChange))
            };

            var items = await _items.Repository.GetAll().OrderBy(i => i.Name).ToListAsync();
            var refTypes = await _context.Set<InvStockLedger>()
                .AsNoTracking()
                .Select(l => l.ReferenceType)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewData["Items"] = new SelectList(items, "Id", "Name", itemId);
            ViewData["ReferenceTypes"] = new SelectList(refTypes, referenceType);

            return View(vm);
        }

        public async Task<IActionResult> DailyInventory(DateOnly? date)
        {
            if (User.IsInRole("Cashier")) 
                return Forbid();

            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var itemsQuery = _context.Items.AsNoTracking().Include(i => i.Category).AsQueryable();

            var allLedgers = await _context.Set<InvStockLedger>()
                .AsNoTracking()
                .Select(l => new
                {
                    l.ItemId,
                    l.ReferenceType,
                    l.ReferenceId,
                    l.QuantityChange,
                    l.UnitCostLyd,
                    l.ValueChangeLyd,
                    l.Created
                })
                .ToListAsync();

            var businessDateMap = await BuildBusinessDateMapAsync(allLedgers.Select(l => (l.ReferenceType, l.ReferenceId)));

            var openingBalances = allLedgers
                .Where(l => ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDateMap) < targetDate)
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityChange));

            var dailyMovements = allLedgers
                .Where(l => ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDateMap) == targetDate)
                .GroupBy(l => new { l.ItemId, l.ReferenceType })
                .Select(g => new { g.Key.ItemId, g.Key.ReferenceType, TotalChange = g.Sum(l => l.QuantityChange) })
                .ToList();

            var resultList = new List<NewsApp2.ViewModels.Inventory.DailyInventoryRowVM>();
            var items = await itemsQuery.ToListAsync();

            // preload stock balances and sales lines for value calculations
            var stockByItem = await _context.Set<InvStockBalance>().AsNoTracking().ToDictionaryAsync(s => s.ItemId, s => s);
            var itemIds = items.Select(i => i.Id).ToList();
            var salesLines = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => itemIds.Contains(l.ItemId))
                .Select(l => new { l.ItemId, l.SalesInvoiceId, l.LineCostDinar, l.Qty })
                .ToListAsync();

            foreach(var item in items)
            {
                var opening = openingBalances.GetValueOrDefault(item.Id, 0m);
                var moves = dailyMovements.Where(m => m.ItemId == item.Id).ToList();
                
                var purchases = moves.Where(m => m.ReferenceType.Contains("Purchase", StringComparison.OrdinalIgnoreCase)).Sum(m => m.TotalChange);
                var sales = moves.Where(m => m.ReferenceType.StartsWith("SalesInvoice", StringComparison.OrdinalIgnoreCase)).Sum(m => m.TotalChange);
                var returns = moves.Where(m => m.ReferenceType.StartsWith("SalesReturn", StringComparison.OrdinalIgnoreCase)).Sum(m => m.TotalChange);
                var other = moves.Where(m => !m.ReferenceType.Contains("Purchase", StringComparison.OrdinalIgnoreCase) && !m.ReferenceType.StartsWith("Sales", StringComparison.OrdinalIgnoreCase)).Sum(m => m.TotalChange);

                var received = purchases + (other > 0 ? other : 0);
                var sold = Math.Abs(sales) + (other < 0 ? Math.Abs(other) : 0);
                
                var closing = opening + purchases + sales + returns + other;

                var closingValue = allLedgers
                    .Where(l => l.ItemId == item.Id && ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDateMap) <= targetDate)
                    .Sum(l => ResolveValueChange(l.QuantityChange, l.UnitCostLyd, l.ValueChangeLyd));

                if (opening == 0 && closing == 0 && received == 0 && sold == 0 && returns == 0)
                    continue;

                // compute sold value for the date (sum COGS for sales lines on this business date)
                var soldValue = salesLines
                    .Where(sl => sl.ItemId == item.Id
                        && ResolveBusinessDate("SalesInvoice", sl.SalesInvoiceId, DateTime.UtcNow, businessDateMap) == targetDate
                        && sl.Qty > 0)
                    .Sum(sl => sl.LineCostDinar);

                resultList.Add(new NewsApp2.ViewModels.Inventory.DailyInventoryRowVM
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    CategoryName = item.Category?.Name ?? "",
                    Barcode = item.Barcode ?? "",
                    OpeningBalance = opening,
                    Received = received,
                    Sold = sold,
                    Returns = returns,
                    ClosingBalance = closing,
                    SoldValueLyd = soldValue,
                    ClosingValueLyd = closingValue
                });
            }

            var vm = new NewsApp2.ViewModels.Inventory.DailyInventoryReportVM
            {
                ReportDate = targetDate,
                Rows = resultList.OrderBy(x => x.CategoryName).ThenBy(x => x.ItemName).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ItemCard(Guid? id, DateOnly? from, DateOnly? to)
        {
            if (User.IsInRole("Cashier"))
                return Forbid();

            var selectedItemId = id ?? Guid.Empty;
            if (selectedItemId == Guid.Empty)
            {
                var items = await _items.Repository.GetAll()
                    .AsNoTracking()
                    .OrderBy(i => i.Name)
                    .ToListAsync();

                ViewBag.Items = new SelectList(items, "Id", "Name");
                ViewBag.OpeningQty = 0m;
                ViewBag.ClosingQty = 0m;
                ViewBag.ClosingValue = 0m;
                ViewData["ItemId"] = Guid.Empty;
                return View(new List<ItemCardRowVM>());
            }

            DateTime? fromDt = from.HasValue ? from.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null;
            DateTime? toDt = to.HasValue ? to.Value.ToDateTime(TimeOnly.MaxValue) : (DateTime?)null;

            var (rows, openingQty, closingQty, closingValue) = await _inventoryService.GetItemCardAsync(selectedItemId, fromDt, toDt);

            ViewBag.OpeningQty = openingQty;
            ViewBag.ClosingQty = closingQty;
            ViewBag.ClosingValue = closingValue;

            ViewData["ItemId"] = selectedItemId;

            return View(rows);
        }

        private static string BuildBusinessDateKey(string referenceType, Guid referenceId)
            => $"{referenceType}:{referenceId}";

        private static decimal ResolveValueChange(decimal quantityChange, decimal unitCost, decimal storedValueChange)
        {
            if (storedValueChange != 0m || quantityChange == 0m)
                return storedValueChange;

            return quantityChange * unitCost;
        }

        private static DateOnly ResolveBusinessDate(string referenceType, Guid referenceId, DateTime createdUtc, IReadOnlyDictionary<string, DateOnly> businessDateMap)
        {
            var key = BuildBusinessDateKey(referenceType, referenceId);
            if (businessDateMap.TryGetValue(key, out var businessDate))
                return businessDate;

            return DateOnly.FromDateTime(createdUtc);
        }

        private async Task<Dictionary<string, DateOnly>> BuildBusinessDateMapAsync(IEnumerable<(string ReferenceType, Guid ReferenceId)> refs)
        {
            var map = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            var grouped = refs
                .Where(x => !string.IsNullOrWhiteSpace(x.ReferenceType) && x.ReferenceId != Guid.Empty)
                .GroupBy(x => x.ReferenceType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ReferenceId).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

            void AddValues(IEnumerable<(string ReferenceType, Guid Id, DateOnly Date)> values)
            {
                foreach (var v in values)
                {
                    map[BuildBusinessDateKey(v.ReferenceType, v.Id)] = v.Date;
                }
            }

            var salesTypes = new[] { "SalesInvoice", "SalesReturn", "SalesInvoiceEdit", "SalesInvoiceCancel" };
            var salesIds = grouped
                .Where(g => salesTypes.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                .SelectMany(g => g.Value)
                .Distinct()
                .ToList();
            if (salesIds.Count > 0)
            {
                var salesMap = await _context.Set<SalesInvoice>()
                    .AsNoTracking()
                    .Where(i => salesIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.InvoiceDate })
                    .ToListAsync();

                AddValues(salesMap.Select(i => ("SalesInvoice", i.Id, i.InvoiceDate)));
                AddValues(salesMap.Select(i => ("SalesReturn", i.Id, i.InvoiceDate)));
                AddValues(salesMap.Select(i => ("SalesInvoiceEdit", i.Id, i.InvoiceDate)));
                AddValues(salesMap.Select(i => ("SalesInvoiceCancel", i.Id, i.InvoiceDate)));
            }

            var purchaseTypes = new[] { "PurchaseInvoice", "PurchaseInvoiceEdit", "PurchaseInvoiceCancel" };
            var purchaseIds = grouped
                .Where(g => purchaseTypes.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                .SelectMany(g => g.Value)
                .Distinct()
                .ToList();
            if (purchaseIds.Count > 0)
            {
                var purchaseMap = await _context.Set<PurchaseInvoice>()
                    .AsNoTracking()
                    .Where(i => purchaseIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.InvoiceDate })
                    .ToListAsync();

                AddValues(purchaseMap.Select(i => ("PurchaseInvoice", i.Id, i.InvoiceDate)));
                AddValues(purchaseMap.Select(i => ("PurchaseInvoiceEdit", i.Id, i.InvoiceDate)));
                AddValues(purchaseMap.Select(i => ("PurchaseInvoiceCancel", i.Id, i.InvoiceDate)));
            }

            return map;
        }

        private async Task LoadLookups(Guid? categoryId)
        {
            var categories = await _categories.Repository.GetAll().OrderBy(c => c.Name).ToListAsync();

            ViewData["Categories"] = new SelectList(categories, "Id", "Name", categoryId);
        }
    }
}
