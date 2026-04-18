using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels.Identity;
using System.Data;

namespace NewsApp2.Controllers
{
    [Authorize(Roles = "Prog")]
    [ViewLayout("_LayoutDashboard")]
    public class RolesController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        //private readonly IWebHostEnvironment _host;


        public RolesController(RoleManager<IdentityRole> roleManager,
                               UserManager<ApplicationUser> userManager,
                               SignInManager<ApplicationUser> signInManager,
                                IEmailSender emailSender,
                                IWebHostEnvironment host) : base(host)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            //_host = host;
        }


      
        public async Task<IActionResult> Index(string roleInUse)
        {
            //var users = await _userManager.Users
                                     //.ToListAsync();
            //var allUsersCount = await _userManager.Users.CountAsync();
            //var usersInRole = await _userManager.GetUsersInRoleAsync("Admin");
            //var usersInRoleCount = (await _userManager.GetUsersInRoleAsync("Admin")).Count();


            ViewBag.RoleInUse = roleInUse;

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return View("NotFound");

            var roles = await _roleManager.Roles
                                          .OrderBy(r => r.Name)
                                          .ToListAsync();


            var isProg = await _userManager.IsInRoleAsync(user, "Prog");
            if (!isProg)
                roles = roles.Where(r => r.Name != "Prog").ToList();


            var rolesVM = new List<RoleVM>();

            foreach (var role in roles)
            {
                rolesVM.Add(new RoleVM
                {
                    Id = role.Id,
                    Name = role.Name,
                    UsersCount = (await _userManager.GetUsersInRoleAsync(role.Name)).Count
                });
            }

            return View(rolesVM);

        }


        [Authorize(Roles = "Prog")]
        public IActionResult CreateRole()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(CreateRoleVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var newRole = new IdentityRole
            {
                Name = model.Name,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

            var result = await _roleManager.CreateAsync(newRole);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Role '{model.Name}' created successfully";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            TempData["ErrorMessage"] = "Failed to create the role";
            return View(model);
        }



        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> EditRole(string id)
        {
            if (string.IsNullOrEmpty(id))
                return View("NotFound");

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                ViewBag.Message = $"Cannot find role with Id={id}";
                return View("NotFound");
            }

            var model = new EditRoleVM
            {
                Id = role.Id,
                RoleName = role.Name
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(EditRoleVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var role = await _roleManager.FindByIdAsync(model.Id);
            if (role == null)
            {
                ViewBag.Message = $"Cannot find role with Id={model.Id}";
                return View("NotFound");
            }

            role.Name = model.RoleName;
            var result = await _roleManager.UpdateAsync(role);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Role '{role.Name}' updated successfully";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            TempData["ErrorMessage"] = $"Failed to update role '{role.Name}'";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["ErrorMessage"] = "Invalid role id.";
                return RedirectToAction(nameof(Index));
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = $"Role with Id='{id}' not found.";
                return RedirectToAction(nameof(Index));
            }

            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete role '{role.Name}' because it is in use.";
                return RedirectToAction("Index", new { roleInUse = role.Name });
            }

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Role '{role.Name}' deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to delete role '{role.Name}'.";
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return RedirectToAction("Index");
        }


    }

}

