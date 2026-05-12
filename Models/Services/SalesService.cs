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
                throw new InvalidOperationException("Sales invoice requires at least one line.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("Maximum allowed lines per invoice is 200.");

            if (!IsSupportedCurrency(invoice.CurrencyCode))
                throw new InvalidOperationException("Only EUR or LYD is allowed.");

            invoice.CurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
            if (invoice.CurrencyCode == "LYD")
                invoice.EurToDinarRateSnapshot = 1m;
            else if (invoice.EurToDinarRateSnapshot <= 0)
                throw new InvalidOperationException("Rate must be greater than zero.");
            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = StatusPosted;
            invoice.SubmittedAt ??= DateTime.UtcNow;
            invoice.ApprovedAt ??= DateTime.UtcNow;
            invoice.ApprovedByUserName ??= invoice.CreatedByUserName;

            _logger.LogInformation("Creating sales invoice for user {User} with {LineCount} lines.", invoice.CreatedByUserName, lineList.Count);

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
                    throw new InvalidOperationException("Quantity must be greater than zero.");

                if (line.Qty != decimal.Truncate(line.Qty))
                    throw new InvalidOperationException("Quantity must be a whole number.");

                if (line.UnitPriceEur < 0)
                    throw new InvalidOperationException("Unit price cannot be negative.");

                if (!itemNames.ContainsKey(line.ItemId))
                    throw new InvalidOperationException("Selected item was not found.");

                var stock = await _context.Set<InvStockBalance>()
                    .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                var available = stock?.QuantityOnHand ?? 0m;
                if (available < line.Qty)
                {
                    var itemName = itemNames.TryGetValue(line.ItemId, out var name) ? name : "item";
                    throw new InvalidOperationException(
                        $"Insufficient stock for {itemName}. Requested {FormatQuantity(line.Qty)}, available {FormatQuantity(available)}.");
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
                // compute COGS from current moving average
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
                    UnitCostLyd = unitCost
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
        }

        public async Task<Guid> CreateReturnAsync(SalesInvoice invoice, IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> lines)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("Return invoice requires at least one line.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("Maximum allowed lines per invoice is 200.");

            if (!IsSupportedCurrency(invoice.CurrencyCode))
                throw new InvalidOperationException("Only EUR or LYD is allowed.");

            invoice.CurrencyCode = NormalizeCurrency(invoice.CurrencyCode);
            if (invoice.CurrencyCode == "LYD")
                invoice.EurToDinarRateSnapshot = 1m;
            else if (invoice.EurToDinarRateSnapshot <= 0)
                throw new InvalidOperationException("Rate must be greater than zero.");
            invoice.Number = string.IsNullOrWhiteSpace(invoice.Number)
                ? await GenerateReturnNumberAsync(invoice.InvoiceDate)
                : invoice.Number.Trim();
            invoice.Status = StatusPosted;
            invoice.SubmittedAt ??= DateTime.UtcNow;
            invoice.ApprovedAt ??= DateTime.UtcNow;
            invoice.ApprovedByUserName ??= invoice.CreatedByUserName;

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
                    throw new InvalidOperationException("Quantity must be greater than zero.");

                if (line.Qty != decimal.Truncate(line.Qty))
                    throw new InvalidOperationException("Quantity must be a whole number.");

                if (line.UnitPriceEur < 0)
                    throw new InvalidOperationException("Unit price cannot be negative.");

                if (!itemNames.ContainsKey(line.ItemId))
                    throw new InvalidOperationException("Selected item was not found.");

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
                    UnitCostLyd = unitCost
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
        }

        public async Task CancelAsync(Guid invoiceId, string? cancelledBy)
        {
            var invoice = await _context.Set<SalesInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Invoice not found.");

            if (!string.Equals(invoice.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only posted invoices can be cancelled.");

            var lines = invoice.Lines?.ToList() ?? new List<SalesLine>();
            if (!lines.Any())
                throw new InvalidOperationException("Invoice has no lines.");

            // For return invoices (negative qty lines), cancelling restores negative qty to stock.
            // Validate that no line would push stock below zero before entering the transaction.
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
                    UnitCostLyd = line.UnitCostLyd
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
        }

        public async Task<Guid> CreatePendingAsync(SalesInvoice invoice, IEnumerable<(Guid ItemId, decimal Qty)> lines)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("Sales invoice requires at least one line.");

            if (lineList.Count > 200)
                throw new InvalidOperationException("Maximum allowed lines per invoice is 200.");

            if (!IsSupportedCurrency(invoice.CurrencyCode))
                throw new InvalidOperationException("Only EUR or LYD is allowed.");

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
                    throw new InvalidOperationException("Quantity must be greater than zero.");

                if (!itemExists.Contains(line.ItemId))
                    throw new InvalidOperationException("Selected item was not found.");

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
                throw new InvalidOperationException("Line prices are required.");

            if (rate <= 0)
                throw new InvalidOperationException("Rate must be greater than zero.");

            var customerExists = await _context.Set<Customer>().AsNoTracking().AnyAsync(c => c.Id == customerId);
            if (!customerExists)
                throw new InvalidOperationException("Customer not found.");

            var invoice = await _context.Set<SalesInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Invoice not found.");

            if (!string.Equals(invoice.Status, StatusPendingApproval, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only pending sales invoices can be approved.");

            var lines = invoice.Lines?.ToList() ?? new List<SalesLine>();
            if (!lines.Any())
                throw new InvalidOperationException("Invoice has no lines.");

            foreach (var line in lines)
            {
                if (!priceMap.ContainsKey(line.Id))
                    throw new InvalidOperationException("Missing price for one or more lines.");
            }

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
                    throw new InvalidOperationException("Unit price cannot be negative.");

                var stock = await _context.Set<InvStockBalance>()
                    .FirstOrDefaultAsync(s => s.ItemId == line.ItemId);

                var available = stock?.QuantityOnHand ?? 0m;
                if (available < line.Qty)
                {
                    var itemName = itemNames.TryGetValue(line.ItemId, out var name) ? name : "item";
                    throw new InvalidOperationException(
                        $"Insufficient stock for {itemName}. Requested {FormatQuantity(line.Qty)}, available {FormatQuantity(available)}.");
                }

                var lineTotalEur = RoundMoney(line.Qty * unitPrice);
                var lineTotalDinar = RoundMoney(lineTotalEur * rate);

                line.UnitPriceEur = unitPrice;
                line.LineTotalEur = lineTotalEur;
                line.LineTotalDinar = lineTotalDinar;
                line.ExchangeRateSnapshot = rate;

                // record COGS from current average
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
                    UnitCostLyd = unitCost
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
        }

        public async Task UpdatePostedAsync(
            Guid invoiceId,
            DateOnly invoiceDate,
            Guid? customerId,
            decimal rate,
            string? note,
            IEnumerable<(Guid ItemId, decimal Qty, decimal UnitPriceEur)> lines,
            string? editedBy)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("Sales invoice requires at least one line.");

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
            }

            if (customerId.HasValue)
            {
                var customerExists = await _context.Set<Customer>()
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == customerId.Value);
                if (!customerExists)
                    throw new InvalidOperationException("Customer not found.");
            }

            var invoice = await _context.Set<SalesInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new InvalidOperationException("Invoice not found.");

            if (!string.Equals(invoice.Status, StatusPosted, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only posted sales invoices can be edited.");

            if (string.Equals(invoice.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase)
                && !customerId.HasValue)
                throw new InvalidOperationException("Customer is required when editing a credit sales invoice.");

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
                    throw new InvalidOperationException("Selected item was not found.");
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
                    var itemName = itemNames.TryGetValue(itemId, out var name) ? name : "item";
                    throw new InvalidOperationException(
                        $"Insufficient stock for {itemName}. Requested {FormatQuantity(newQty)}, available after edit {FormatQuantity(stock.QuantityOnHand + oldQty)}.");
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
                    UnitCostLyd = stock.AverageCostLyd
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
            if (amount == 0m)
                return;

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
    }
}
