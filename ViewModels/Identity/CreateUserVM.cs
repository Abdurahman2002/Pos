using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class CreateUserVM
    {
        [DataType(DataType.EmailAddress)]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        public required string Email { get; set; }

        [ValidateNever]
        public required List<IdentityRole> Roles { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Roles")]
        public string RoleId { get; set; }

    }
}
