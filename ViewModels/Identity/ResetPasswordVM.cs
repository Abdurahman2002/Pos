using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class ResetPasswordVM
    {
        [EmailAddress]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("Password", ErrorMessage = "Password and confirm password must match")]
        public string ConfirmPassword { get; set; }

        public string Token { get; set; }
    }


}
