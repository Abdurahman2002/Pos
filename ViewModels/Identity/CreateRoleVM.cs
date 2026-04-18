using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class CreateRoleVM
    {
        [Required(ErrorMessage = "Role name is required")]
        [StringLength(30, MinimumLength = 3)]
        public required string Name { get; set; }

    }
}
