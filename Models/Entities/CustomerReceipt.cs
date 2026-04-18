using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class CustomerReceipt : BaseEntity
    {
        public Guid CustomerId { get; set; }

        public DateOnly ReceiptDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

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

        public Guid? PosShiftId { get; set; }

        public Customer? Customer { get; set; }
        public PosShift? PosShift { get; set; }
    }
}
