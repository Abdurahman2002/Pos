using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class ChangePasswordVM
    {
        [DataType(DataType.Password)]
        public required string CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public required string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "New Password and confirm password must match")]
        public required string ConfirmPassword { get; set; }

    }
}
