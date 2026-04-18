using Microsoft.AspNetCore.Mvc;
using NewsApp2.Models.Entities;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NewsApp2.ViewModels
{
    public class CreateEmployeeVM
    {
        [DataType(DataType.EmailAddress)]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        public required string Email { get; set; }

        [Required]
        [Display(Name = "System Role")]
        public string RoleName { get; set; } = "SalesOfficer";

        public Employee Employee { get; set; } = new Employee();

        public List<SelectListItem> AvailableRoles { get; set; } = new();

    }
}
