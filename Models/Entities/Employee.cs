using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewsApp2.Models.Entities
{
    public class Employee : BaseEntity
    {
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [ValidateNever]
        public ApplicationUser? ApplicationUser { get; set; }

        [ForeignKey("ApplicationUser")]
        public string? UserId { get; set; }

        public Guid? WarehouseId { get; set; }

        [ValidateNever]
        public Warehouse? Warehouse { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
        public decimal BaseSalaryLyd { get; set; }

    }
}
