using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels;
using NewsApp2.ViewModels.Identity;

namespace NewsApp2.Controllers
{
    // [AllowAnonymous]//  لآكشن معينة داخل الكونترولر Authorize عند استعماله لايمكن تفعيل
    public class AccountController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _host;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork<Employee> _employee;
        private readonly IServiceProvider _serviceProvider;
        private readonly AppDbContext _context;

        public AccountController(
                                 UserManager<ApplicationUser> userManager,
                                 SignInManager<ApplicationUser> signInManager,

                                 RoleManager<IdentityRole> roleManager,

                                 IUnitOfWork<Employee> employee,

                                 IWebHostEnvironment host,
                                 IEmailSender emailSender,
                                 IServiceProvider serviceProvider,
                                 AppDbContext context) : base(host)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _host = host;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _employee = employee;
            _serviceProvider = serviceProvider;
            _context = context;
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View(new EmployeeRegisterVM
            {
                Email = string.Empty,
                Password = string.Empty,
                ConfirmPassword = string.Empty,
                Employee = new Employee()
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(EmployeeRegisterVM registerVM)
        {
            return await RegisterEmployeeInternal(registerVM, nameof(Register));
        }




        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> IsEmailInUse(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Json(true);
            }
            else
            {
                return Json($"The Email:'{email}' is already in use");
            }
        }

        public async Task<IActionResult> EmailConfirm(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return View(nameof(NotFound));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.ErrorMessage = $"Cannot be found user with Id={userId}";
                return View(nameof(NotFound));
            }

            if (user.EmailConfirmed)
            {
                ViewBag.ErrorMessage = "You have already verified your email address";
                return View();
            }

            //var result = await _userManager.ConfirmEmailAsync(user, token);
            //if (result.Succeeded)
            //{

            //}

            if ((await _userManager.ConfirmEmailAsync(user, token)).Succeeded)
            {
                //------------------------Employee Message----------------------------------
                bool isEmployee = await _userManager.IsInRoleAsync(user, "EmployeePending");
                if (isEmployee)
                {
                    ViewBag.Message = "You need the << admin's approval >> to activate your account 'as an employee'";
                    return View();
                }
                //------------------------------------------------------------------------


                return View();
            }

            ViewBag.ErrorMessage = "Email Confirmation failed";
            return View();
        }

        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User))
            {
                {
                    //var id = _userManager.GetUserId(User);
                    //var userName = User.Identity.Name;
                    //User.IsInRole("Admin");
                }
                return RedirectToAction("Index", "Home");
            }

            return View();

        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM loginVM)
        {
            if (!ModelState.IsValid) return View(loginVM);

            var user = await _userManager.FindByEmailAsync(loginVM.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email Not Found");
                return View(loginVM);
            }


            if (!await _userManager.IsEmailConfirmedAsync(user)
                && await _userManager.CheckPasswordAsync(user, loginVM.Password)) // بحيث انه لن يعطي رسالة الكونفيرميد الا اذا كان الاسم والباسوورد صحيحين
            {
                ViewBag.ErrorTitle = "Login failed";
                ViewBag.ErrorMessage = "You must have a confirmed email to log on.";
                return View("Result");
            }


            //var result1 = await _signInManager.PasswordSignInAsync(loginVM.Email, loginVM.Password, loginVM.RememberMe, true);

            var result = await _signInManager.PasswordSignInAsync(user, loginVM.Password, loginVM.RememberMe, true);

            if (result.Succeeded)
            {
                user.LastAccessTime = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                if (user.Approval == false
                    && !await _userManager.IsInRoleAsync(user, "Admin")
                    && !await _userManager.IsInRoleAsync(user, "Prog"))
                {
                    return RedirectToAction(nameof(PendingApproval));
                }

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Dashboard");
                }

                if (await _userManager.IsInRoleAsync(user, "Prog"))
                {
                    //--------------------------------------------------------------
                    {
                        //// المبرمج الرئيسي
                        if (user.UserName == "Programmer@Gmail.com")
                        {
                            if (!await CheckProgClaims(user, loginVM))
                            {
                                ModelState.AddModelError("", "Failed to setup Lead Developer claims.");
                                return View("Login", loginVM);
                            }
                        }
                    }
                    //----------------------------------------------------------------
                    return RedirectToAction("Index", "Dashboard");

                }

                if (await _userManager.IsInRoleAsync(user, "SalesManager"))
                {
                    return RedirectToAction("Index", "Dashboard");
                }

                if (await _userManager.IsInRoleAsync(user, "Cashier"))
                {
                    return RedirectToAction("Create", "SalesInvoices");
                }

                // المستخدم العادي
                return RedirectToAction("Index", "StockBalances");
            }

            if (result.IsLockedOut)
            {
                if (user.LockoutEnd.HasValue
                    && user.LockoutEnd.Value.Year > DateTimeOffset.UtcNow.Year)
                {
                    ViewBag.ErrorTitle = "Login failed";
                    ViewBag.ErrorMessage = $"The user '{loginVM.Email}' is locke out from Administrator.";
                    return View("Result");
                }
                else
                {
                    ViewBag.ErrorTitle = "Login failed";
                    ViewBag.ErrorMessage = $"You made 5 wrong attempts. The user '{loginVM.Email}' is locked, try again after 15 minutes.";
                    ViewBag.ResetPassword = "Reset Password";
                    return View("Result");
                }
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt");
            return View(loginVM);
        }


        public async Task<bool> CheckProgClaims(ApplicationUser user, LoginVM model)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);

            var missingClaims = StaticClaims.All
                .Where(sc => !userClaims.Any(uc => uc.Type == sc.Type))
                .ToList();

            if (missingClaims.Any())
            {
                var result = await _userManager.AddClaimsAsync(user, missingClaims);
                return result.Succeeded;
            }

            // إذا كان لديه جميع Claims
            return true;
        }


        [Authorize] // سيحوله الى Login
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult ForgotPassword()
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ViewBag.ErrorTitle = "Failed Reset password";
                ViewBag.ErrorMessage = "We cannot find the email, please check your email spelling ";
                return View("Result");
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                ViewBag.ErrorTitle = "Failed Reset password";
                ViewBag.ErrorMessage = "Your account needs to be Email Confirmation";
                return View("Result");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResetLink = Url.Action("ResetPassword", "Account", new { email = model.Email, token = token }, Request.Scheme);
            //------------------send email-----------------------------------

            string content = ReadHtmlTemplate("ForgotPassword.html");

            var subject = "Forgot password";
            content = content.Replace("{Subject}", subject);
            content = content.Replace("{UserName}", model.Email);
            content = content.Replace("{passwordResetLink}", passwordResetLink);

            var message = new Message(new string[] { model.Email }, subject, content, null);
            try
            {
                await _emailSender.SendEmailAsync(message);
            }
            catch
            {
                ViewBag.ErrorTitle = "Reset password Error";
                ViewBag.ErrorMessage = "Failed to send email";
                return View("Error");
            }
            //-------------------------------------------------------

            TempData["SuccessTitle"] = "Reset password";
            TempData["SuccessMessage"] = "sending email";

            ViewBag.SuccessTitle = "Reset password";
            ViewBag.SuccessMessage = "Please check your email, The link of reset password has been sent to your email";
            return View("Result");

        }


        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
                ModelState.AddModelError(string.Empty, "Invalid password reset link");


            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Token))
            {
                ModelState.AddModelError("", "Invalid password reset request.");
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ViewBag.ErrorTitle = "Failed Reset Password";
                ViewBag.ErrorMessage = "Account not found.";
                return View("Result");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                ViewBag.SuccessTitle = "Password Reset";
                ViewBag.SuccessMessage = "Your password has been reset successfully.";
                return View("Result");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }


        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["ErrorTitle"] = "User Not Found";
                TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);

                TempData["SuccessTitle"] = "Password Updated";
                TempData["SuccessMessage"] = "Your password has been changed successfully.";

                ViewData["SuccessTitle"] = "Password Updated";
                ViewData["SuccessMessage"] = "Your password has been changed successfully.";

                return View("Result");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }


        public IActionResult AccessDenied(string message)
        {
            ViewBag.Message = message;
            return View();
        }

        [Authorize]
        public async Task<IActionResult> PendingApproval()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return View("NotFound");

            if (user.Approval == true)
                return RedirectToAction("Index", "Home");

            return View();
        }


        public IActionResult EmployeeRegister()
        {
            return View(new EmployeeRegisterVM
            {
                Email = string.Empty,
                Password = string.Empty,
                ConfirmPassword = string.Empty,
                Employee = new Employee()
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmployeeRegister(EmployeeRegisterVM model)
        {
            return await RegisterEmployeeInternal(model, nameof(EmployeeRegister));
        }

        private async Task<IActionResult> RegisterEmployeeInternal(EmployeeRegisterVM model, string viewName)
        {
            if (!ModelState.IsValid)
                return View(viewName, model);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                _context.ChangeTracker.Clear();
                using var transaction = await _context.Database.BeginTransactionAsync();

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    Approval = false,
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);

                    return View(viewName, model);
                }

                var nameExists = await _employee.Repository
                                             .GetWhere(e => e.Name == model.Employee.Name)
                                             .AnyAsync();
                if (nameExists)
                {
                    TempData["ErrorMessage"] = "The Employee name is reserved";
                    return View(viewName, model);
                }

                var employee = new Employee
                {
                    Name = model.Employee.Name,
                    UserId = user.Id,
                    Created = DateTime.UtcNow
                };

                try
                {
                    _employee.Repository.Insert(employee);
                    await _employee.SaveAsync();
                }
                catch
                {
                    TempData["ErrorMessage"] = "Failed to create employee profile";
                    return View("Error");
                }

                var pendingRoles = new[] { "EmployeePending" };
                foreach (var roleName in pendingRoles)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        var role = new IdentityRole
                        {
                            Name = roleName,
                            ConcurrencyStamp = Guid.NewGuid().ToString()
                        };

                        var roleResult = await _roleManager.CreateAsync(role);
                        if (!roleResult.Succeeded)
                        {
                            TempData["ErrorMessage"] = $"Failed to create role '{roleName}'";
                            return View("Error");
                        }
                    }

                    if (!await _userManager.IsInRoleAsync(user, roleName))
                    {
                        var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
                        if (!addRoleResult.Succeeded)
                        {
                            TempData["ErrorMessage"] = $"Failed to assign role '{roleName}'";
                            return View("Error");
                        }
                    }
                }

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(
                    "EmailConfirm",
                    "Account",
                    new { userId = user.Id, token },
                    Request.Scheme
                );

                string content = ReadHtmlTemplate("ConfirmEmail.html");
                string subject = "Email Confirmation";

                content = content.Replace("{Subject}", subject)
                                 .Replace("{UserName}", model.Email)
                                 .Replace("{confirmationLink}", confirmationLink);

                var message = new Message(new[] { model.Email }, subject, content, null);
                try
                {
                    await _emailSender.SendEmailAsync(message);
                    await transaction.CommitAsync();

                    ViewBag.SuccessTitle = "Email confirmation required";
                    ViewBag.SuccessMessage = "Please check your email, we sent a confirmation link";

                    return View("Result");
                }
                catch
                {
                    TempData["ErrorMessage"] = "User created, but email sending failed";
                    return View(viewName, model);
                }
            });
        }

    }

}

