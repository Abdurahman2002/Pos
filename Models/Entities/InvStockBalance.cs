using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class InvStockBalance : BaseEntity
    {
        [Required]
        public Guid ItemId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityOnHand { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public Item? Item { get; set; }
    }
}
