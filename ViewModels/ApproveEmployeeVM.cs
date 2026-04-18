using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NewsApp2.ViewModels
{
    public class ApproveEmployeeVM
    {
        public Guid EmployeeId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "System Role")]
        public string RoleName { get; set; } = "SalesOfficer";

        public List<SelectListItem> AvailableRoles { get; set; } = new();
    }
}
