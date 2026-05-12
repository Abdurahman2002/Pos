using System;

namespace NewsApp2.ViewModels.Inventory
{
    public class ItemCardRowVM
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public Guid ReferenceId { get; set; }
        public string Reference { get; set; } = string.Empty;

        public decimal QtyIn { get; set; }
        public decimal QtyOut { get; set; }

        public decimal UnitCost { get; set; }
        public decimal LineValue { get; set; }

        public decimal RunningQty { get; set; }
        public decimal RunningAvgCost { get; set; }
        public decimal RunningValue { get; set; }
    }
}
