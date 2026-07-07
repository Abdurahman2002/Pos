using NewsApp2.ViewModels.Common;

namespace NewsApp2.ViewModels.Inventory
{
    public class StockMovementRowVM
    {
        public DateTime Created { get; set; }
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public Guid ReferenceId { get; set; }
        public decimal QuantityChange { get; set; }
        public decimal BalanceAfter { get; set; }
        public string? Note { get; set; }
    }

    public class StockMovementReportVM
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public Guid? ItemId { get; set; }
        public string? ReferenceType { get; set; }
        public decimal TotalIn { get; set; }
        public decimal TotalOut { get; set; }
        public List<StockMovementRowVM> Rows { get; set; } = new();
        public PaginationVM? Pagination { get; set; }
    }
}
