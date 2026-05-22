using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    public class PurchaseService
    {
        private const string JournalSourceType = "PurchaseInvoice";
        private const string AccountInventoryCode = "1301";
        private const string AccountInventoryName = "المخزون";
        private const string AccountCashCode = "1101";
        private const string AccountCashName = "الصندوق";
        private const string AccountBankCode = "1102";
        private const string AccountBankName = "البنك";
        private const string AccountSupplierCode = "2101";
        private const string AccountSupplierName = "ذمم الموردين";

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
                throw new InvalidOperationException("فاتورة الشراء تتطلب بنداً واحداً على الأقل.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("الحد الأقصى لبنود الفاتورة هو 200.");

            if (!string.Equals(invoice.CurrencyCode, "EUR", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(invoice.CurrencyCode, "LYD", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("يُسمح فقط باليورو أو الدينار الليبي.");

            if (string.Equals(invoice.CurrencyCode, "LYD", StringComparison.OrdinalIgnoreCase))
                invoice.EurToDinarRateSnapshot = 1m;
            else if (invoice.EurToDinarRateSnapshot <= 0)
                throw new InvalidOperationException("يجب أن يكون سعر الصرف أكبر من الصفر.");

            invoice.PaymentMethod = NormalizePaymentMethod(invoice.PaymentMethod);
            if (string.Equals(invoice.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
            {
                if (!invoice.SupplierId.HasValue || invoice.SupplierId == Guid.Empty)
                    throw new InvalidOperationException("حدد المورد عند الشراء الآجل.");

                if (!invoice.DueDate.HasValue)
                    throw new InvalidOperationException("حدد تاريخ الاستحقاق عند الشراء الآجل.");

                if (invoice.DueDate.Value < invoice.InvoiceDate)
                    throw new InvalidOperationException("تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ الفاتورة.");
            }
            else
            {
                invoice.DueDate = null;
            }

            if (invoice.SupplierId.HasValue && invoice.SupplierId != Guid.Empty)
            {
                var supplierExists = await _context.Set<Supplier>().AnyAsync(s => s.Id == invoice.SupplierId.Value);
                if (!supplierExists)
                    throw new InvalidOperationException("المورد المحدد غير موجود.");
            }

            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = "Posted";

            _logger.LogInformation("Creating purchase invoice for user {User} with {LineCount} lines.", invoice.CreatedByUserName, lineList.Count);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
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
                        throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

                    if (line.Qty != decimal.Truncate(line.Qty))
                        throw new InvalidOperationException("يجب أن تكون الكمية رقماً صحيحاً.");

                    if (line.UnitPriceEur < 0)
                        throw new InvalidOperationException("لا يمكن أن يكون سعر الوحدة سالباً.");

                    if (!itemById.ContainsKey(line.ItemId))
                        throw new InvalidOperationException("الصنف المحدد غير موجود.");

                    if (line.SellPriceLyd.HasValue && line.SellPriceLyd.Value < 0)
                        throw new InvalidOperationException("لا يمكن أن يكون سعر البيع سالباً.");

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
                    purchaseLine.UnitCostLyd = line.Qty > 0 ? RoundMoney(lineTotalDinar / line.Qty) : 0m;
                    _context.Set<PurchaseLine>().Add(purchaseLine);

                    var stock = await _context.Set<InvStockBalance>()
                        .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                    var incomingUnitCost = purchaseLine.UnitCostLyd;
                    if (stock == null)
                    {
                        stock = new InvStockBalance
                        {
                            ItemId = line.ItemId,
                            QuantityOnHand = line.Qty,
                            AverageCostLyd = incomingUnitCost
                        };
                        _context.Set<InvStockBalance>().Add(stock);
                    }
                    else
                    {
                        var oldQty = stock.QuantityOnHand;
                        var oldAvg = stock.AverageCostLyd;
                        var newQty = oldQty + line.Qty;
                        if (newQty > 0)
                        {
                            var newAvg = ((oldQty * oldAvg) + (line.Qty * incomingUnitCost)) / newQty;
                            stock.AverageCostLyd = RoundMoney(newAvg);
                        }
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
                    ledger.UnitCostLyd = incomingUnitCost;
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

                await ReplaceFinancialEntriesAsync(invoice, invoice.CreatedByUserId, invoice.CreatedByUserName);

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
            });
        }

        public async Task CancelAsync(Guid invoiceId, string? cancelledBy)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                var invoice = await _context.Set<PurchaseInvoice>()
                    .Include(i => i.Lines)
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    throw new InvalidOperationException("لم يتم العثور على الفاتورة.");

                if (!string.Equals(invoice.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("يمكن إلغاء الفواتير المرحلة فقط.");

                var lines = invoice.Lines?.ToList() ?? new List<PurchaseLine>();
                if (!lines.Any())
                    throw new InvalidOperationException("الفاتورة لا تحتوي على بنود.");

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
                        var itemName = itemNames.TryGetValue(line.ItemId, out var name) ? name : "صنف";
                        throw new InvalidOperationException(
                            $"لا يمكن إلغاء شراء {itemName}. المطلوب {FormatQuantity(line.Qty)}، المتاح {FormatQuantity(available)}.");
                    }

                    var removedQty = line.Qty;
                    var removedValue = line.UnitCostLyd * removedQty;
                    var currentQty = stock.QuantityOnHand;
                    var currentValue = currentQty * stock.AverageCostLyd;
                    var newQty = currentQty - removedQty;
                    if (newQty > 0)
                    {
                        var newAvg = (currentValue - removedValue) / newQty;
                        stock.AverageCostLyd = RoundMoney(newAvg);
                    }
                    else
                    {
                        stock.AverageCostLyd = 0m;
                    }
                    stock.QuantityOnHand = newQty;

                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = line.ItemId,
                        MovementType = "Out",
                        ReferenceType = "PurchaseInvoiceCancel",
                        ReferenceId = invoice.Id,
                        QuantityChange = -line.Qty,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = $"Cancelled by {cancelledBy ?? "unknown"}",
                        UnitCostLyd = line.UnitCostLyd
                    });
                }

                invoice.Status = "Cancelled";
                await RemoveFinancialEntriesAsync(invoice.Id);

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
            });
        }

        public async Task UpdatePostedAsync(
            Guid invoiceId,
            DateOnly invoiceDate,
            decimal rate,
            string? note,
            Guid? supplierId,
            string? paymentMethod,
            DateOnly? dueDate,
            IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur, decimal? SellPriceLyd)> lines,
            string? editedBy)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("فاتورة الشراء تتطلب بنداً واحداً على الأقل.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("الحد الأقصى لبنود الفاتورة هو 200.");

            if (rate <= 0)
                throw new InvalidOperationException("يجب أن يكون سعر الصرف أكبر من الصفر.");

            var normalizedPaymentMethod = NormalizePaymentMethod(paymentMethod);
            if (string.Equals(normalizedPaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
            {
                if (!supplierId.HasValue || supplierId == Guid.Empty)
                    throw new InvalidOperationException("حدد المورد عند الشراء الآجل.");

                if (!dueDate.HasValue)
                    throw new InvalidOperationException("حدد تاريخ الاستحقاق عند الشراء الآجل.");

                if (dueDate.Value < invoiceDate)
                    throw new InvalidOperationException("تاريخ الاستحقاق لا يمكن أن يكون قبل تاريخ الفاتورة.");
            }
            else
            {
                dueDate = null;
            }

            if (supplierId.HasValue && supplierId != Guid.Empty)
            {
                var supplierExists = await _context.Set<Supplier>().AnyAsync(s => s.Id == supplierId.Value);
                if (!supplierExists)
                    throw new InvalidOperationException("المورد المحدد غير موجود.");
            }

            foreach (var line in lineList)
            {
                if (line.Qty <= 0)
                    throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

                if (line.Qty != decimal.Truncate(line.Qty))
                    throw new InvalidOperationException("يجب أن تكون الكمية رقماً صحيحاً.");

                if (line.UnitPriceEur < 0)
                    throw new InvalidOperationException("لا يمكن أن يكون سعر الوحدة سالباً.");

                if (line.SellPriceLyd.HasValue && line.SellPriceLyd.Value < 0)
                    throw new InvalidOperationException("لا يمكن أن يكون سعر البيع سالباً.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                var invoice = await _context.Set<PurchaseInvoice>()
                    .Include(i => i.Lines)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    throw new InvalidOperationException("لم يتم العثور على الفاتورة.");

                if (!string.Equals(invoice.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("يمكن تعديل فواتير الشراء المرحلة فقط.");

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
                        throw new InvalidOperationException("الصنف المحدد غير موجود.");
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
                        var itemName = itemNames.TryGetValue(itemId, out var name) ? name : "صنف";
                        throw new InvalidOperationException(
                            $"لا يمكن تقليل كمية الشراء لـ {itemName}. المتاح {FormatQuantity(stock.QuantityOnHand)}، التغيير المطلوب {FormatQuantity(delta)}.");
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
                    var oldLines = (invoice.Lines ?? new List<PurchaseLine>()).Where(l => l.ItemId == itemId).ToList();
                    var oldValue = oldLines.Sum(l => l.LineTotalDinar);
                    var newLines = lineList.Where(l => l.ItemId == itemId).ToList();
                    decimal newValue = 0m;
                    foreach (var nl in newLines)
                    {
                        var lTotalEur = RoundMoney(nl.Qty * nl.UnitPriceEur);
                        var lTotalDinar = RoundMoney(lTotalEur * rate);
                        newValue += lTotalDinar;
                    }

                    var deltaValue = newValue - oldValue;

                    var currentQty = stock.QuantityOnHand;
                    var currentValue = currentQty * stock.AverageCostLyd;
                    var newStockQty = currentQty + delta;
                    if (newStockQty > 0)
                    {
                        var newAvg = (currentValue + deltaValue) / newStockQty;
                        stock.AverageCostLyd = RoundMoney(newAvg);
                    }
                    else
                    {
                        stock.AverageCostLyd = 0m;
                    }

                    stock.QuantityOnHand = newStockQty;

                    var unitCostForLedger = delta != 0 ? RoundMoney(deltaValue / delta) : 0m;
                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = itemId,
                        MovementType = delta >= 0 ? "In" : "Out",
                        ReferenceType = "PurchaseInvoiceEdit",
                        ReferenceId = invoice.Id,
                        QuantityChange = delta,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = $"Edited by {editedBy ?? "unknown"}",
                        UnitCostLyd = unitCostForLedger
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

                    var unitCostLyd = line.Qty > 0 ? RoundMoney(lineTotalDinar / line.Qty) : 0m;
                    _context.Set<PurchaseLine>().Add(new PurchaseLine
                    {
                        PurchaseInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = line.Qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = line.UnitPriceEur,
                        LineTotalEur = lineTotalEur,
                        LineTotalDinar = lineTotalDinar,
                        UnitCostLyd = unitCostLyd,
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
                invoice.SupplierId = supplierId;
                invoice.PaymentMethod = normalizedPaymentMethod;
                invoice.DueDate = dueDate;
                invoice.TotalEur = RoundMoney(totalEur);
                invoice.TotalDinar = RoundMoney(totalDinar);

                await ReplaceFinancialEntriesAsync(invoice, null, editedBy);

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
            });
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

        private async Task ReplaceFinancialEntriesAsync(PurchaseInvoice invoice, string? userId, string? userName)
        {
            await RemoveFinancialEntriesAsync(invoice.Id);

            var total = RoundMoney(invoice.TotalDinar);
            if (total <= 0)
                return;

            var (counterCode, counterName) = ResolvePurchaseCounterAccount(invoice.PaymentMethod);

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = invoice.InvoiceDate,
                SourceType = JournalSourceType,
                SourceId = invoice.Id,
                DocumentNo = invoice.Number,
                AccountCode = AccountInventoryCode,
                AccountName = AccountInventoryName,
                Debit = total,
                Credit = 0m,
                Note = invoice.Note,
                CreatedByUserId = userId,
                CreatedByUserName = userName
            });

            _context.Set<FinJournalEntry>().Add(new FinJournalEntry
            {
                EntryDate = invoice.InvoiceDate,
                SourceType = JournalSourceType,
                SourceId = invoice.Id,
                DocumentNo = invoice.Number,
                AccountCode = counterCode,
                AccountName = counterName,
                Debit = 0m,
                Credit = total,
                Note = invoice.Note,
                CreatedByUserId = userId,
                CreatedByUserName = userName
            });
        }

        private async Task RemoveFinancialEntriesAsync(Guid invoiceId)
        {
            var existing = await _context.Set<FinJournalEntry>()
                .Where(e => e.SourceType == JournalSourceType && e.SourceId == invoiceId)
                .ToListAsync();

            if (existing.Count > 0)
                _context.Set<FinJournalEntry>().RemoveRange(existing);
        }

        private static (string Code, string Name) ResolvePurchaseCounterAccount(string? paymentMethod)
        {
            if (string.Equals(paymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
                return (AccountSupplierCode, AccountSupplierName);

            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return (AccountBankCode, AccountBankName);

            return (AccountCashCode, AccountCashName);
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return "Cash";

            if (string.Equals(paymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
                return "Credit";

            if (string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return "Transfer";

            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase))
                return "Card";

            return "Cash";
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
