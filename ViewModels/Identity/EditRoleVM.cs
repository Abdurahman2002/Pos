using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class EditRoleVM
    {
        public required string Id { get; set; }

        [Required(ErrorMessage = "Role name is required.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Role name must be between 3 and 50 characters.")]
        [Display(Name = "Role Name")]
        public required string RoleName { get; set; }

    }
}
