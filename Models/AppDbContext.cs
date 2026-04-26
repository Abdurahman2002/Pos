using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models
{
    //public class ApplicationDbContext : DbContext
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IServiceProvider _serviceProvider;

        public AppDbContext(DbContextOptions<AppDbContext> options
                                    , IServiceProvider serviceProvider) : base(options)
        {
            _serviceProvider = serviceProvider;
        }


        public DbSet<Contact> Contact { get; set; }
        public DbSet<Section> Sections { get; set; } //News يمكن الغاءه لأنه مضمن في
        public DbSet<News> News { get; set; }
        public DbSet<SiteInfo> SiteInfo { get; set; }
        public DbSet<SiteState> SiteState { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<BarcodeMapping> BarcodeMappings { get; set; }
        public DbSet<InventorySettings> InventorySettings { get; set; }
        public DbSet<InvStockBalance> InvStockBalances { get; set; }
        public DbSet<InvStockLedger> InvStockLedgers { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseLine> PurchaseLines { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesLine> SalesLines { get; set; }
        public DbSet<SalesInvoiceDraft> SalesInvoiceDrafts { get; set; }
        public DbSet<PosShift> PosShifts { get; set; }
        public DbSet<CustomerReceipt> CustomerReceipts { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }
        public DbSet<ExpenseEntry> ExpenseEntries { get; set; }
        public DbSet<FinJournalEntry> FinJournalEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<Contact>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Section>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<News>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<SiteInfo>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<SiteState>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            //---------------------------------------------------------------------------------
            modelBuilder.Entity<Employee>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            //---------------------------------------------------------------------------------
            modelBuilder.Entity<Category>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Customer>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Supplier>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Warehouse>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<Item>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<BarcodeMapping>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<InventorySettings>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<InvStockBalance>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<InvStockLedger>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<PurchaseInvoice>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<PurchaseLine>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<SalesInvoice>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<SalesLine>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<SalesInvoiceDraft>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<PosShift>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<CustomerReceipt>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<SupplierPayment>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<ExpenseEntry>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            modelBuilder.Entity<FinJournalEntry>().Property(x => x.Id).HasDefaultValueSql("NEWID()");
            //---------------------------------------------------------------------------------
            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<Customer>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<Supplier>().HasIndex(s => s.Name).IsUnique();
            modelBuilder.Entity<Item>().HasIndex(i => i.Name).IsUnique();
            modelBuilder.Entity<Item>().HasIndex(i => i.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            modelBuilder.Entity<Warehouse>().HasIndex(w => w.Name).IsUnique();
            modelBuilder.Entity<BarcodeMapping>().HasIndex(b => new { b.Code, b.CodeType }).IsUnique();
            modelBuilder.Entity<InvStockBalance>().HasIndex(b => b.ItemId).IsUnique();
            modelBuilder.Entity<PurchaseInvoice>().HasIndex(i => i.Number).IsUnique();
            modelBuilder.Entity<SalesInvoice>().HasIndex(i => i.Number).IsUnique();
            modelBuilder.Entity<SalesInvoiceDraft>().HasIndex(d => d.CreatedByUserId);
            modelBuilder.Entity<PosShift>().HasIndex(s => new { s.OpenedByUserId, s.Status });
            modelBuilder.Entity<CustomerReceipt>().HasIndex(r => new { r.CustomerId, r.ReceiptDate });
            modelBuilder.Entity<SupplierPayment>().HasIndex(r => new { r.SupplierId, r.PaymentDate });
            modelBuilder.Entity<SupplierPayment>().HasIndex(r => r.Number).IsUnique();
            modelBuilder.Entity<ExpenseEntry>().HasIndex(e => new { e.ExpenseDate, e.ExpenseKind });
            modelBuilder.Entity<ExpenseEntry>().HasIndex(e => new { e.EmployeeId, e.ExpenseDate });
            modelBuilder.Entity<FinJournalEntry>().HasIndex(e => new { e.SourceType, e.SourceId });
            modelBuilder.Entity<FinJournalEntry>().HasIndex(e => new { e.EntryDate, e.AccountCode });
            modelBuilder.Entity<InventorySettings>().Property(s => s.MaxCashierDiscountPercent).HasPrecision(18, 2);
            modelBuilder.Entity<PosShift>().Property(s => s.OpeningCashLyd).HasPrecision(18, 2);
            modelBuilder.Entity<PosShift>().Property(s => s.ClosingCashLyd).HasPrecision(18, 2);
            modelBuilder.Entity<InvStockBalance>().Property(b => b.RowVersion).IsRowVersion();
            modelBuilder.Entity<InvStockBalance>().ToTable(tb =>
                tb.HasCheckConstraint("CK_InvStockBalance_QtyOnHand_NonNegative", "[QuantityOnHand] >= 0"));

            modelBuilder.Entity<Item>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<InvStockBalance>().HasQueryFilter(b => !b.Item!.IsDeleted);
            modelBuilder.Entity<InvStockLedger>().HasQueryFilter(l => !l.Item!.IsDeleted);
            modelBuilder.Entity<PurchaseLine>().HasQueryFilter(l => !l.Item!.IsDeleted);
            modelBuilder.Entity<SalesLine>().HasQueryFilter(l => !l.Item!.IsDeleted);
            modelBuilder.Entity<BarcodeMapping>().HasQueryFilter(m => !m.Item!.IsDeleted);

            modelBuilder.Entity<InvStockBalance>()
                .HasOne(b => b.Item)
                .WithMany()
                .HasForeignKey(b => b.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvStockLedger>()
                .HasOne(l => l.Item)
                .WithMany()
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseLine>()
                .HasOne(l => l.PurchaseInvoice)
                .WithMany(h => h.Lines)
                .HasForeignKey(l => l.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseLine>()
                .HasOne(l => l.Item)
                .WithMany()
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(i => i.Supplier)
                .WithMany(s => s.PurchaseInvoices)
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesLine>()
                .HasOne(l => l.SalesInvoice)
                .WithMany(h => h.Lines)
                .HasForeignKey(l => l.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalesLine>()
                .HasOne(l => l.Item)
                .WithMany()
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesInvoice>()
                .HasOne(i => i.Customer)
                .WithMany(c => c.SalesInvoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesInvoice>()
                .HasOne(i => i.PosShift)
                .WithMany(s => s.SalesInvoices)
                .HasForeignKey(i => i.PosShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SalesInvoiceDraft>()
                .HasOne<Customer>()
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CustomerReceipt>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerReceipt>()
                .HasOne(r => r.PosShift)
                .WithMany()
                .HasForeignKey(r => r.PosShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SupplierPayment>()
                .HasOne(r => r.Supplier)
                .WithMany(s => s.Payments)
                .HasForeignKey(r => r.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExpenseEntry>()
                .HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Seed();

            modelBuilder.Entity<ApplicationUser>()
                        .HasOne(u => u.Employee)
                        .WithOne(e => e.ApplicationUser)
                        .HasForeignKey<Employee>(e => e.UserId)
                        .OnDelete(DeleteBehavior.Cascade);

         
        }

        public override int SaveChanges()
        {
            ApplyUtcDateTimes();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyUtcDateTimes();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyUtcDateTimes();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyUtcDateTimes();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyUtcDateTimes()
        {
            foreach (var entry in ChangeTracker.Entries().Where(e =>
                         e.State == EntityState.Added ||
                         e.State == EntityState.Modified))
            {
                if (entry.Entity is BaseEntity baseEntity)
                {
                    if (entry.State == EntityState.Added)
                        baseEntity.Created = ToUtc(baseEntity.Created == default ? DateTime.UtcNow : baseEntity.Created);

                    if (entry.State == EntityState.Modified)
                        baseEntity.Modified = DateTime.UtcNow;
                }

                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.ClrType == typeof(DateTime) && property.CurrentValue is DateTime dt)
                    {
                        property.CurrentValue = ToUtc(dt);
                    }
                    else if (property.Metadata.ClrType == typeof(DateTime?) && property.CurrentValue is DateTime nullableDt)
                    {
                        property.CurrentValue = ToUtc(nullableDt);
                    }
                }
            }
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                _ => value
            };
        }
}

}
