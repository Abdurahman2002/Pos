using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class InvStockLedger : BaseEntity
    {
        [Required]
        public Guid ItemId { get; set; }

        [Required]
        [StringLength(30)]
        public string MovementType { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string ReferenceType { get; set; } = string.Empty;

        [Required]
        public Guid ReferenceId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityChange { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCostLyd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ValueChangeLyd { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public Item? Item { get; set; }
    }
}
