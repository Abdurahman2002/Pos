using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models.Services
{
    public sealed class PartyBalanceRow
    {
        public Guid PartyId { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public decimal InvoicedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public DateOnly? LastInvoiceDate { get; set; }
        public DateOnly? LastPaymentDate { get; set; }
    }

    public sealed class PartyAccountsSummary
    {
        public string? Search { get; set; }
        public decimal CustomerReceivables { get; set; }
        public decimal SupplierPayables { get; set; }
        public decimal NetPosition => CustomerReceivables - SupplierPayables;
        public List<PartyBalanceRow> TopCustomerBalances { get; set; } = new();
        public List<PartyBalanceRow> TopSupplierBalances { get; set; } = new();
    }

    public class AccountBalanceService
    {
        private static readonly Guid DailySalesCustomerSeedId = Guid.Parse("7e2efb6c-0cb2-430f-92af-6e0ad720f105");

        private readonly AppDbContext _context;

        public AccountBalanceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PartyAccountsSummary> GetSummaryAsync(string? search = null, int top = 10)
        {
            var customers = await GetCustomerBalancesAsync(search, top);
            var suppliers = await GetSupplierBalancesAsync(search, top);

            return new PartyAccountsSummary
            {
                Search = search,
                CustomerReceivables = await GetCustomerReceivablesTotalAsync(),
                SupplierPayables = await GetSupplierPayablesTotalAsync(),
                TopCustomerBalances = customers,
                TopSupplierBalances = suppliers
            };
        }

        public async Task<decimal> GetCustomerReceivablesTotalAsync()
        {
            var creditSales = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.CustomerId.HasValue && i.CustomerId.Value != DailySalesCustomerSeedId)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .GroupBy(i => i.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Amount = g.Sum(i => i.TotalDinar)
                })
                .ToListAsync();

            var customerIds = creditSales.Select(x => x.CustomerId).ToList();
            var receipts = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => customerIds.Contains(r.CustomerId))
                .GroupBy(r => r.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Amount = g.Sum(r => r.Amount)
                })
                .ToListAsync();

            var receiptsByCustomer = receipts.ToDictionary(x => x.CustomerId, x => x.Amount);
            return creditSales.Sum(x => Math.Max(0m, x.Amount - receiptsByCustomer.GetValueOrDefault(x.CustomerId)));
        }

        public async Task<decimal> GetSupplierPayablesTotalAsync()
        {
            var creditPurchases = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.SupplierId.HasValue)
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .GroupBy(i => i.SupplierId!.Value)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    Amount = g.Sum(i => i.TotalDinar)
                })
                .ToListAsync();

            var supplierIds = creditPurchases.Select(x => x.SupplierId).ToList();
            var payments = await _context.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(p => supplierIds.Contains(p.SupplierId))
                .GroupBy(p => p.SupplierId)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    Amount = g.Sum(p => p.Amount)
                })
                .ToListAsync();

            var paymentsBySupplier = payments.ToDictionary(x => x.SupplierId, x => x.Amount);
            return creditPurchases.Sum(x => Math.Max(0m, x.Amount - paymentsBySupplier.GetValueOrDefault(x.SupplierId)));
        }

        public async Task<List<PartyBalanceRow>> GetCustomerBalancesAsync(string? search = null, int? take = null)
        {
            var customersQuery = _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Id != DailySalesCustomerSeedId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                customersQuery = customersQuery.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
            }

            var customers = await customersQuery
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Phone })
                .ToListAsync();

            var customerIds = customers.Select(c => c.Id).ToList();
            var creditSales = await _context.Set<SalesInvoice>()
                .AsNoTracking()
                .Where(i => i.CustomerId.HasValue && customerIds.Contains(i.CustomerId.Value))
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .GroupBy(i => i.CustomerId!.Value)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Sum = g.Sum(i => i.TotalDinar),
                    LastDate = g.Max(i => i.InvoiceDate)
                })
                .ToListAsync();

            var receipts = await _context.Set<CustomerReceipt>()
                .AsNoTracking()
                .Where(r => customerIds.Contains(r.CustomerId))
                .GroupBy(r => r.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    Sum = g.Sum(r => r.Amount),
                    LastDate = g.Max(r => r.ReceiptDate)
                })
                .ToListAsync();

            var salesByCustomer = creditSales.ToDictionary(x => x.CustomerId, x => (x.Sum, x.LastDate));
            var receiptsByCustomer = receipts.ToDictionary(x => x.CustomerId, x => (x.Sum, x.LastDate));

            var rows = customers
                .Select(c =>
                {
                    var sales = salesByCustomer.TryGetValue(c.Id, out var s) ? s.Sum : 0m;
                    var paid = receiptsByCustomer.TryGetValue(c.Id, out var r) ? r.Sum : 0m;
                    return new PartyBalanceRow
                    {
                        PartyId = c.Id,
                        PartyName = c.Name,
                        Phone = c.Phone,
                        InvoicedAmount = sales,
                        PaidAmount = paid,
                        BalanceAmount = Math.Max(0m, sales - paid),
                        LastInvoiceDate = salesByCustomer.TryGetValue(c.Id, out var sx) ? sx.LastDate : null,
                        LastPaymentDate = receiptsByCustomer.TryGetValue(c.Id, out var rx) ? rx.LastDate : null
                    };
                })
                .Where(r => r.BalanceAmount > 0)
                .OrderByDescending(r => r.BalanceAmount)
                .ThenBy(r => r.PartyName)
                .ToList();

            return take.HasValue ? rows.Take(take.Value).ToList() : rows;
        }

        public async Task<List<PartyBalanceRow>> GetSupplierBalancesAsync(string? search = null, int? take = null)
        {
            var suppliersQuery = _context.Set<Supplier>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                suppliersQuery = suppliersQuery.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
            }

            var suppliers = await suppliersQuery
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.Phone })
                .ToListAsync();

            var supplierIds = suppliers.Select(s => s.Id).ToList();
            var creditPurchases = await _context.Set<PurchaseInvoice>()
                .AsNoTracking()
                .Where(i => i.SupplierId.HasValue && supplierIds.Contains(i.SupplierId.Value))
                .Where(i => i.Status == "Posted")
                .Where(i => i.PaymentMethod == "Credit")
                .GroupBy(i => i.SupplierId!.Value)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    Sum = g.Sum(i => i.TotalDinar),
                    LastDate = g.Max(i => i.InvoiceDate)
                })
                .ToListAsync();

            var payments = await _context.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(r => supplierIds.Contains(r.SupplierId))
                .GroupBy(r => r.SupplierId)
                .Select(g => new
                {
                    SupplierId = g.Key,
                    Sum = g.Sum(r => r.Amount),
                    LastDate = g.Max(r => r.PaymentDate)
                })
                .ToListAsync();

            var purchasesBySupplier = creditPurchases.ToDictionary(x => x.SupplierId, x => (x.Sum, x.LastDate));
            var paymentsBySupplier = payments.ToDictionary(x => x.SupplierId, x => (x.Sum, x.LastDate));

            var rows = suppliers
                .Select(s =>
                {
                    var purchases = purchasesBySupplier.TryGetValue(s.Id, out var p) ? p.Sum : 0m;
                    var paid = paymentsBySupplier.TryGetValue(s.Id, out var r) ? r.Sum : 0m;
                    return new PartyBalanceRow
                    {
                        PartyId = s.Id,
                        PartyName = s.Name,
                        Phone = s.Phone,
                        InvoicedAmount = purchases,
                        PaidAmount = paid,
                        BalanceAmount = Math.Max(0m, purchases - paid),
                        LastInvoiceDate = purchasesBySupplier.TryGetValue(s.Id, out var px) ? px.LastDate : null,
                        LastPaymentDate = paymentsBySupplier.TryGetValue(s.Id, out var rx) ? rx.LastDate : null
                    };
                })
                .Where(r => r.BalanceAmount > 0)
                .OrderByDescending(r => r.BalanceAmount)
                .ThenBy(r => r.PartyName)
                .ToList();

            return take.HasValue ? rows.Take(take.Value).ToList() : rows;
        }
    }
}
