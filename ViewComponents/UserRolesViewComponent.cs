
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Identity;

namespace NewsApp2.ViewComponents
{
    [ViewComponent(Name = "UserRoles")] // زائدة
    public class UserRolesViewComponent : ViewComponent
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserRolesViewComponent(RoleManager<IdentityRole> roleManager,
                               UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("NotFound");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"User with ID '{userId}' was not found.";
                return View("NotFound");
            }

            var roles = await _roleManager.Roles
                                          .OrderBy(r => r.Name)
                                          .ToListAsync();



            var model = new List<UserRolesVM>();

            foreach (var role in roles)
            {
                if (role.Name == "Prog" && !User.IsInRole("Prog"))
                    continue;


                model.Add(new UserRolesVM
                {
                    RoleId = role.Id,
                    RoleName = role.Name!,
                    IsSelected = await _userManager.IsInRoleAsync(user, role.Name!)
                });
            }

            ViewBag.UserId = userId;
            return View(model);

        }

    }
}