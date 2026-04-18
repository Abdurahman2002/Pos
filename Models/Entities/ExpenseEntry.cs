using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class ExpenseEntry : BaseEntity
    {
        public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal Amount { get; set; }

        [StringLength(20)]
        public string ExpenseKind { get; set; } = "General";

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        public Guid? EmployeeId { get; set; }

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(50)]
        public string? ReferenceNo { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }

        public Employee? Employee { get; set; }
    }
}