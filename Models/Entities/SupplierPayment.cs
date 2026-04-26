using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class SupplierPayment : BaseEntity
    {
        [Required]
        public Guid SupplierId { get; set; }

        [StringLength(50)]
        public string Number { get; set; } = string.Empty;

        public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal Amount { get; set; }

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(500)]
        public string? Note { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }

        public Supplier? Supplier { get; set; }
    }
}
