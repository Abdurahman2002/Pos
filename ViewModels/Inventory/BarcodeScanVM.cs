using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Inventory
{
    public class BarcodeResolveRequestVM
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        public Guid? WarehouseId { get; set; }

        [StringLength(20)]
        public string? Mode { get; set; }
    }

    public class BarcodeMapRequestVM
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string CodeType { get; set; } = "Raw";

        [Required]
        public Guid ItemId { get; set; }
    }
}
