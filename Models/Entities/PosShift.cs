using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class PosShift : BaseEntity
    {
        [Required]
        [StringLength(450)]
        public string OpenedByUserId { get; set; } = string.Empty;

        [StringLength(200)]
        public string? OpenedByUserName { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal OpeningCashLyd { get; set; }

        public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ClosedAtUtc { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        public decimal? ClosingCashLyd { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Open";

        [StringLength(500)]
        public string? Note { get; set; }

        public ICollection<SalesInvoice>? SalesInvoices { get; set; }
    }
}
