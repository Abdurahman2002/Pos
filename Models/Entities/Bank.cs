using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Models.Entities
{
    public class Bank : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Note { get; set; }
    }
}
