using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Inventory;

namespace NewsApp2.Models.Services
{
    public class InventoryService
    {
        private readonly AppDbContext _context;

        public InventoryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(List<ItemCardRowVM> Rows, decimal OpeningQty, decimal ClosingQty, decimal ClosingValue)> GetItemCardAsync(Guid itemId, DateTime? from, DateTime? to)
        {
            var fromDate = from.HasValue ? DateOnly.FromDateTime(from.Value) : (DateOnly?)null;
            var toDate = to.HasValue ? DateOnly.FromDateTime(to.Value) : (DateOnly?)null;

            // Query ledger entries for this item once, then sort/filter in memory by business date.
            var ledgerQuery = _context.Set<InvStockLedger>()
                .AsNoTracking()
                .Where(l => l.ItemId == itemId);

            var businessDates = await BuildBusinessDateMapAsync(ledgerQuery);
            var ledgerRows = await ledgerQuery
                .Select(l => new { l.Id, l.Created, l.ReferenceType, l.ReferenceId, l.QuantityChange, l.UnitCostLyd, l.BalanceAfter, l.Note })
                .ToListAsync();

            var sortedRows = ledgerRows
                .Select(l => new
                {
                    Row = l,
                    BusinessDate = ResolveBusinessDate(l.ReferenceType, l.ReferenceId, l.Created, businessDates)
                })
                .OrderBy(x => x.BusinessDate)
                .ThenBy(x => x.Row.Created)
                .ThenBy(x => x.Row.Id)
                .ToList();

            var filteredRows = sortedRows
                .Where(x => !fromDate.HasValue || x.BusinessDate >= fromDate.Value)
                .Where(x => !toDate.HasValue || x.BusinessDate <= toDate.Value)
                .ToList();

            var openingRows = sortedRows
                .Where(x => !fromDate.HasValue || x.BusinessDate < fromDate.Value)
                .Select(x => x.Row)
                .ToList();

            var openingQty = openingRows.Sum(r => r.QuantityChange);
            var openingValue = openingRows.Sum(r => r.QuantityChange * r.UnitCostLyd);

            var salesRefs = filteredRows
                .Select(x => x.Row)
                .Where(r => !string.IsNullOrWhiteSpace(r.ReferenceType) && r.ReferenceType.Contains("Sales", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.ReferenceId)
                .Distinct()
                .ToList();
            var purchaseRefs = filteredRows
                .Select(x => x.Row)
                .Where(r => !string.IsNullOrWhiteSpace(r.ReferenceType) && r.ReferenceType.Contains("Purchase", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.ReferenceId)
                .Distinct()
                .ToList();

            var salesNumbers = salesRefs.Count > 0
                ? await _context.Set<SalesInvoice>().AsNoTracking()
                    .Where(i => salesRefs.Contains(i.Id))
                    .Select(i => new { i.Id, i.Number })
                    .ToDictionaryAsync(x => x.Id, x => x.Number)
                : new Dictionary<Guid, string>();

            var purchaseNumbers = purchaseRefs.Count > 0
                ? await _context.Set<PurchaseInvoice>().AsNoTracking()
                    .Where(i => purchaseRefs.Contains(i.Id))
                    .Select(i => new { i.Id, i.Number })
                    .ToDictionaryAsync(x => x.Id, x => x.Number)
                : new Dictionary<Guid, string>();

            var rows = new List<ItemCardRowVM>();

            decimal runningQty = openingQty;
            decimal runningValue = openingValue;

            foreach (var entry in filteredRows)
            {
                var r = entry.Row;
                var qtyIn = r.QuantityChange > 0 ? r.QuantityChange : 0m;
                var qtyOut = r.QuantityChange < 0 ? Math.Abs(r.QuantityChange) : 0m;
                var unitCost = r.UnitCostLyd;
                var lineValue = (qtyIn - qtyOut) * unitCost;

                runningQty += qtyIn - qtyOut;
                runningValue += lineValue;
                var runningAvg = runningQty == 0 ? 0m : Math.Round(runningValue / runningQty, 2, MidpointRounding.ToEven);

                rows.Add(new ItemCardRowVM
                {
                    Id = r.Id,
                    Date = entry.BusinessDate.ToDateTime(TimeOnly.MinValue),
                    TransactionType = MapReferenceType(r.ReferenceType),
                    ReferenceType = r.ReferenceType ?? string.Empty,
                    ReferenceId = r.ReferenceId,
                    Reference = BuildReferenceLabel(r.ReferenceType, r.ReferenceId, salesNumbers, purchaseNumbers),
                    QtyIn = qtyIn,
                    QtyOut = qtyOut,
                    UnitCost = unitCost,
                    LineValue = Math.Round(lineValue, 2, MidpointRounding.ToEven),
                    RunningQty = runningQty,
                    RunningAvgCost = runningAvg,
                    RunningValue = Math.Round(runningValue, 2, MidpointRounding.ToEven)
                });
            }

            // closing based on final running variables
            var closingQty = runningQty;
            var closingValue = Math.Round(runningValue, 2, MidpointRounding.ToEven);

            return (rows, openingQty, closingQty, closingValue);
        }

        private static string MapReferenceType(string? refType)
        {
            if (string.IsNullOrWhiteSpace(refType)) return "Adjustment";
            if (refType.Contains("Purchase", StringComparison.OrdinalIgnoreCase)) return "Purchase";
            if (refType.Contains("SalesReturn", StringComparison.OrdinalIgnoreCase)) return "Sales Return";
            if (refType.Contains("Sales", StringComparison.OrdinalIgnoreCase)) return "Sale";
            if (refType.Contains("Adjustment", StringComparison.OrdinalIgnoreCase)) return "Adjustment";
            return refType;
        }

        private async Task<IReadOnlyDictionary<string, DateOnly>> BuildBusinessDateMapAsync(IQueryable<InvStockLedger> ledgerQuery)
        {
            var refs = await ledgerQuery
                .Select(l => new { l.ReferenceType, l.ReferenceId })
                .Distinct()
                .ToListAsync();

            var map = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);

            var salesIds = refs
                .Where(x => !string.IsNullOrWhiteSpace(x.ReferenceType) && x.ReferenceType.Contains("Sales", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.ReferenceId)
                .Distinct()
                .ToList();
            if (salesIds.Count > 0)
            {
                var salesDates = await _context.Set<SalesInvoice>()
                    .AsNoTracking()
                    .Where(i => salesIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.InvoiceDate })
                    .ToListAsync();

                foreach (var row in salesDates)
                {
                    map[$"SalesInvoice:{row.Id}"] = row.InvoiceDate;
                    map[$"SalesReturn:{row.Id}"] = row.InvoiceDate;
                    map[$"SalesInvoiceEdit:{row.Id}"] = row.InvoiceDate;
                    map[$"SalesInvoiceCancel:{row.Id}"] = row.InvoiceDate;
                }
            }

            var purchaseIds = refs
                .Where(x => !string.IsNullOrWhiteSpace(x.ReferenceType) && x.ReferenceType.Contains("Purchase", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.ReferenceId)
                .Distinct()
                .ToList();
            if (purchaseIds.Count > 0)
            {
                var purchaseDates = await _context.Set<PurchaseInvoice>()
                    .AsNoTracking()
                    .Where(i => purchaseIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.InvoiceDate })
                    .ToListAsync();

                foreach (var row in purchaseDates)
                {
                    map[$"PurchaseInvoice:{row.Id}"] = row.InvoiceDate;
                    map[$"PurchaseInvoiceEdit:{row.Id}"] = row.InvoiceDate;
                    map[$"PurchaseInvoiceCancel:{row.Id}"] = row.InvoiceDate;
                }
            }

            return map;
        }

        private static DateOnly ResolveBusinessDate(string? referenceType, Guid referenceId, DateTime createdUtc, IReadOnlyDictionary<string, DateOnly> businessDates)
        {
            var key = $"{referenceType}:{referenceId}";
            if (!string.IsNullOrWhiteSpace(referenceType) && businessDates.TryGetValue(key, out var date))
                return date;

            return DateOnly.FromDateTime(createdUtc);
        }

        private static string BuildReferenceLabel(
            string? referenceType,
            Guid referenceId,
            IReadOnlyDictionary<Guid, string> salesNumbers,
            IReadOnlyDictionary<Guid, string> purchaseNumbers)
        {
            if (!string.IsNullOrWhiteSpace(referenceType))
            {
                if (referenceType.Contains("Purchase", StringComparison.OrdinalIgnoreCase)
                    && purchaseNumbers.TryGetValue(referenceId, out var purchaseNumber))
                {
                    return purchaseNumber;
                }

                if (referenceType.Contains("Sales", StringComparison.OrdinalIgnoreCase)
                    && salesNumbers.TryGetValue(referenceId, out var salesNumber))
                {
                    return salesNumber;
                }
            }

            return referenceId.ToString();
        }
    }
}
