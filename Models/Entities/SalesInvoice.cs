using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class SalesInvoice : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Number { get; set; } = string.Empty;

        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "EUR";

        [Column(TypeName = "decimal(18,6)")]
        public decimal EurToDinarRateSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalEur { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDinar { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "PendingApproval";

        public Guid? CustomerId { get; set; }

        [StringLength(20)]
        public string PaymentMethod { get; set; } = "Cash";
        public Guid? BankId { get; set; }

        public Guid? PosShiftId { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(256)]
        public string? ApprovedByUserName { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }

        public Customer? Customer { get; set; }
        public Bank? Bank { get; set; }
        public PosShift? PosShift { get; set; }

        public ICollection<SalesLine>? Lines { get; set; }
    }
}
