using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class ForgotPasswordVM
    {
        [EmailAddress]
        public required string Email { get; set; }

    }
}
