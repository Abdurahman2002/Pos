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
            if (!invoice.SupplierId.HasValue || invoice.SupplierId == Guid.Empty)
                throw new InvalidOperationException("حدد المورد لفاتورة المشتريات.");

            if (string.Equals(invoice.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
            {
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

                var itemIds = lineList.Select(l => l.ItemId).Distinct().ToList();
                var itemById = await _context.Set<Item>()
                    .Where(i => itemIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i);

                var preparedTotals = PreparePurchaseTotals(
                    lineList,
                    invoice.CurrencyCode,
                    invoice.EurToDinarRateSnapshot,
                    invoice.DiscountType,
                    invoice.DiscountValue);

                for (var lineIndex = 0; lineIndex < preparedTotals.Lines.Count; lineIndex++)
                {
                    var line = preparedTotals.Lines[lineIndex];
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

                    var purchaseLine = new PurchaseLine
                    {
                        PurchaseInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = line.Qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = line.UnitPriceEur,
                        LineTotalEur = line.LineTotalEur,
                        LineTotalDinar = line.LineTotalDinar,
                        DiscountAllocatedEur = line.DiscountAllocatedEur,
                        DiscountAllocatedDinar = line.DiscountAllocatedDinar,
                        UnitCostLyd = line.UnitCostLyd,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRateSnapshot = invoice.EurToDinarRateSnapshot
                    };
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
                        Note = invoice.Note,
                        ValueChangeLyd = line.LineTotalDinar
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

                ApplyTotalsToInvoice(invoice, preparedTotals);

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
                        UnitCostLyd = line.UnitCostLyd,
                        ValueChangeLyd = -removedValue
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
            string? discountType,
            decimal discountValue,
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
            if (!supplierId.HasValue || supplierId == Guid.Empty)
                throw new InvalidOperationException("حدد المورد لفاتورة المشتريات.");

            if (string.Equals(normalizedPaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
            {
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

                var invoiceCurrency = "LYD";
                var effectiveRate = 1m;
                var preparedTotals = PreparePurchaseTotals(
                    lineList,
                    invoiceCurrency,
                    effectiveRate,
                    discountType,
                    discountValue);

                var oldQtyByItem = (invoice.Lines ?? new List<PurchaseLine>())
                    .GroupBy(l => l.ItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

                var newQtyByItem = preparedTotals.Lines
                    .GroupBy(l => l.ItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

                var oldValueByItem = (invoice.Lines ?? new List<PurchaseLine>())
                    .GroupBy(l => l.ItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotalDinar));

                var newValueByItem = preparedTotals.Lines
                    .GroupBy(l => l.ItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotalDinar));

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
                    var oldValue = oldValueByItem.TryGetValue(itemId, out var oldItemValue) ? oldItemValue : 0m;
                    var newValue = newValueByItem.TryGetValue(itemId, out var newItemValue) ? newItemValue : 0m;
                    var deltaValue = RoundMoney(newValue - oldValue);
                    if (delta == 0 && deltaValue == 0)
                        continue;

                    var stock = stockByItem[itemId];

                    var currentQty = stock.QuantityOnHand;
                    var currentValue = currentQty * stock.AverageCostLyd;
                    var newStockQty = currentQty + delta;
                    if (newStockQty > 0)
                    {
                        var newStockValue = currentValue + deltaValue;
                        if (newStockValue < 0)
                        {
                            var itemName = itemNames.TryGetValue(itemId, out var name) ? name : "Item";
                            throw new InvalidOperationException($"Cannot reduce the purchase value below zero for {itemName}.");
                        }

                        var newAvg = newStockValue / newStockQty;
                        stock.AverageCostLyd = RoundMoney(newAvg);
                    }
                    else
                    {
                        stock.AverageCostLyd = 0m;
                    }

                    stock.QuantityOnHand = newStockQty;

                    var unitCostForLedger = delta != 0
                        ? RoundMoney(Math.Abs(deltaValue / delta))
                        : stock.AverageCostLyd;
                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = itemId,
                        MovementType = delta >= 0 ? "In" : "Out",
                        ReferenceType = "PurchaseInvoiceEdit",
                        ReferenceId = invoice.Id,
                        QuantityChange = delta,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = $"Edited by {editedBy ?? "unknown"}",
                        UnitCostLyd = unitCostForLedger,
                        ValueChangeLyd = deltaValue
                    });
                }

                _context.Set<PurchaseLine>().RemoveRange(invoice.Lines ?? new List<PurchaseLine>());

                for (var lineIndex = 0; lineIndex < preparedTotals.Lines.Count; lineIndex++)
                {
                    var line = preparedTotals.Lines[lineIndex];
                    _context.Set<PurchaseLine>().Add(new PurchaseLine
                    {
                        PurchaseInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = line.Qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = line.UnitPriceEur,
                        LineTotalEur = line.LineTotalEur,
                        LineTotalDinar = line.LineTotalDinar,
                        DiscountAllocatedEur = line.DiscountAllocatedEur,
                        DiscountAllocatedDinar = line.DiscountAllocatedDinar,
                        UnitCostLyd = line.UnitCostLyd,
                        CurrencyCode = invoiceCurrency,
                        ExchangeRateSnapshot = effectiveRate
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
                invoice.CurrencyCode = invoiceCurrency;
                invoice.EurToDinarRateSnapshot = effectiveRate;
                invoice.Note = note;
                invoice.SupplierId = supplierId;
                invoice.PaymentMethod = normalizedPaymentMethod;
                invoice.DueDate = dueDate;
                ApplyTotalsToInvoice(invoice, preparedTotals);

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

        private sealed class PreparedPurchaseTotals
        {
            public List<PreparedPurchaseLine> Lines { get; set; } = new();
            public decimal SubtotalEur { get; set; }
            public decimal SubtotalDinar { get; set; }
            public string DiscountType { get; set; } = "Amount";
            public decimal DiscountValue { get; set; }
            public decimal DiscountEur { get; set; }
            public decimal DiscountDinar { get; set; }
            public decimal TotalEur { get; set; }
            public decimal TotalDinar { get; set; }
        }

        private sealed class PreparedPurchaseLine
        {
            public Guid ItemId { get; set; }
            public decimal Qty { get; set; }
            public decimal UnitPriceEur { get; set; }
            public decimal GrossLineTotalEur { get; set; }
            public decimal GrossLineTotalDinar { get; set; }
            public decimal DiscountAllocatedEur { get; set; }
            public decimal DiscountAllocatedDinar { get; set; }
            public decimal LineTotalEur { get; set; }
            public decimal LineTotalDinar { get; set; }
            public decimal UnitCostLyd { get; set; }
            public decimal? SellPriceLyd { get; set; }
        }

        private static PreparedPurchaseTotals PreparePurchaseTotals(
            IReadOnlyList<(Guid ItemId, decimal Qty, decimal UnitPriceEur, decimal? SellPriceLyd)> lines,
            string currencyCode,
            decimal rate,
            string? discountType,
            decimal discountValue)
        {
            var normalizedDiscountType = NormalizeDiscountType(discountType);
            if (discountValue < 0)
                throw new InvalidOperationException("Purchase discount cannot be negative.");

            if (normalizedDiscountType == "Percent" && discountValue > 100)
                throw new InvalidOperationException("Purchase discount percent cannot exceed 100%.");

            var result = new PreparedPurchaseTotals
            {
                DiscountType = normalizedDiscountType,
                DiscountValue = RoundMoney(discountValue)
            };

            foreach (var line in lines)
            {
                var lineTotalEur = RoundMoney(line.Qty * line.UnitPriceEur);
                var lineTotalDinar = RoundMoney(lineTotalEur * rate);

                result.Lines.Add(new PreparedPurchaseLine
                {
                    ItemId = line.ItemId,
                    Qty = line.Qty,
                    UnitPriceEur = line.UnitPriceEur,
                    GrossLineTotalEur = lineTotalEur,
                    GrossLineTotalDinar = lineTotalDinar,
                    SellPriceLyd = line.SellPriceLyd
                });
            }

            result.SubtotalEur = RoundMoney(result.Lines.Sum(l => l.GrossLineTotalEur));
            result.SubtotalDinar = RoundMoney(result.Lines.Sum(l => l.GrossLineTotalDinar));

            var discountEur = normalizedDiscountType == "Percent"
                ? RoundMoney(result.SubtotalEur * (discountValue / 100m))
                : RoundMoney(discountValue);

            if (discountEur > result.SubtotalEur)
                throw new InvalidOperationException("Purchase discount cannot exceed the invoice subtotal.");

            result.DiscountEur = discountEur;
            result.DiscountDinar = normalizedDiscountType == "Percent"
                ? RoundMoney(result.SubtotalDinar * (discountValue / 100m))
                : RoundMoney(discountEur * rate);

            if (result.DiscountDinar > result.SubtotalDinar)
                result.DiscountDinar = result.SubtotalDinar;

            decimal allocatedEur = 0m;
            decimal allocatedDinar = 0m;

            for (var index = 0; index < result.Lines.Count; index++)
            {
                var line = result.Lines[index];
                var isLast = index == result.Lines.Count - 1;

                var lineDiscountEur = 0m;
                if (result.SubtotalEur > 0)
                {
                    lineDiscountEur = isLast
                        ? result.DiscountEur - allocatedEur
                        : RoundMoney(result.DiscountEur * (line.GrossLineTotalEur / result.SubtotalEur));
                }

                var lineDiscountDinar = 0m;
                if (result.SubtotalDinar > 0)
                {
                    lineDiscountDinar = isLast
                        ? result.DiscountDinar - allocatedDinar
                        : RoundMoney(result.DiscountDinar * (line.GrossLineTotalDinar / result.SubtotalDinar));
                }

                line.DiscountAllocatedEur = RoundMoney(Math.Min(line.GrossLineTotalEur, Math.Max(0m, lineDiscountEur)));
                line.DiscountAllocatedDinar = RoundMoney(Math.Min(line.GrossLineTotalDinar, Math.Max(0m, lineDiscountDinar)));
                line.LineTotalEur = RoundMoney(line.GrossLineTotalEur - line.DiscountAllocatedEur);
                line.LineTotalDinar = RoundMoney(line.GrossLineTotalDinar - line.DiscountAllocatedDinar);
                line.UnitCostLyd = line.Qty > 0 ? RoundMoney(line.LineTotalDinar / line.Qty) : 0m;

                allocatedEur += line.DiscountAllocatedEur;
                allocatedDinar += line.DiscountAllocatedDinar;
            }

            result.TotalEur = RoundMoney(result.Lines.Sum(l => l.LineTotalEur));
            result.TotalDinar = RoundMoney(result.Lines.Sum(l => l.LineTotalDinar));
            result.DiscountEur = RoundMoney(result.SubtotalEur - result.TotalEur);
            result.DiscountDinar = RoundMoney(result.SubtotalDinar - result.TotalDinar);
            return result;
        }

        private static void ApplyTotalsToInvoice(PurchaseInvoice invoice, PreparedPurchaseTotals totals)
        {
            invoice.SubtotalEur = totals.SubtotalEur;
            invoice.SubtotalDinar = totals.SubtotalDinar;
            invoice.DiscountType = totals.DiscountType;
            invoice.DiscountValue = totals.DiscountValue;
            invoice.DiscountEur = totals.DiscountEur;
            invoice.DiscountDinar = totals.DiscountDinar;
            invoice.TotalEur = totals.TotalEur;
            invoice.TotalDinar = totals.TotalDinar;
        }

        private static string NormalizeDiscountType(string? discountType)
        {
            return string.Equals(discountType, "Percent", StringComparison.OrdinalIgnoreCase)
                ? "Percent"
                : "Amount";
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
