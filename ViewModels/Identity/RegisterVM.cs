using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels.Identity
{
    public class RegisterVM
    {
        [DataType(DataType.EmailAddress)]
        [Remote(action: "IsEmailInUse", controller: "Account")]
        public required string Email { get; set; }

        [DataType(DataType.Password)]
        public required string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not mutch")]
        public required string ConfirmPassword { get; set; }
       
    }
}
