using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class FinJournalEntry : BaseEntity
    {
        public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        [Required]
        [StringLength(50)]
        public string SourceType { get; set; } = string.Empty;

        public Guid SourceId { get; set; }

        [StringLength(50)]
        public string? DocumentNo { get; set; }

        [Required]
        [StringLength(20)]
        public string AccountCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string AccountName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }
    }
}
