using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    public class SalesService
    {
        private const string StatusPendingApproval = "PendingApproval";
        private const string StatusPosted = "Posted";
        private const string StatusCancelled = "Cancelled";
        private const string JournalSourceType = "SalesInvoice";

        private const string AccountCashCode = "1101";
        private const string AccountCashName = "الصندوق";
        private const string AccountBankCode = "1102";
        private const string AccountBankName = "البنك";
        private const string AccountCustomerCode = "1201";
        private const string AccountCustomerName = "ذمم العملاء";
        private const string AccountSalesRevenueCode = "4101";
        private const string AccountSalesRevenueName = "إيراد المبيعات";
        private const string AccountInventoryCode = "1301";
        private const string AccountInventoryName = "المخزون";
        private const string AccountCogsCode = "5001";
        private const string AccountCogsName = "تكلفة البضاعة المباعة";

        private readonly AppDbContext _context;
        private readonly ILogger<SalesService> _logger;

        public SalesService(AppDbContext context, ILogger<SalesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Guid> CreateAsync(SalesInvoice invoice, IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> lines)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("فاتورة البيع تتطلب بنداً واحداً على الأقل.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("الحد الأقصى لبنود الفاتورة هو 200.");

            if (!IsSupportedCurrency(invoice.CurrencyCode))
                throw new InvalidOperationException("يُسمح فقط باليورو أو الدينار الليبي.");

            invoice.CurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
            if (invoice.CurrencyCode == "LYD")
                invoice.EurToDinarRateSnapshot = 1m;
            else if (invoice.EurToDinarRateSnapshot <= 0)
                throw new InvalidOperationException("يجب أن يكون سعر الصرف أكبر من الصفر.");
            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = StatusPosted;
            invoice.SubmittedAt ??= DateTime.UtcNow;
            invoice.ApprovedAt ??= DateTime.UtcNow;
            invoice.ApprovedByUserName ??= invoice.CreatedByUserName;

            _logger.LogInformation("Creating sales invoice for user {User} with {LineCount} lines.", invoice.CreatedByUserName, lineList.Count);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                using var tx = await _context.Database.BeginTransactionAsync();

                _context.Set<SalesInvoice>().Add(invoice);
                await _context.SaveChangesAsync();

                decimal totalEur = 0;
                decimal totalDinar = 0;

                var itemIds = lineList.Select(l => l.ItemId).Distinct().ToList();
                var itemNames = await _context.Set<Item>()
                    .AsNoTracking()
                    .Where(i => itemIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i.Name);

                for (var lineIndex = 0; lineIndex < lineList.Count; lineIndex++)
                {
                    var line = lineList[lineIndex];
                    if (line.Qty <= 0)
                        throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

                    if (line.Qty != decimal.Truncate(line.Qty))
                        throw new InvalidOperationException("يجب أن تكون الكمية رقماً صحيحاً.");

                    if (line.UnitPriceEur < 0)
                        throw new InvalidOperationException("لا يمكن أن يكون سعر الوحدة سالباً.");

                    if (!itemNames.ContainsKey(line.ItemId))
                        throw new InvalidOperationException("الصنف المحدد غير موجود.");

                    var stock = await _context.Set<InvStockBalance>()
                        .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                    var available = stock?.QuantityOnHand ?? 0m;
                    if (available < line.Qty)
                    {
                        var itemName = itemNames.TryGetValue(line.ItemId, out var name) ? name : "صنف";
                        throw new InvalidOperationException(
                            $"رصيد غير كافٍ لـ {itemName}. المطلوب {FormatQuantity(line.Qty)}، المتاح {FormatQuantity(available)}.");
                    }

                    var lineTotalEur = RoundMoney(line.Qty * line.UnitPriceEur);
                    var lineTotalDinar = RoundMoney(lineTotalEur * invoice.EurToDinarRateSnapshot);

                    totalEur += lineTotalEur;
                    totalDinar += lineTotalDinar;

                    var salesLine = new SalesLine
                    {
                        SalesInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = line.Qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = line.UnitPriceEur,
                        LineTotalEur = lineTotalEur,
                        LineTotalDinar = lineTotalDinar,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRateSnapshot = invoice.EurToDinarRateSnapshot
                    };
                    var unitCost = stock!.AverageCostLyd;
                    salesLine.UnitCostLyd = unitCost;
                    salesLine.LineCostDinar = RoundMoney(unitCost * line.Qty);
                    _context.Set<SalesLine>().Add(salesLine);

                    stock.QuantityOnHand -= line.Qty;

                    var ledger = new InvStockLedger
                    {
                        ItemId = line.ItemId,
                        MovementType = "Out",
                        ReferenceType = "SalesInvoice",
                        ReferenceId = invoice.Id,
                        QuantityChange = -line.Qty,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = invoice.Note,
                        UnitCostLyd = unitCost,
                        ValueChangeLyd = -salesLine.LineCostDinar
                    };
                    _context.Set<InvStockLedger>().Add(ledger);
                }

                invoice.TotalEur = RoundMoney(totalEur);
                invoice.TotalDinar = RoundMoney(totalDinar);

                await ReplaceFinancialEntriesAsync(invoice, invoice.CreatedByUserId, invoice.CreatedByUserName);

                _context.Set<AuditLog>().Add(new AuditLog
                {
                    Action = "Create",
                    EntityType = "SalesInvoice",
                    EntityId = invoice.Id,
                    EntityNumber = invoice.Number,
                    Description = $"Lines: {lineList.Count}, TotalEUR: {invoice.TotalEur:0.00}, TotalLYD: {invoice.TotalDinar:0.00}, Rate: {invoice.EurToDinarRateSnapshot:0.000000}",
                    CreatedByUserId = invoice.CreatedByUserId,
                    CreatedByUserName = invoice.CreatedByUserName
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Sales invoice {Number} created successfully.", invoice.Number);
                return invoice.Id;
            });
        }

        public async Task<Guid> CreateReturnAsync(SalesInvoice invoice, IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> lines)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("فاتورة المرتجع تتطلب بنداً واحداً على الأقل.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("الحد الأقصى لبنود الفاتورة هو 200.");

            if (!IsSupportedCurrency(invoice.CurrencyCode))
                throw new InvalidOperationException("يُسمح فقط باليورو أو الدينار الليبي.");

            invoice.CurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
            if (invoice.CurrencyCode == "LYD")
                invoice.EurToDinarRateSnapshot = 1m;
            else if (invoice.EurToDinarRateSnapshot <= 0)
                throw new InvalidOperationException("يجب أن يكون سعر الصرف أكبر من الصفر.");
            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateReturnNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = StatusPosted;
            invoice.SubmittedAt ??= DateTime.UtcNow;
            invoice.ApprovedAt ??= DateTime.UtcNow;
            invoice.ApprovedByUserName ??= invoice.CreatedByUserName;

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                using var tx = await _context.Database.BeginTransactionAsync();

                _context.Set<SalesInvoice>().Add(invoice);
                await _context.SaveChangesAsync();

                decimal totalEur = 0;
                decimal totalDinar = 0;

                var itemIds = lineList.Select(l => l.ItemId).Distinct().ToList();
                var itemNames = await _context.Set<Item>()
                    .AsNoTracking()
                    .Where(i => itemIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i.Name);

                for (var lineIndex = 0; lineIndex < lineList.Count; lineIndex++)
                {
                    var line = lineList[lineIndex];
                    if (line.Qty <= 0)
                        throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

                    if (line.Qty != decimal.Truncate(line.Qty))
                        throw new InvalidOperationException("يجب أن تكون الكمية رقماً صحيحاً.");

                    if (line.UnitPriceEur < 0)
                        throw new InvalidOperationException("لا يمكن أن يكون سعر الوحدة سالباً.");

                    if (!itemNames.ContainsKey(line.ItemId))
                        throw new InvalidOperationException("الصنف المحدد غير موجود.");

                    var stock = await _context.Set<InvStockBalance>()
                        .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                    if (stock == null)
                    {
                        stock = new InvStockBalance
                        {
                            ItemId = line.ItemId,
                            QuantityOnHand = 0m
                        };
                        _context.Set<InvStockBalance>().Add(stock);
                    }

                    var qty = -line.Qty;
                    var lineTotalEur = RoundMoney(qty * line.UnitPriceEur);
                    var lineTotalDinar = RoundMoney(lineTotalEur * invoice.EurToDinarRateSnapshot);

                    totalEur += lineTotalEur;
                    totalDinar += lineTotalDinar;

                    var unitCost = stock.AverageCostLyd;
                    var salesLine = new SalesLine
                    {
                        SalesInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = line.UnitPriceEur,
                        LineTotalEur = lineTotalEur,
                        LineTotalDinar = lineTotalDinar,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRateSnapshot = invoice.EurToDinarRateSnapshot,
                        UnitCostLyd = unitCost,
                        LineCostDinar = RoundMoney(unitCost * qty)
                    };
                    _context.Set<SalesLine>().Add(salesLine);

                    stock.QuantityOnHand += line.Qty;

                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = line.ItemId,
                        MovementType = "In",
                        ReferenceType = "SalesReturn",
                        ReferenceId = invoice.Id,
                        QuantityChange = line.Qty,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = invoice.Note,
                        UnitCostLyd = unitCost,
                        ValueChangeLyd = RoundMoney(unitCost * line.Qty)
                    });
                }

                invoice.TotalEur = RoundMoney(totalEur);
                invoice.TotalDinar = RoundMoney(totalDinar);

                await ReplaceFinancialEntriesAsync(invoice, invoice.CreatedByUserId, invoice.CreatedByUserName);

                _context.Set<AuditLog>().Add(new AuditLog
                {
                    Action = "CreateReturn",
                    EntityType = "SalesInvoice",
                    EntityId = invoice.Id,
                    EntityNumber = invoice.Number,
                    Description = $"Return invoice. Lines: {lineList.Count}, TotalEUR: {invoice.TotalEur:0.00}",
                    CreatedByUserId = invoice.CreatedByUserId,
                    CreatedByUserName = invoice.CreatedByUserName
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return invoice.Id;
            });
        }

        public async Task CancelAsync(Guid invoiceId, string? cancelledBy)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                var invoice = await _context.Set<SalesInvoice>()
                    .Include(i => i.Lines)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    throw new InvalidOperationException("لم يتم العثور على الفاتورة.");

                if (!string.Equals(invoice.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("يمكن إلغاء الفواتير المرحلة فقط.");

                var lines = invoice.Lines?.ToList() ?? new List<SalesLine>();
                if (!lines.Any())
                    throw new InvalidOperationException("الفاتورة لا تحتوي على بنود.");

                var negativeLines = lines.Where(l => l.Qty < 0).ToList();
                if (negativeLines.Any())
                {
                    var negItemIds = negativeLines.Select(l => l.ItemId).Distinct().ToList();
                    var stockForNeg = await _context.Set<InvStockBalance>()
                        .AsNoTracking()
                        .Where(s => negItemIds.Contains(s.ItemId))
                        .ToDictionaryAsync(s => s.ItemId, s => s.QuantityOnHand);

                    foreach (var negLine in negativeLines)
                    {
                        var available = stockForNeg.TryGetValue(negLine.ItemId, out var qty) ? qty : 0m;
                        var wouldBeRemoved = Math.Abs(negLine.Qty);
                        if (available < wouldBeRemoved)
                        {
                            var itemName = await _context.Set<Item>()
                                .AsNoTracking()
                                .Where(i => i.Id == negLine.ItemId)
                                .Select(i => i.Name)
                                .FirstOrDefaultAsync() ?? negLine.ItemId.ToString();
                            throw new InvalidOperationException(
                                $"تعذر إلغاء المرتجع: الكمية المتاحة لـ {itemName} غير كافية. متاح: {FormatQuantity(available)}, مطلوب: {FormatQuantity(wouldBeRemoved)}.");
                        }
                    }
                }

                using var tx = await _context.Database.BeginTransactionAsync();

                _logger.LogInformation("Cancelling sales invoice {Number} ({Id}) by {User}.", invoice.Number, invoice.Id, cancelledBy);

                foreach (var line in lines)
                {
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

                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = line.ItemId,
                        MovementType = line.Qty >= 0 ? "In" : "Out",
                        ReferenceType = "SalesInvoiceCancel",
                        ReferenceId = invoice.Id,
                        QuantityChange = line.Qty,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = $"Cancelled by {cancelledBy ?? "unknown"}",
                        UnitCostLyd = line.UnitCostLyd,
                        ValueChangeLyd = line.LineCostDinar
                    });
                }

                invoice.Status = StatusCancelled;
                await RemoveFinancialEntriesAsync(invoice.Id);

                _context.Set<AuditLog>().Add(new AuditLog
                {
                    Action = "Cancel",
                    EntityType = "SalesInvoice",
                    EntityId = invoice.Id,
                    EntityNumber = invoice.Number,
                    Description = $"Cancelled sales invoice. Lines: {lines.Count}",
                    CreatedByUserName = cancelledBy
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Sales invoice {Number} cancelled.", invoice.Number);
            });
        }

        public async Task<Guid> CreatePendingAsync(SalesInvoice invoice, IEnumerable<(Guid ItemId, decimal Qty)> lines)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("فاتورة البيع تتطلب بنداً واحداً على الأقل.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("الحد الأقصى لبنود الفاتورة هو 200.");

            if (!IsSupportedCurrency(invoice.CurrencyCode))
                throw new InvalidOperationException("يُسمح فقط باليورو أو الدينار الليبي.");

            invoice.CurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
            if (invoice.EurToDinarRateSnapshot <= 0)
                invoice.EurToDinarRateSnapshot = 1m;
            if (invoice.CurrencyCode == "LYD")
                invoice.EurToDinarRateSnapshot = 1m;
            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = StatusPendingApproval;
            invoice.TotalEur = 0;
            invoice.TotalDinar = 0;
            invoice.SubmittedAt = DateTime.UtcNow;
            invoice.ApprovedAt = null;
            invoice.ApprovedByUserName = null;
            invoice.CustomerId = null;

            _logger.LogInformation("Creating pending sales invoice for user {User} with {LineCount} lines.", invoice.CreatedByUserName, lineList.Count);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                using var tx = await _context.Database.BeginTransactionAsync();

                _context.Set<SalesInvoice>().Add(invoice);
                await _context.SaveChangesAsync();

                var itemIds = lineList.Select(l => l.ItemId).Distinct().ToList();
                var itemExists = await _context.Set<Item>()
                    .AsNoTracking()
                    .Where(i => itemIds.Contains(i.Id))
                    .Select(i => i.Id)
                    .ToListAsync();

                for (var lineIndex = 0; lineIndex < lineList.Count; lineIndex++)
                {
                    var line = lineList[lineIndex];
                    if (line.Qty <= 0)
                        throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

                    if (!itemExists.Contains(line.ItemId))
                        throw new InvalidOperationException("الصنف المحدد غير موجود.");

                    _context.Set<SalesLine>().Add(new SalesLine
                    {
                        SalesInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = line.Qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = 0,
                        LineTotalEur = 0,
                        LineTotalDinar = 0,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRateSnapshot = invoice.EurToDinarRateSnapshot
                    });
                }

                _context.Set<AuditLog>().Add(new AuditLog
                {
                    Action = "CreatePending",
                    EntityType = "SalesInvoice",
                    EntityId = invoice.Id,
                    EntityNumber = invoice.Number,
                    Description = $"Pending approval. Lines: {lineList.Count}",
                    CreatedByUserId = invoice.CreatedByUserId,
                    CreatedByUserName = invoice.CreatedByUserName
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("Pending sales invoice {Number} created successfully.", invoice.Number);
                return invoice.Id;
            });
        }

        public async Task ApprovePendingAsync(
            Guid invoiceId,
            Guid customerId,
            decimal rate,
            IEnumerable<(Guid LineId, decimal UnitPriceEur)> linePrices,
            string? approvedBy)
        {
            var priceMap = linePrices.ToDictionary(x => x.LineId, x => x.UnitPriceEur);
            if (!priceMap.Any())
                throw new InvalidOperationException("أسعار البنود مطلوبة.");

            if (rate <= 0)
                throw new InvalidOperationException("يجب أن يكون سعر الصرف أكبر من الصفر.");

            var customerExists = await _context.Set<Customer>().AsNoTracking().AnyAsync(c => c.Id == customerId);
            if (!customerExists)
                throw new InvalidOperationException("العميل المحدد غير موجود.");

            var invoice = await _context.Set<SalesInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("لم يتم العثور على الفاتورة.");

            if (!string.Equals(invoice.Status, StatusPendingApproval, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("يمكن اعتماد فواتير البيع المعلقة فقط.");

            var lines = invoice.Lines?.ToList() ?? new List<SalesLine>();
            if (!lines.Any())
                throw new InvalidOperationException("الفاتورة لا تحتوي على بنود.");

            foreach (var line in lines)
            {
                if (!priceMap.ContainsKey(line.Id))
                    throw new InvalidOperationException("سعر واحد أو أكثر من البنود مفقود.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                using var tx = await _context.Database.BeginTransactionAsync();

                decimal totalEur = 0;
                decimal totalDinar = 0;

                var itemIds = lines.Select(l => l.ItemId).Distinct().ToList();
                var itemNames = await _context.Set<Item>()
                    .AsNoTracking()
                    .Where(i => itemIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i.Name);

                foreach (var line in lines)
                {
                    var unitPrice = priceMap[line.Id];
                    if (unitPrice < 0)
                        throw new InvalidOperationException("لا يمكن أن يكون سعر الوحدة سالباً.");

                    var stock = await _context.Set<InvStockBalance>()
                        .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                    var available = stock?.QuantityOnHand ?? 0m;
                    if (available < line.Qty)
                    {
                        var itemName = itemNames.TryGetValue(line.ItemId, out var name) ? name : "صنف";
                        throw new InvalidOperationException(
                            $"رصيد غير كافٍ لـ {itemName}. المطلوب {FormatQuantity(line.Qty)}، المتاح {FormatQuantity(available)}.");
                    }

                    var lineTotalEur = RoundMoney(line.Qty * unitPrice);
                    var lineTotalDinar = RoundMoney(lineTotalEur * rate);

                    line.UnitPriceEur = unitPrice;
                    line.LineTotalEur = lineTotalEur;
                    line.LineTotalDinar = lineTotalDinar;
                    line.ExchangeRateSnapshot = rate;

                    var unitCost = stock!.AverageCostLyd;
                    line.UnitCostLyd = unitCost;
                    line.LineCostDinar = RoundMoney(unitCost * line.Qty);

                    totalEur += lineTotalEur;
                    totalDinar += lineTotalDinar;

                    stock!.QuantityOnHand -= line.Qty;

                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = line.ItemId,
                        MovementType = "Out",
                        ReferenceType = "SalesInvoice",
                        ReferenceId = invoice.Id,
                        QuantityChange = -line.Qty,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = invoice.Note,
                        UnitCostLyd = unitCost,
                        ValueChangeLyd = -line.LineCostDinar
                    });
                }

                invoice.CustomerId = customerId;
                invoice.EurToDinarRateSnapshot = rate;
                invoice.TotalEur = RoundMoney(totalEur);
                invoice.TotalDinar = RoundMoney(totalDinar);
                invoice.Status = StatusPosted;
                invoice.ApprovedAt = DateTime.UtcNow;
                invoice.ApprovedByUserName = approvedBy;

                await ReplaceFinancialEntriesAsync(invoice, null, approvedBy);

                _context.Set<AuditLog>().Add(new AuditLog
                {
                    Action = "Approve",
                    EntityType = "SalesInvoice",
                    EntityId = invoice.Id,
                    EntityNumber = invoice.Number,
                    Description = $"Approved pending sales invoice. Lines: {lines.Count}, TotalEUR: {invoice.TotalEur:0.00}",
                    CreatedByUserName = approvedBy
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Pending sales invoice {Number} approved by {User}.", invoice.Number, approvedBy);
            });
        }

        public async Task UpdatePostedAsync(
            Guid invoiceId,
            DateOnly invoiceDate,
            Guid? customerId,
            decimal rate,
            string? note,
            IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> lines,
            string? editedBy,
            string? paymentMethod = null,
            Guid? bankId = null)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("فاتورة البيع تتطلب بنداً واحداً على الأقل.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("الحد الأقصى لبنود الفاتورة هو 200.");

            if (rate <= 0)
                throw new InvalidOperationException("يجب أن يكون سعر الصرف أكبر من الصفر.");

            foreach (var line in lineList)
            {
                if (line.Qty <= 0)
                    throw new InvalidOperationException("يجب أن تكون الكمية أكبر من الصفر.");

                if (line.Qty != decimal.Truncate(line.Qty))
                    throw new InvalidOperationException("يجب أن تكون الكمية رقماً صحيحاً.");

                if (line.UnitPriceEur < 0)
                    throw new InvalidOperationException("لا يمكن أن يكون سعر الوحدة سالباً.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                if (customerId.HasValue)
                {
                    var customerExists = await _context.Set<Customer>()
                        .AsNoTracking()
                        .AnyAsync(c => c.Id == customerId.Value);
                    if (!customerExists)
                        throw new InvalidOperationException("العميل المحدد غير موجود.");
                }

                var invoice = await _context.Set<SalesInvoice>()
                    .Include(i => i.Lines)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId);

                if (invoice == null)
                    throw new InvalidOperationException("لم يتم العثور على الفاتورة.");

                if (!string.Equals(invoice.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("يمكن تعديل فواتير البيع المرحلة فقط.");

                var normalizedPaymentMethod = NormalizePaymentMethod(paymentMethod ?? invoice.PaymentMethod);

                if (string.Equals(normalizedPaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase)
                    && !customerId.HasValue)
                    throw new InvalidOperationException("العميل مطلوب عند تعديل فاتورة بيع آجل.");

                var itemIds = lineList.Select(l => l.ItemId)
                    .Concat((invoice.Lines ?? new List<SalesLine>()).Select(l => l.ItemId))
                    .Distinct()
                    .ToList();

                var itemNames = await _context.Set<Item>()
                    .AsNoTracking()
                    .Where(i => itemIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i.Name);

                foreach (var id in lineList.Select(l => l.ItemId).Distinct())
                {
                    if (!itemNames.ContainsKey(id))
                        throw new InvalidOperationException("الصنف المحدد غير موجود.");
                }

                var oldQtyByItem = (invoice.Lines ?? new List<SalesLine>())
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
                    var deltaStock = oldQty - newQty;

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

                    var resultQty = stock.QuantityOnHand + deltaStock;
                    if (resultQty < 0)
                    {
                        var itemName = itemNames.TryGetValue(itemId, out var name) ? name : "صنف";
                        throw new InvalidOperationException(
                            $"رصيد غير كافٍ لـ {itemName}. المطلوب {FormatQuantity(newQty)}، المتاح بعد التعديل {FormatQuantity(stock.QuantityOnHand + oldQty)}.");
                    }
                }

                using var tx = await _context.Database.BeginTransactionAsync();

                foreach (var itemId in unionItemIds)
                {
                    var oldQty = oldQtyByItem.TryGetValue(itemId, out var oldVal) ? oldVal : 0m;
                    var newQty = newQtyByItem.TryGetValue(itemId, out var newVal) ? newVal : 0m;
                    var deltaStock = oldQty - newQty;
                    if (deltaStock == 0)
                        continue;

                    var stock = stockByItem[itemId];
                    stock.QuantityOnHand += deltaStock;

                    _context.Set<InvStockLedger>().Add(new InvStockLedger
                    {
                        ItemId = itemId,
                        MovementType = deltaStock >= 0 ? "In" : "Out",
                        ReferenceType = "SalesInvoiceEdit",
                        ReferenceId = invoice.Id,
                        QuantityChange = deltaStock,
                        BalanceAfter = stock.QuantityOnHand,
                        Note = $"Edited by {editedBy ?? "unknown"}",
                        UnitCostLyd = stock.AverageCostLyd,
                        ValueChangeLyd = RoundMoney(deltaStock * stock.AverageCostLyd)
                    });
                }

                _context.Set<SalesLine>().RemoveRange(invoice.Lines ?? new List<SalesLine>());

                decimal totalEur = 0m;
                decimal totalDinar = 0m;

                for (var lineIndex = 0; lineIndex < lineList.Count; lineIndex++)
                {
                    var line = lineList[lineIndex];
                    var lineTotalEur = RoundMoney(line.Qty * line.UnitPriceEur);
                    var lineTotalDinar = RoundMoney(lineTotalEur * rate);

                    totalEur += lineTotalEur;
                    totalDinar += lineTotalDinar;

                    var unitCost = stockByItem[line.ItemId].AverageCostLyd;
                    var salesLine = new SalesLine
                    {
                        SalesInvoiceId = invoice.Id,
                        ItemId = line.ItemId,
                        Qty = line.Qty,
                        LineOrder = lineIndex,
                        UnitPriceEur = line.UnitPriceEur,
                        LineTotalEur = lineTotalEur,
                        LineTotalDinar = lineTotalDinar,
                        CurrencyCode = invoice.CurrencyCode,
                        ExchangeRateSnapshot = rate,
                        UnitCostLyd = unitCost,
                        LineCostDinar = RoundMoney(unitCost * line.Qty)
                    };
                    _context.Set<SalesLine>().Add(salesLine);
                }

                invoice.InvoiceDate = invoiceDate;
                invoice.CurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
                invoice.CustomerId = customerId;
                invoice.EurToDinarRateSnapshot = invoice.CurrencyCode == "LYD" ? 1m : rate;
                invoice.Note = note;
                invoice.TotalEur = RoundMoney(totalEur);
                invoice.TotalDinar = RoundMoney(totalDinar);
                invoice.PaymentMethod = normalizedPaymentMethod;
                invoice.BankId = bankId;

                await ReplaceFinancialEntriesAsync(invoice, null, editedBy);

                _context.Set<AuditLog>().Add(new AuditLog
                {
                    Action = "Edit",
                    EntityType = "SalesInvoice",
                    EntityId = invoice.Id,
                    EntityNumber = invoice.Number,
                    Description = $"Edited sales invoice. Lines: {lineList.Count}, TotalEUR: {invoice.TotalEur:0.00}",
                    CreatedByUserName = editedBy
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation("Sales invoice {Number} edited by {User}.", invoice.Number, editedBy);
            });
        }

        private async Task<string> GenerateNumberAsync(DateOnly invoiceDate)
        {
            var datePart = invoiceDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var prefix = $"SAL-{datePart}-";

            var lastNumber = await _context.Set<SalesInvoice>()
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

        private async Task<string> GenerateReturnNumberAsync(DateOnly invoiceDate)
        {
            var datePart = invoiceDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var prefix = $"RET-{datePart}-";

            var lastNumber = await _context.Set<SalesInvoice>()
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

        private async Task ReplaceFinancialEntriesAsync(SalesInvoice invoice, string? userId, string? userName)
        {
            await RemoveFinancialEntriesAsync(invoice.Id);

            var amount = RoundMoney(invoice.TotalDinar);

            if (amount != 0m)
            {
                var (debitCode, debitName, creditCode, creditName, debitAmount, creditAmount) =
                    ResolveSalesEntry(invoice.PaymentMethod, amount);

                _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                {
                    EntryDate = invoice.InvoiceDate,
                    SourceType = JournalSourceType,
                    SourceId = invoice.Id,
                    DocumentNo = invoice.Number,
                    AccountCode = debitCode,
                    AccountName = debitName,
                    Debit = debitAmount,
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
                    AccountCode = creditCode,
                    AccountName = creditName,
                    Debit = 0m,
                    Credit = creditAmount,
                    Note = invoice.Note,
                    CreatedByUserId = userId,
                    CreatedByUserName = userName
                });
            }

            await AddCogsEntryAsync(invoice, amount >= 0m, userId, userName);
        }

        private async Task AddCogsEntryAsync(SalesInvoice invoice, bool isSale, string? userId, string? userName)
        {
            var trackedEntries = _context.ChangeTracker.Entries<SalesLine>()
                .Where(e => e.Entity.SalesInvoiceId == invoice.Id)
                .ToList();

            var deletedTrackedIds = trackedEntries
                .Where(e => e.State == EntityState.Deleted)
                .Select(e => e.Entity.Id)
                .Where(id => id != Guid.Empty)
                .ToHashSet();

            var activeTrackedLines = trackedEntries
                .Where(e => e.State != EntityState.Deleted)
                .Select(e => e.Entity)
                .ToList();

            var activeTrackedIds = activeTrackedLines
                .Select(l => l.Id)
                .Where(id => id != Guid.Empty)
                .ToHashSet();

            var persistedLines = await _context.Set<SalesLine>()
                .AsNoTracking()
                .Where(l => l.SalesInvoiceId == invoice.Id)
                .ToListAsync();

            var lines = persistedLines
                .Where(l => !deletedTrackedIds.Contains(l.Id))
                .Where(l => !activeTrackedIds.Contains(l.Id))
                .Concat(activeTrackedLines)
                .ToList();

            var totalCost = RoundMoney(lines.Sum(l => Math.Abs(l.LineCostDinar)));
            if (totalCost == 0m)
                return;

            if (isSale)
            {
                _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                {
                    EntryDate = invoice.InvoiceDate,
                    SourceType = JournalSourceType,
                    SourceId = invoice.Id,
                    DocumentNo = invoice.Number,
                    AccountCode = AccountCogsCode,
                    AccountName = AccountCogsName,
                    Debit = totalCost,
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
                    AccountCode = AccountInventoryCode,
                    AccountName = AccountInventoryName,
                    Debit = 0m,
                    Credit = totalCost,
                    Note = invoice.Note,
                    CreatedByUserId = userId,
                    CreatedByUserName = userName
                });
            }
            else
            {
                _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                {
                    EntryDate = invoice.InvoiceDate,
                    SourceType = JournalSourceType,
                    SourceId = invoice.Id,
                    DocumentNo = invoice.Number,
                    AccountCode = AccountInventoryCode,
                    AccountName = AccountInventoryName,
                    Debit = totalCost,
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
                    AccountCode = AccountCogsCode,
                    AccountName = AccountCogsName,
                    Debit = 0m,
                    Credit = totalCost,
                    Note = invoice.Note,
                    CreatedByUserId = userId,
                    CreatedByUserName = userName
                });
            }
        }

        private async Task RemoveFinancialEntriesAsync(Guid invoiceId)
        {
            var existing = await _context.Set<FinJournalEntry>()
                .Where(e => e.SourceType == JournalSourceType && e.SourceId == invoiceId)
                .ToListAsync();

            if (existing.Count > 0)
                _context.Set<FinJournalEntry>().RemoveRange(existing);
        }

        private static (string DebitCode, string DebitName, string CreditCode, string CreditName, decimal DebitAmount, decimal CreditAmount)
            ResolveSalesEntry(string? paymentMethod, decimal signedAmount)
        {
            var absoluteAmount = Math.Abs(signedAmount);
            if (absoluteAmount == 0m)
                return (AccountCashCode, AccountCashName, AccountSalesRevenueCode, AccountSalesRevenueName, 0m, 0m);

            var (receivableCode, receivableName) = ResolveSalesDebitAccount(paymentMethod);

            if (signedAmount > 0)
            {
                return (receivableCode, receivableName, AccountSalesRevenueCode, AccountSalesRevenueName, absoluteAmount, absoluteAmount);
            }

            return (AccountSalesRevenueCode, AccountSalesRevenueName, receivableCode, receivableName, absoluteAmount, absoluteAmount);
        }

        private static (string Code, string Name) ResolveSalesDebitAccount(string? paymentMethod)
        {
            if (string.Equals(paymentMethod, "Credit", StringComparison.OrdinalIgnoreCase))
                return (AccountCustomerCode, AccountCustomerName);

            if (string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase))
                return (AccountBankCode, AccountBankName);

            return (AccountCashCode, AccountCashName);
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsSupportedCurrency(string? currencyCode)
        {
            return string.Equals(currencyCode, "EUR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currencyCode, "LYD", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeCurrency(string? currencyCode)
        {
            return string.Equals(currencyCode, "LYD", StringComparison.OrdinalIgnoreCase) ? "LYD" : "EUR";
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            var normalized = (paymentMethod ?? string.Empty).Trim();
            if (string.Equals(normalized, "Card", StringComparison.OrdinalIgnoreCase))
                return "Card";
            if (string.Equals(normalized, "Transfer", StringComparison.OrdinalIgnoreCase))
                return "Transfer";
            if (string.Equals(normalized, "Credit", StringComparison.OrdinalIgnoreCase))
                return "Credit";
            return "Cash";
        }

        public async Task<(int Processed, int Skipped)> BackfillMissingCogsEntriesAsync(string? userId, string? userName)
        {
            var invoices = await _context.Set<SalesInvoice>()
                .Where(i => i.Status == StatusPosted)
                .Select(i => new { i.Id, i.TotalDinar, i.InvoiceDate, i.Number, i.Note })
                .ToListAsync();

            var processed = 0;
            var skipped = 0;

            foreach (var inv in invoices)
            {
                var hasCogs = await _context.Set<FinJournalEntry>()
                    .AnyAsync(e => e.SourceType == JournalSourceType
                                   && e.SourceId == inv.Id
                                   && e.AccountCode == AccountCogsCode);

                if (hasCogs)
                {
                    skipped++;
                    continue;
                }

                var cost = RoundMoney(await _context.Set<SalesLine>()
                    .Where(l => l.SalesInvoiceId == inv.Id)
                    .SumAsync(l => Math.Abs(l.LineCostDinar)));

                if (cost == 0m)
                {
                    skipped++;
                    continue;
                }

                var isSale = inv.TotalDinar >= 0m;

                if (isSale)
                {
                    _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                    {
                        EntryDate = inv.InvoiceDate,
                        SourceType = JournalSourceType,
                        SourceId = inv.Id,
                        DocumentNo = inv.Number,
                        AccountCode = AccountCogsCode,
                        AccountName = AccountCogsName,
                        Debit = cost,
                        Credit = 0m,
                        Note = inv.Note,
                        CreatedByUserId = userId,
                        CreatedByUserName = userName
                    });

                    _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                    {
                        EntryDate = inv.InvoiceDate,
                        SourceType = JournalSourceType,
                        SourceId = inv.Id,
                        DocumentNo = inv.Number,
                        AccountCode = AccountInventoryCode,
                        AccountName = AccountInventoryName,
                        Debit = 0m,
                        Credit = cost,
                        Note = inv.Note,
                        CreatedByUserId = userId,
                        CreatedByUserName = userName
                    });
                }
                else
                {
                    _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                    {
                        EntryDate = inv.InvoiceDate,
                        SourceType = JournalSourceType,
                        SourceId = inv.Id,
                        DocumentNo = inv.Number,
                        AccountCode = AccountInventoryCode,
                        AccountName = AccountInventoryName,
                        Debit = cost,
                        Credit = 0m,
                        Note = inv.Note,
                        CreatedByUserId = userId,
                        CreatedByUserName = userName
                    });

                    _context.Set<FinJournalEntry>().Add(new FinJournalEntry
                    {
                        EntryDate = inv.InvoiceDate,
                        SourceType = JournalSourceType,
                        SourceId = inv.Id,
                        DocumentNo = inv.Number,
                        AccountCode = AccountCogsCode,
                        AccountName = AccountCogsName,
                        Debit = 0m,
                        Credit = cost,
                        Note = inv.Note,
                        CreatedByUserId = userId,
                        CreatedByUserName = userName
                    });
                }

                processed++;
            }

            if (processed > 0)
                await _context.SaveChangesAsync();

            return (processed, skipped);
        }
    }
}
