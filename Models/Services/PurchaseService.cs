using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    public class PurchaseService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PurchaseService> _logger;

        public PurchaseService(AppDbContext context, ILogger<PurchaseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Guid> CreateAsync(PurchaseInvoice invoice, IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur, decimal? SellPriceLyd)> lines)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("Purchase requires at least one line.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("Maximum allowed lines per invoice is 200.");

            if (!string.Equals(invoice.CurrencyCode, "EUR", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(invoice.CurrencyCode, "LYD", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only EUR or LYD is allowed.");

            if (string.Equals(invoice.CurrencyCode, "LYD", StringComparison.OrdinalIgnoreCase))
                invoice.EurToDinarRateSnapshot = 1m;
            else if (invoice.EurToDinarRateSnapshot <= 0)
                throw new InvalidOperationException("Rate must be greater than zero.");
            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = "Posted";

            _logger.LogInformation("Creating purchase invoice for user {User} with {LineCount} lines.", invoice.CreatedByUserName, lineList.Count);

            using var tx = await _context.Database.BeginTransactionAsync();

            _context.Set<PurchaseInvoice>().Add(invoice);
            await _context.SaveChangesAsync();

            decimal totalEur = 0;
            decimal totalDinar = 0;

            var itemIds = lineList.Select(l => l.ItemId).Distinct().ToList();
            var itemById = await _context.Set<Item>()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i);

            for (var lineIndex = 0; lineIndex < lineList.Count; lineIndex++)
            {
                var line = lineList[lineIndex];
                if (line.Qty <= 0)
                    throw new InvalidOperationException("Quantity must be greater than zero.");

                if (line.Qty != decimal.Truncate(line.Qty))
                    throw new InvalidOperationException("Quantity must be a whole number.");

                if (line.UnitPriceEur < 0)
                    throw new InvalidOperationException("Unit price cannot be negative.");

                if (!itemById.ContainsKey(line.ItemId))
                    throw new InvalidOperationException("Selected item was not found.");

                if (line.SellPriceLyd.HasValue && line.SellPriceLyd.Value < 0)
                    throw new InvalidOperationException("Sell price cannot be negative.");

                var lineTotalEur = RoundMoney(line.Qty * line.UnitPriceEur);
                var lineTotalDinar = RoundMoney(lineTotalEur * invoice.EurToDinarRateSnapshot);

                totalEur += lineTotalEur;
                totalDinar += lineTotalDinar;

                var purchaseLine = new PurchaseLine
                {
                    PurchaseInvoiceId = invoice.Id,
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    LineOrder = lineIndex,
                    UnitPriceEur = line.UnitPriceEur,
                    LineTotalEur = lineTotalEur,
                    LineTotalDinar = lineTotalDinar,
                    CurrencyCode = invoice.CurrencyCode,
                    ExchangeRateSnapshot = invoice.EurToDinarRateSnapshot
                };
                _context.Set<PurchaseLine>().Add(purchaseLine);

                var stock = await _context.Set<InvStockBalance>()
                    .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                if (stock == null)
                {
                    stock = new InvStockBalance
                    {
                        ItemId = line.ItemId,
                        QuantityOnHand = line.Qty
                    };
                    _context.Set<InvStockBalance>().Add(stock);
                }
                else
                {
                    stock.QuantityOnHand += line.Qty;
                }

                var ledger = new InvStockLedger
                {
                    ItemId = line.ItemId,
                    MovementType = "In",
                    ReferenceType = "PurchaseInvoice",
                    ReferenceId = invoice.Id,
                    QuantityChange = line.Qty,
                    BalanceAfter = stock.QuantityOnHand,
                    Note = invoice.Note
                };
                _context.Set<InvStockLedger>().Add(ledger);
            }

            var salePriceUpdates = lineList
                .Where(l => l.SellPriceLyd.HasValue)
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Last().SellPriceLyd!.Value);

            foreach (var update in salePriceUpdates)
            {
                if (!itemById.TryGetValue(update.Key, out var item))
                    continue;

                item.DefaultSalePriceLyd = RoundMoney(update.Value);
            }

            invoice.TotalEur = RoundMoney(totalEur);
            invoice.TotalDinar = RoundMoney(totalDinar);

            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Create",
                EntityType = "PurchaseInvoice",
                EntityId = invoice.Id,
                EntityNumber = invoice.Number,
                Description = $"Lines: {lineList.Count}, TotalEUR: {invoice.TotalEur:0.00}, TotalLYD: {invoice.TotalDinar:0.00}, Rate: {invoice.EurToDinarRateSnapshot:0.000000}",
                CreatedByUserId = invoice.CreatedByUserId,
                CreatedByUserName = invoice.CreatedByUserName
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            _logger.LogInformation("Purchase invoice {Number} created successfully.", invoice.Number);
            return invoice.Id;
        }

        public async Task CancelAsync(Guid invoiceId, string? cancelledBy)
        {
            var invoice = await _context.Set<PurchaseInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Invoice not found.");

            if (!string.Equals(invoice.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only posted invoices can be cancelled.");

            var lines = invoice.Lines?.ToList() ?? new List<PurchaseLine>();
            if (!lines.Any())
                throw new InvalidOperationException("Invoice has no lines.");

            using var tx = await _context.Database.BeginTransactionAsync();

            _logger.LogInformation("Cancelling purchase invoice {Number} ({Id}) by {User}.", invoice.Number, invoice.Id, cancelledBy);

            var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
            var itemNames = await _context.Set<Item>()
                .AsNoTracking()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Name);

            foreach (var line in lines)
            {
                var stock = await _context.Set<InvStockBalance>()
                    .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                if (stock == null || stock.QuantityOnHand < line.Qty)
                {
                    var available = stock?.QuantityOnHand ?? 0m;
                    var itemName = itemNames.TryGetValue(line.ItemId, out var name) ? name : "item";
                    throw new InvalidOperationException(
                        $"Cannot cancel purchase for {itemName}. Required {FormatQuantity(line.Qty)}, available {FormatQuantity(available)}.");
                }

                stock.QuantityOnHand -= line.Qty;

                _context.Set<InvStockLedger>().Add(new InvStockLedger
                {
                    ItemId = line.ItemId,
                    MovementType = "Out",
                    ReferenceType = "PurchaseInvoiceCancel",
                    ReferenceId = invoice.Id,
                    QuantityChange = -line.Qty,
                    BalanceAfter = stock.QuantityOnHand,
                    Note = $"Cancelled by {cancelledBy ?? "unknown"}"
                });
            }

            invoice.Status = "Cancelled";

            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Cancel",
                EntityType = "PurchaseInvoice",
                EntityId = invoice.Id,
                EntityNumber = invoice.Number,
                Description = $"Cancelled purchase invoice. Lines: {lines.Count}",
                CreatedByUserName = cancelledBy
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            _logger.LogInformation("Purchase invoice {Number} cancelled.", invoice.Number);
        }

        public async Task UpdatePostedAsync(
            Guid invoiceId,
            DateOnly invoiceDate,
            decimal rate,
            string? note,
            IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur, decimal? SellPriceLyd)> lines,
            string? editedBy)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("Purchase requires at least one line.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("Maximum allowed lines per invoice is 200.");

            if (rate <= 0)
                throw new InvalidOperationException("Rate must be greater than zero.");

            foreach (var line in lineList)
            {
                if (line.Qty <= 0)
                    throw new InvalidOperationException("Quantity must be greater than zero.");

                if (line.Qty != decimal.Truncate(line.Qty))
                    throw new InvalidOperationException("Quantity must be a whole number.");

                if (line.UnitPriceEur < 0)
                    throw new InvalidOperationException("Unit price cannot be negative.");

                if (line.SellPriceLyd.HasValue && line.SellPriceLyd.Value < 0)
                    throw new InvalidOperationException("Sell price cannot be negative.");
            }

            var invoice = await _context.Set<PurchaseInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Invoice not found.");

            if (!string.Equals(invoice.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only posted purchase invoices can be edited.");

            var itemIds = lineList.Select(l => l.ItemId)
                .Concat((invoice.Lines ?? new List<PurchaseLine>()).Select(l => l.ItemId))
                .Distinct()
                .ToList();

            var itemNames = await _context.Set<Item>()
                .AsNoTracking()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.Name);

            var itemById = await _context.Set<Item>()
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i);

            foreach (var id in lineList.Select(l => l.ItemId).Distinct())
            {
                if (!itemNames.ContainsKey(id))
                    throw new InvalidOperationException("Selected item was not found.");
            }

            var oldQtyByItem = (invoice.Lines ?? new List<PurchaseLine>())
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            var newQtyByItem = lineList
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            var stockByItem = await _context.Set<InvStockBalance>()
                .Where(s => itemIds.Contains(s.ItemId))
                .ToDictionaryAsync(s => s.ItemId, s => s);

            var unionItemIds = oldQtyByItem.Keys.Union(newQtyByItem.Keys).ToList();

            foreach (var itemId in unionItemIds)
            {
                var oldQty = oldQtyByItem.TryGetValue(itemId, out var oldVal) ? oldVal : 0m;
                var newQty = newQtyByItem.TryGetValue(itemId, out var newVal) ? newVal : 0m;
                var delta = newQty - oldQty;

                if (!stockByItem.TryGetValue(itemId, out var stock))
                {
                    stock = new InvStockBalance
                    {
                        ItemId = itemId,
                        QuantityOnHand = 0m
                    };
                    _context.Set<InvStockBalance>().Add(stock);
                    stockByItem[itemId] = stock;
                }

                var resultQty = stock.QuantityOnHand + delta;
                if (resultQty < 0)
                {
                    var itemName = itemNames.TryGetValue(itemId, out var name) ? name : "item";
                    throw new InvalidOperationException(
                        $"Cannot reduce purchased quantity for {itemName}. Available {FormatQuantity(stock.QuantityOnHand)}, requested change {FormatQuantity(delta)}.");
                }
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            foreach (var itemId in unionItemIds)
            {
                var oldQty = oldQtyByItem.TryGetValue(itemId, out var oldVal) ? oldVal : 0m;
                var newQty = newQtyByItem.TryGetValue(itemId, out var newVal) ? newVal : 0m;
                var delta = newQty - oldQty;
                if (delta == 0)
                    continue;

                var stock = stockByItem[itemId];
                stock.QuantityOnHand += delta;

                _context.Set<InvStockLedger>().Add(new InvStockLedger
                {
                    ItemId = itemId,
                    MovementType = delta >= 0 ? "In" : "Out",
                    ReferenceType = "PurchaseInvoiceEdit",
                    ReferenceId = invoice.Id,
                    QuantityChange = delta,
                    BalanceAfter = stock.QuantityOnHand,
                    Note = $"Edited by {editedBy ?? "unknown"}"
                });
            }

            _context.Set<PurchaseLine>().RemoveRange(invoice.Lines ?? new List<PurchaseLine>());

            decimal totalEur = 0m;
            decimal totalDinar = 0m;

            for (var lineIndex = 0; lineIndex < lineList.Count; lineIndex++)
            {
                var line = lineList[lineIndex];
                var lineTotalEur = RoundMoney(line.Qty * line.UnitPriceEur);
                var lineTotalDinar = RoundMoney(lineTotalEur * rate);

                totalEur += lineTotalEur;
                totalDinar += lineTotalDinar;

                _context.Set<PurchaseLine>().Add(new PurchaseLine
                {
                    PurchaseInvoiceId = invoice.Id,
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    LineOrder = lineIndex,
                    UnitPriceEur = line.UnitPriceEur,
                    LineTotalEur = lineTotalEur,
                    LineTotalDinar = lineTotalDinar,
                    CurrencyCode = invoice.CurrencyCode,
                    ExchangeRateSnapshot = rate
                });
            }

            var salePriceUpdates = lineList
                .Where(l => l.SellPriceLyd.HasValue)
                .GroupBy(l => l.ItemId)
                .ToDictionary(g => g.Key, g => g.Last().SellPriceLyd!.Value);

            foreach (var update in salePriceUpdates)
            {
                if (!itemById.TryGetValue(update.Key, out var item))
                    continue;

                item.DefaultSalePriceLyd = RoundMoney(update.Value);
            }

            invoice.InvoiceDate = invoiceDate;
            invoice.CurrencyCode = string.Equals(invoice.CurrencyCode, "LYD", StringComparison.OrdinalIgnoreCase) ? "LYD" : "EUR";
            invoice.EurToDinarRateSnapshot = string.Equals(invoice.CurrencyCode, "LYD", StringComparison.OrdinalIgnoreCase) ? 1m : rate;
            invoice.Note = note;
            invoice.TotalEur = RoundMoney(totalEur);
            invoice.TotalDinar = RoundMoney(totalDinar);

            _context.Set<AuditLog>().Add(new AuditLog
            {
                Action = "Edit",
                EntityType = "PurchaseInvoice",
                EntityId = invoice.Id,
                EntityNumber = invoice.Number,
                Description = $"Edited purchase invoice. Lines: {lineList.Count}, TotalEUR: {invoice.TotalEur:0.00}",
                CreatedByUserName = editedBy
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            _logger.LogInformation("Purchase invoice {Number} edited by {User}.", invoice.Number, editedBy);
        }

        private async Task<string> GenerateNumberAsync(DateOnly invoiceDate)
        {
            var datePart = invoiceDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var prefix = $"PUR-{datePart}-";

            var lastNumber = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.Number.StartsWith(prefix))
                .OrderByDescending(i => i.Number)
                .Select(i => i.Number)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(lastNumber))
            {
                var parts = lastNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && int.TryParse(parts[^1], out var parsed))
                    next = parsed + 1;
            }

            return $"{prefix}{next:0000}";
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.ToEven);
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
