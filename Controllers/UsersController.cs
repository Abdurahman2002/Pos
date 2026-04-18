using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels.Identity;
using PasswordGenerator;
using System.Security.Claims;

namespace NewsApp2.Controllers
{
    [Authorize(Roles = "Prog")]

    [ViewLayout("_LayoutDashboard")]
    public class UsersController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        // private readonly IUnitOfWork<Employee> _employee;
        private readonly IEmailSender _emailSender;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _host;


        public UsersController(RoleManager<IdentityRole> roleManager,
                               UserManager<ApplicationUser> userManager,
                               SignInManager<ApplicationUser> signInManager,
                               // IUnitOfWork<Employee> employee,
                               IEmailSender emailSender,
                               IServiceProvider serviceProvider,
                               IWebHostEnvironment host) : base(host)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _signInManager = signInManager;
            // _employee = employee;
            _emailSender = emailSender;
            _serviceProvider = serviceProvider;
            _host = host;
        }




        public async Task<IActionResult> Index(string userList)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return View("NotFound");


            var query = _userManager.Users
                                    .AsNoTracking()
                                    .Where(u => u.Employee != null);


            bool currentIsProg = await _userManager.IsInRoleAsync(currentUser, "Prog");
            if (!currentIsProg)
            {
                var progUsers = await _userManager.GetUsersInRoleAsync("Prog");
                var progIds = progUsers.Select(u => u.Id).ToHashSet();

                query = query.Where(u => !progIds.Contains(u.Id));
            }

            query = query.OrderByDescending(u => u.CreatedDate);

            var users = await query.ToListAsync();



            ViewBag.AllUsers = users.Count();
            ViewBag.Confirmed = users.Where(u => u.EmailConfirmed == true).Count();
            ViewBag.Unconfirmed = users.Where(u => u.EmailConfirmed == false).Count();
            ViewBag.Locked = users.Where(u => u.LockoutEnd > DateTime.UtcNow).Count();
            ViewBag.UserList = userList;


            return userList switch
            {
                "Confirmed" => View(users.Where(u => u.EmailConfirmed)),
                "Unconfirmed" => View(users.Where(u => !u.EmailConfirmed)),
                "Locked" => View(users.Where(u => u.LockoutEnd > DateTime.UtcNow)),
                _ => View(users)
            };

            {
                //if (userList == "Confirmed")
                //{
                //    return View(users.Where(u => u.EmailConfirmed == true));
                //}
                //else if (userList == "Unconfirmed")
                //{
                //    return View(users.Where(u => u.EmailConfirmed == false));
                //}
                //else if (userList == "Locked")
                //{
                //    return View(users.Where(u => u.LockoutEnd > DateTime.UtcNow));
                //}
                //else
                //{
                //    return View(users);
                //}
            }

        }



        //[Authorize(Policy = "EditUserPolicy")]
        public async Task<IActionResult> EditUser(string id)
        {

            if (string.IsNullOrWhiteSpace(id))
            {
                ViewBag.ErrorMessage = "Invalid user id";
                return View("NotFound");
            }


            {
            }



            var user = await _userManager.Users
                                         .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                ViewBag.ErrorMessage = $"User with Id={id} was not found.";
                return View("NotFound");
            }

            ViewBag.userId = user.Id; //ViewComponent لاستعمالها في

            var userRoles = await _userManager.GetRolesAsync(user);
            var userClaims = await _userManager.GetClaimsAsync(user);



            var editUserVM = new EditUserVM
            {
                Id = user.Id,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                LastAccessTime = user.LastAccessTime,

                LockoutEnd = user.LockoutEnd,

                CreatedDate = user.CreatedDate,
                ModifiedDate = user.ModifiedDate,

                Claims = userClaims.Select(c => c.Value)?.ToList() ?? new List<string>(),
                Roles = userRoles?.ToList() ?? new List<string>(),
            };

            return View(editUserVM);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "EditUserPolicy")]
        public async Task<IActionResult> EditUser(EditUserVM editUserVM)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill all required fields correctly.";
                return RedirectToAction("EditUser", new { id = editUserVM.Id });
            }

            var user = await _userManager.FindByIdAsync(editUserVM.Id);

            if (user == null)
            {
                TempData["ErrorMessage"] = $"User with Id {editUserVM.Id} was not found.";
                return RedirectToAction("Users");
            }

            try
            {
                user.Email = editUserVM.Email;
                user.UserName = editUserVM.Email;
                user.ModifiedDate = DateTime.UtcNow;

                IdentityResult result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join("<br>", result.Errors.Select(e => e.Description));
                    return RedirectToAction("EditUser", new { id = editUserVM.Id });
                }

                TempData["SuccessMessage"] = "User information updated successfully";
                return RedirectToAction("EditUser", new { id = editUserVM.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred: " + ex.Message;
                return RedirectToAction("EditUser", new { id = editUserVM.Id });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
       [Authorize(Policy = "EditUserPolicy")]
        public async Task<IActionResult> LockoutUser(string id)
        {

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = $"User with Id={id} was not found.";
                return View("NotFound");
            }

            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot lockout the currently logged-in user.";
                return RedirectToAction(nameof(EditUser), new { id = user.Id });
            }

            try
            {
                IdentityResult result;

                bool isCurrentlyLocked =
                    user.LockoutEnd.HasValue &&
                    user.LockoutEnd.Value > DateTimeOffset.UtcNow;

                //isCurrentlyLocked=false سترجع user.LockoutEnd.HasValue=null اذا كانت


                if (isCurrentlyLocked) // في حال المستخدم موقوف
                {
                    result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow); // UNLOCK
                }
                else // في حال المستخدم غير موقوف
                {
                    var lockoutEndDate = DateTimeOffset.UtcNow.AddYears(10);
                    result = await _userManager.SetLockoutEndDateAsync(user, lockoutEndDate);
                }

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "User lockout status updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = string.Join("<br>", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unexpected error occurred: " + ex.Message;
            }

            return RedirectToAction(nameof(EditUser), new { id = user.Id });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "EditUserPolicy")]
        public async Task<IActionResult> UserEmailConfirmed(string id, string emailConfirmed)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = $"User with Id={id} was not found.";
                return View("NotFound");
            }

            try
            {
                if (emailConfirmed == "Email Confirmed")
                {
                    user.EmailConfirmed = true;
                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                    {
                        TempData["SuccessMessage"] = "User email has been successfully confirmed.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = string.Join("<br>", result.Errors.Select(e => e.Description));
                    }
                }
                //--------------------------------------------------
                else if (emailConfirmed == "Send confirmation link")
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action(
                        "EmailConfirm",
                        "Account",
                        new { userId = user.Id, token },
                        Request.Scheme);

                    string content = ReadHtmlTemplate("ConfirmEmail.html");
                    string subject = "Email Confirmation";
                    content = content.Replace("{Subject}", subject)
                                     .Replace("{UserName}", user.Email)
                                     .Replace("{confirmationLink}", confirmationLink);

                    var message = new Message(new string[] { user.Email }, subject, content, null);

                    try
                    {
                        await _emailSender.SendEmailAsync(message);
                        TempData["SuccessMessage"] = "The confirmation link has been sent successfully.";
                    }
                    catch (Exception ex)
                    {
                        TempData["ErrorMessage"] = $"Failed to send email: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(EditUser), new { id = user.Id });
        }


        [HttpPost]
        [Authorize(Policy = "EditUserPolicy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            // يجب التحقق من ان جدول الموظف المربوط بجدول اليوزر غير مستعمل في الأخبار
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "The requested user was not found.";
                    return View("NotFound");
                }

                var currentUser = await _userManager.GetUserAsync(HttpContext.User);
                if (currentUser.Id == id)
                {
                    TempData["ErrorMessage"] = "You cannot delete the currently logged in account.";
                    return RedirectToAction(nameof(EditUser), new { id = user.Id });
                }

                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "The user has been deleted successfully.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = string.Join("<br>", result.Errors.Select(e => e.Description));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(EditUser), new { id });
        }



        public async Task<IActionResult> CreateUser()
        {
            TempData["ErrorMessage"] = "Plain user creation is disabled. Create employees only.";
            return RedirectToAction(nameof(Index));
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserVM model)
        {
            TempData["ErrorMessage"] = "Plain user creation is disabled. Create employees only.";
            return RedirectToAction(nameof(Index));

        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        //[Authorize(Policy = "EditUserPolicy")]
        public async Task<IActionResult> EditUserRoles(List<UserRolesVM> model, string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (userId == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot modify your own roles.";
                return RedirectToAction("EditUser", new { Id = userId });
            }


            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"Cannot be found user with Id={userId}";
                return View("NotFound");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to remove existing roles.";
                return RedirectToAction("EditUser", new { Id = userId });
            }

            var rolesToAdd = model.Where(r => r.IsSelected)
                                  .Select(r => r.RoleName)
                                  .ToList();

            if (rolesToAdd.Count > 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Failed to assign selected roles.";
                    return RedirectToAction("EditUser", new { Id = userId });
                }
            }

            TempData["SuccessMessage"] = "User roles have been modified successfully.";
            return RedirectToAction("EditUser", new { Id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        //[Authorize(Policy = "EditUserPolicy")]
        [Authorize(Policy = "CreatePolicy")]
        public async Task<IActionResult> EditUserClaims(UserClaimListVM model)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (model.UserId == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot modify your own claims.";
                return RedirectToAction("EditUser", new { Id = model.UserId });
            }


            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"Cannot be found user with Id={model.UserId}";
                return View("NotFound");
            }

            var existingClaims = await _userManager.GetClaimsAsync(user);

            var systemClaimTypes = StaticClaims.All.Select(c => c.Type);

            var claimsToRemove = existingClaims
                .Where(c => systemClaimTypes.Contains(c.Type))
                .ToList();

            if (claimsToRemove.Any())
            {
                var removeResult = await _userManager.RemoveClaimsAsync(user, claimsToRemove);
                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Failed to remove existing claims";
                    return RedirectToAction("EditUser", new { Id = model.UserId });
                }
            }

            // إضافة Claims المفعلة فقط
            var claimsToAdd = model.Claims
                .Where(c => c.IsSelected)
                .Select(c => new Claim(c.ClaimType, "true"))
                .ToList();

            if (claimsToAdd.Any())
            {
                var addResult = await _userManager.AddClaimsAsync(user, claimsToAdd);
                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = "Failed to add new claims";
                    return RedirectToAction("EditUser", new { Id = model.UserId });
                }
            }

            TempData["SuccessMessage"] = "User Claims have been modified successfully";
            return RedirectToAction("EditUser", new { Id = model.UserId });
        }


    }
}
