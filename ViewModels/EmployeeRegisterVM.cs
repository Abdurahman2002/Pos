using Microsoft.AspNetCore.Mvc;
using NewsApp2.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace NewsApp2.ViewModels
{
    public class EmployeeRegisterVM
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

        public Employee Employee { get; set; }

    }
}
