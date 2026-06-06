using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Financial;
using NewsApp2.Models.Services;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "AdminOrProgPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class FinancialReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SalesService _salesService;

        public FinancialReportsController(AppDbContext context, SalesService salesService)
        {
            _context = context;
            _salesService = salesService;
        }

        [HttpGet]
        public async Task<IActionResult> ProfitLoss(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);

            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var purchases = await _context.Set<PurchaseLine>()
                .AsNoTracking()
                .Where(l => l.PurchaseInvoice != null
                            && l.PurchaseInvoice.Status == "Posted"
                            && l.PurchaseInvoice.InvoiceDate <= toDate
                            && l.Qty > 0)
                .Select(l => new
                {
                    l.ItemId,
                    ItemName = l.Item != null ? l.Item.Name : "Unknown Item",
                    InvoiceDate = l.PurchaseInvoice!.InvoiceDate,
                    Created = l.Created,
                    Qty = l.Qty,
                    UnitCostDinar = l.Qty > 0 ? l.LineTotalDinar / l.Qty : 0m
                })
                .ToListAsync();

            var sales = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => l.SalesInvoice != null
                            && l.SalesInvoice.Status == "Posted"
                            && l.SalesInvoice.InvoiceDate <= toDate
                            && l.Qty != 0)
                .Select(l => new
                {
                    l.ItemId,
                    ItemName = l.Item != null ? l.Item.Name : "Unknown Item",
                    InvoiceDate = l.SalesInvoice!.InvoiceDate,
                    Created = l.Created,
                    Qty = l.Qty,
                    RevenueDinar = l.LineTotalDinar
                })
                .ToListAsync();

            var itemIds = purchases.Select(x => x.ItemId)
                .Concat(sales.Select(x => x.ItemId))
                .Distinct()
                .ToList();

            var rows = new List<ProfitLossItemRowVM>();

            foreach (var itemId in itemIds)
            {
                var itemPurchases = purchases.Where(x => x.ItemId == itemId).ToList();
                var itemSales = sales.Where(x => x.ItemId == itemId).ToList();

                var itemName = itemSales.Select(x => x.ItemName).FirstOrDefault()
                               ?? itemPurchases.Select(x => x.ItemName).FirstOrDefault()
                               ?? "Unknown Item";

                var purchasedQtyToDate = itemPurchases.Sum(x => x.Qty);
                var purchasedValueToDate = itemPurchases.Sum(x => x.Qty * x.UnitCostDinar);
                var avgUnitCost = purchasedQtyToDate > 0 ? purchasedValueToDate / purchasedQtyToDate : 0m;

                var soldQtyToDate = itemSales.Sum(x => x.Qty);
                var soldQtyInPeriod = itemSales
                    .Where(x => x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate)
                    .Sum(x => x.Qty);
                var revenueDinarInPeriod = itemSales
                    .Where(x => x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate)
                    .Sum(x => x.RevenueDinar);
                var cogsDinarInPeriod = soldQtyInPeriod * avgUnitCost;

                var remainingQty = Math.Max(0m, purchasedQtyToDate - soldQtyToDate);
                var remainingValueDinar = remainingQty * avgUnitCost;

                var hasRelevantData = soldQtyInPeriod != 0 || revenueDinarInPeriod != 0 || remainingQty > 0;
                if (!hasRelevantData)
                    continue;

                rows.Add(new ProfitLossItemRowVM
                {
                    ItemId = itemId,
                    ItemName = itemName,
                    SoldQty = Round2(soldQtyInPeriod),
                    RevenueEur = Round2(revenueDinarInPeriod),
                    RevenueDinar = Round2(revenueDinarInPeriod),
                    CogsEur = Round2(cogsDinarInPeriod),
                    CogsDinar = Round2(cogsDinarInPeriod),
                    GrossProfitEur = Round2(revenueDinarInPeriod - cogsDinarInPeriod),
                    GrossProfitDinar = Round2(revenueDinarInPeriod - cogsDinarInPeriod),
                    RemainingQty = Round2(remainingQty),
                    RemainingValueEur = Round2(remainingValueDinar),
                    RemainingValueDinar = Round2(remainingValueDinar)
                });
            }

            rows = rows.OrderByDescending(x => x.GrossProfitDinar).ToList();

            var revenueDinar = Round2(rows.Sum(x => x.RevenueDinar));
            var cogsDinar = Round2(rows.Sum(x => x.CogsDinar));
            var grossProfitDinar = Round2(revenueDinar - cogsDinar);
            var marginPercent = revenueDinar > 0 ? Round2((grossProfitDinar / revenueDinar) * 100m) : 0m;

            var remainingStockQty = Round2(rows.Sum(x => x.RemainingQty));
            var remainingStockValueDinar = Round2(rows.Sum(x => x.RemainingValueDinar));

            var postedPurchasesInPeriodDinar = Round2(await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted" && i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
                .SumAsync(i => (decimal?)i.TotalDinar) ?? 0m);

            var periodSalesInvoices = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.Status == "Posted" && i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
                .ToListAsync();

            decimal cash = 0, card = 0, transfer = 0, credit = 0, returns = 0;
            foreach (var inv in periodSalesInvoices)
            {
                if ((inv.Note != null && inv.Note.Contains("[POS-RETURN]")) || inv.TotalDinar < 0)
                {
                    returns += Math.Abs(inv.TotalDinar);
                }
                else
                {
                    var m = (inv.PaymentMethod ?? "").Trim().ToLower();
                    if (m == "card" || m == "بطاقة") card += inv.TotalDinar;
                    else if (m == "transfer" || m == "تحويل") transfer += inv.TotalDinar;
                    else if (m == "credit" || m == "آجل") credit += inv.TotalDinar;
                    else cash += inv.TotalDinar;
                }
            }

            var expenseRows = await _context.Set<ExpenseEntry>()
                .AsNoTracking()
                .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
                .Select(e => new { e.ExpenseKind, e.Amount })
                .ToListAsync();

            var generalExpensesDinar = Round2(expenseRows
                .Where(x => string.Equals(x.ExpenseKind, "General", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount));

            var salariesDinar = Round2(expenseRows
                .Where(x => string.Equals(x.ExpenseKind, "Salary", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount));

            var advancesDinar = Round2(expenseRows
                .Where(x => string.Equals(x.ExpenseKind, "Advance", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Amount));

            var totalOperatingOutflowsDinar = Round2(generalExpensesDinar + salariesDinar + advancesDinar);
            var netProfitAfterExpensesDinar = Round2(grossProfitDinar - totalOperatingOutflowsDinar);

            var vm = new ProfitLossReportVM
            {
                From = fromDate,
                To = toDate,
                SecondaryCurrencyCode = "LYD",
                RevenueEur = revenueDinar,
                RevenueDinar = revenueDinar,
                CogsEur = cogsDinar,
                CogsDinar = cogsDinar,
                GrossProfitEur = grossProfitDinar,
                GrossProfitDinar = grossProfitDinar,
                MarginPercent = marginPercent,
                RemainingStockQty = remainingStockQty,
                RemainingStockValueEur = remainingStockValueDinar,
                RemainingStockValueDinar = remainingStockValueDinar,
                PostedPurchasesInPeriodEur = postedPurchasesInPeriodDinar,
                PostedPurchasesInPeriodDinar = postedPurchasesInPeriodDinar,
                CashSalesDinar = Round2(cash),
                CardSalesDinar = Round2(card),
                TransferSalesDinar = Round2(transfer),
                CreditSalesDinar = Round2(credit),
                ReturnsDinar = Round2(returns),
                GeneralExpensesDinar = generalExpensesDinar,
                SalariesDinar = salariesDinar,
                AdvancesDinar = advancesDinar,
                TotalOperatingOutflowsDinar = totalOperatingOutflowsDinar,
                NetProfitAfterExpensesDinar = netProfitAfterExpensesDinar,
                Rows = rows
            };

            ViewBag.SecondaryCurrencyCode = "LYD";
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Journal(DateOnly? from, DateOnly? to, string? accountCode, string? sourceType, string? search)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);

            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var query = _context.Set<FinJournalEntry>()
                .AsNoTracking()
                .Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate);

            if (!string.IsNullOrWhiteSpace(accountCode))
            {
                var code = accountCode.Trim();
                query = query.Where(e => e.AccountCode == code);
            }

            if (!string.IsNullOrWhiteSpace(sourceType))
            {
                var source = sourceType.Trim();
                query = query.Where(e => e.SourceType == source);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    (e.DocumentNo != null && e.DocumentNo.Contains(term))
                    || (e.AccountName != null && e.AccountName.Contains(term))
                    || (e.Note != null && e.Note.Contains(term))
                    || (e.CreatedByUserName != null && e.CreatedByUserName.Contains(term)));
            }

            var rows = await query
                .OrderBy(e => e.EntryDate)
                .ThenBy(e => e.SourceType)
                .ThenBy(e => e.DocumentNo)
                .ThenBy(e => e.Created)
                .Select(e => new JournalEntryRowVM
                {
                    EntryDate = e.EntryDate,
                    SourceType = e.SourceType,
                    DocumentNo = e.DocumentNo,
                    AccountCode = e.AccountCode,
                    AccountName = e.AccountName,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Note = e.Note,
                    CreatedByUserName = e.CreatedByUserName
                })
                .ToListAsync();

            var accountCodes = await _context.Set<FinJournalEntry>()
                .AsNoTracking()
                .Select(e => e.AccountCode)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var sourceTypes = await _context.Set<FinJournalEntry>()
                .AsNoTracking()
                .Select(e => e.SourceType)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var vm = new JournalReportVM
            {
                From = fromDate,
                To = toDate,
                AccountCode = string.IsNullOrWhiteSpace(accountCode) ? null : accountCode.Trim(),
                SourceType = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim(),
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                TotalDebit = Round2(rows.Sum(r => r.Debit)),
                TotalCredit = Round2(rows.Sum(r => r.Credit)),
                Rows = rows,
                AccountCodes = accountCodes,
                SourceTypes = sourceTypes
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportJournalExcel(DateOnly? from, DateOnly? to, string? accountCode, string? sourceType, string? search)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);

            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var query = _context.Set<FinJournalEntry>()
                .AsNoTracking()
                .Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate);

            if (!string.IsNullOrWhiteSpace(accountCode))
            {
                var code = accountCode.Trim();
                query = query.Where(e => e.AccountCode == code);
            }

            if (!string.IsNullOrWhiteSpace(sourceType))
            {
                var source = sourceType.Trim();
                query = query.Where(e => e.SourceType == source);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    (e.DocumentNo != null && e.DocumentNo.Contains(term))
                    || (e.AccountName != null && e.AccountName.Contains(term))
                    || (e.Note != null && e.Note.Contains(term))
                    || (e.CreatedByUserName != null && e.CreatedByUserName.Contains(term)));
            }

            var rows = await query
                .OrderBy(e => e.EntryDate)
                .ThenBy(e => e.SourceType)
                .ThenBy(e => e.DocumentNo)
                .ThenBy(e => e.Created)
                .Select(e => new JournalEntryRowVM
                {
                    EntryDate = e.EntryDate,
                    SourceType = e.SourceType,
                    DocumentNo = e.DocumentNo,
                    AccountCode = e.AccountCode,
                    AccountName = e.AccountName,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Note = e.Note,
                    CreatedByUserName = e.CreatedByUserName
                })
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Journal");

            sheet.Cell(1, 1).Value = "التاريخ";
            sheet.Cell(1, 2).Value = "المصدر";
            sheet.Cell(1, 3).Value = "المستند";
            sheet.Cell(1, 4).Value = "رمز الحساب";
            sheet.Cell(1, 5).Value = "اسم الحساب";
            sheet.Cell(1, 6).Value = "مدين";
            sheet.Cell(1, 7).Value = "دائن";
            sheet.Cell(1, 8).Value = "البيان";
            sheet.Cell(1, 9).Value = "المستخدم";
            sheet.Range(1, 1, 1, 9).Style.Font.Bold = true;

            var rowIndex = 2;
            foreach (var row in rows)
            {
                sheet.Cell(rowIndex, 1).Value = row.EntryDate.ToString("yyyy-MM-dd");
                sheet.Cell(rowIndex, 2).Value = row.SourceType;
                sheet.Cell(rowIndex, 3).Value = row.DocumentNo ?? string.Empty;
                sheet.Cell(rowIndex, 4).Value = row.AccountCode;
                sheet.Cell(rowIndex, 5).Value = row.AccountName;
                sheet.Cell(rowIndex, 6).Value = row.Debit;
                sheet.Cell(rowIndex, 7).Value = row.Credit;
                sheet.Cell(rowIndex, 8).Value = row.Note ?? string.Empty;
                sheet.Cell(rowIndex, 9).Value = row.CreatedByUserName ?? string.Empty;
                rowIndex++;
            }

            sheet.Cell(rowIndex, 5).Value = "الإجمالي";
            sheet.Cell(rowIndex, 6).Value = rows.Sum(r => r.Debit);
            sheet.Cell(rowIndex, 7).Value = rows.Sum(r => r.Credit);
            sheet.Range(rowIndex, 5, rowIndex, 7).Style.Font.Bold = true;

            sheet.Columns().AdjustToContents();

            await using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"journal_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> TrialBalance(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);

            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var rows = await _context.Set<FinJournalEntry>()
                .AsNoTracking()
                .Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate)
                .GroupBy(e => new { e.AccountCode, e.AccountName })
                .Select(g => new TrialBalanceRowVM
                {
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.AccountName,
                    Debit = Round2(g.Sum(x => x.Debit)),
                    Credit = Round2(g.Sum(x => x.Credit))
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                var diff = Round2(row.Debit - row.Credit);
                if (diff >= 0)
                {
                    row.NetDebit = diff;
                    row.NetCredit = 0m;
                }
                else
                {
                    row.NetDebit = 0m;
                    row.NetCredit = Math.Abs(diff);
                }
            }

            rows = rows
                .OrderBy(r => r.AccountCode)
                .ThenBy(r => r.AccountName)
                .ToList();

            var vm = new TrialBalanceReportVM
            {
                From = fromDate,
                To = toDate,
                TotalDebit = Round2(rows.Sum(r => r.Debit)),
                TotalCredit = Round2(rows.Sum(r => r.Credit)),
                TotalNetDebit = Round2(rows.Sum(r => r.NetDebit)),
                TotalNetCredit = Round2(rows.Sum(r => r.NetCredit)),
                Rows = rows
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTrialBalanceExcel(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var toDate = to ?? today;
            var fromDate = from ?? toDate.AddDays(-30);

            if (fromDate > toDate)
            {
                var temp = fromDate;
                fromDate = toDate;
                toDate = temp;
            }

            var rows = await _context.Set<FinJournalEntry>()
                .AsNoTracking()
                .Where(e => e.EntryDate >= fromDate && e.EntryDate <= toDate)
                .GroupBy(e => new { e.AccountCode, e.AccountName })
                .Select(g => new TrialBalanceRowVM
                {
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.AccountName,
                    Debit = Round2(g.Sum(x => x.Debit)),
                    Credit = Round2(g.Sum(x => x.Credit))
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                var diff = Round2(row.Debit - row.Credit);
                if (diff >= 0)
                {
                    row.NetDebit = diff;
                    row.NetCredit = 0m;
                }
                else
                {
                    row.NetDebit = 0m;
                    row.NetCredit = Math.Abs(diff);
                }
            }

            rows = rows
                .OrderBy(r => r.AccountCode)
                .ThenBy(r => r.AccountName)
                .ToList();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("TrialBalance");

            sheet.Cell(1, 1).Value = "رمز الحساب";
            sheet.Cell(1, 2).Value = "اسم الحساب";
            sheet.Cell(1, 3).Value = "إجمالي مدين";
            sheet.Cell(1, 4).Value = "إجمالي دائن";
            sheet.Cell(1, 5).Value = "رصيد مدين";
            sheet.Cell(1, 6).Value = "رصيد دائن";
            sheet.Range(1, 1, 1, 6).Style.Font.Bold = true;

            var rowIndex = 2;
            foreach (var row in rows)
            {
                sheet.Cell(rowIndex, 1).Value = row.AccountCode;
                sheet.Cell(rowIndex, 2).Value = row.AccountName;
                sheet.Cell(rowIndex, 3).Value = row.Debit;
                sheet.Cell(rowIndex, 4).Value = row.Credit;
                sheet.Cell(rowIndex, 5).Value = row.NetDebit;
                sheet.Cell(rowIndex, 6).Value = row.NetCredit;
                rowIndex++;
            }

            sheet.Cell(rowIndex, 2).Value = "الإجمالي";
            sheet.Cell(rowIndex, 3).Value = rows.Sum(r => r.Debit);
            sheet.Cell(rowIndex, 4).Value = rows.Sum(r => r.Credit);
            sheet.Cell(rowIndex, 5).Value = rows.Sum(r => r.NetDebit);
            sheet.Cell(rowIndex, 6).Value = rows.Sum(r => r.NetCredit);
            sheet.Range(rowIndex, 2, rowIndex, 6).Style.Font.Bold = true;

            sheet.Columns().AdjustToContents();

            await using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"trial_balance_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public IActionResult BackfillCogs()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackfillCogs(string? confirm)
        {
            if (confirm != "نعم")
            {
                TempData["Error"] = "الرجاء كتابة 'نعم' للتأكيد.";
                return RedirectToAction(nameof(BackfillCogs));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity?.Name;

            var (processed, skipped) = await _salesService.BackfillMissingCogsEntriesAsync(userId, userName);

            TempData["Success"] = $"تمت المعالجة: {processed} فاتورة تم ترحيلها، {skipped} فاتورة تم تخطيها.";
            return RedirectToAction(nameof(TrialBalance));
        }

        private static decimal Round2(decimal value)
            => Math.Round(value, 2, MidpointRounding.ToEven);

    }
}