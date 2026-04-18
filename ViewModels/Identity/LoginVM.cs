using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class LoginVM
    {
        [EmailAddress]
        public required string Email { get; set; }

        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

    }
}
