using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class AuditLog : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        public Guid? EntityId { get; set; }

        [StringLength(50)]
        public string? EntityNumber { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        [StringLength(256)]
        public string? CreatedByUserName { get; set; }
    }
}
