using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Financial;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "AdminOrProgPolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class FinancialReportsController : Controller
    {
        private readonly AppDbContext _context;

        public FinancialReportsController(AppDbContext context)
        {
            _context = context;
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
                            && l.Qty > 0)
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
                var itemPurchases = purchases
                    .Where(x => x.ItemId == itemId)
                    .OrderBy(x => x.InvoiceDate)
                    .ThenBy(x => x.Created)
                    .ToList();

                var itemSales = sales
                    .Where(x => x.ItemId == itemId)
                    .OrderBy(x => x.InvoiceDate)
                    .ThenBy(x => x.Created)
                    .ToList();

                var itemName = itemSales.Select(x => x.ItemName).FirstOrDefault()
                               ?? itemPurchases.Select(x => x.ItemName).FirstOrDefault()
                               ?? "Unknown Item";

                var layers = new Queue<FifoLayer>();

                decimal soldQtyInPeriod = 0m;
                decimal revenueDinarInPeriod = 0m;
                decimal cogsDinarInPeriod = 0m;

                var purchaseIndex = 0;
                var saleIndex = 0;

                while (purchaseIndex < itemPurchases.Count || saleIndex < itemSales.Count)
                {
                    var hasPurchase = purchaseIndex < itemPurchases.Count;
                    var hasSale = saleIndex < itemSales.Count;

                    var nextPurchaseDate = hasPurchase ? itemPurchases[purchaseIndex].InvoiceDate : DateOnly.MaxValue;
                    var nextSaleDate = hasSale ? itemSales[saleIndex].InvoiceDate : DateOnly.MaxValue;

                    var takePurchase = hasPurchase && (!hasSale || nextPurchaseDate < nextSaleDate ||
                        (nextPurchaseDate == nextSaleDate && itemPurchases[purchaseIndex].Created <= itemSales[saleIndex].Created));

                    if (takePurchase)
                    {
                        var p = itemPurchases[purchaseIndex++];
                        layers.Enqueue(new FifoLayer
                        {
                            RemainingQty = p.Qty,
                            UnitCostDinar = p.UnitCostDinar
                        });
                    }
                    else
                    {
                        var s = itemSales[saleIndex++];
                        var inPeriod = s.InvoiceDate >= fromDate && s.InvoiceDate <= toDate;

                        var qtyToConsume = s.Qty;
                        decimal eventCogsDinar = 0m;

                        while (qtyToConsume > 0 && layers.Count > 0)
                        {
                            var layer = layers.Peek();
                            var consumed = Math.Min(qtyToConsume, layer.RemainingQty);
                            eventCogsDinar += consumed * layer.UnitCostDinar;

                            layer.RemainingQty -= consumed;
                            qtyToConsume -= consumed;

                            if (layer.RemainingQty <= 0)
                                layers.Dequeue();
                        }

                        if (inPeriod)
                        {
                            soldQtyInPeriod += s.Qty;
                            revenueDinarInPeriod += s.RevenueDinar;
                            cogsDinarInPeriod += eventCogsDinar;
                        }
                    }
                }

                var remainingQty = layers.Sum(x => x.RemainingQty);
                var remainingValueDinar = layers.Sum(x => x.RemainingQty * x.UnitCostDinar);

                var hasRelevantData = soldQtyInPeriod > 0 || remainingQty > 0;
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

        private static decimal Round2(decimal value)
            => Math.Round(value, 2, MidpointRounding.ToEven);

        private sealed class FifoLayer
        {
            public decimal RemainingQty { get; set; }
            public decimal UnitCostDinar { get; set; }
        }
    }
}