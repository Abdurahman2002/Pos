using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Classes.Helpers;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels;
using PasswordGenerator;

namespace NewsApp2.Controllers
{
    [Authorize(Roles = "Prog,Admin")]
    [ViewLayout("_LayoutDashboard")]
    public class EmployeesController : BaseController
    {
        private static readonly string[] OperationalRoles =
        {
            "Cashier",
            "SalesManager",
            "SalesOfficer"
        };

        private static readonly HashSet<string> OperationalRoleSet = new(OperationalRoles, StringComparer.OrdinalIgnoreCase);

        private readonly IUnitOfWork<Employee> _employee;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public EmployeesController(IUnitOfWork<Employee> employee,
                                   IWebHostEnvironment host,
                                   IEmailSender emailSender,
                                   UserManager<ApplicationUser> userManager,
                                   SignInManager<ApplicationUser> signInManager,
                                   RoleManager<IdentityRole> roleManager,
                                   AppDbContext context) : base(host)
        {
            _employee = employee;
            _emailSender = emailSender;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }




        public async Task<IActionResult> Index()
        {
            var employees = await _employee.Repository
                                           .GetAll()
                                           .Include(e => e.ApplicationUser)
                                           .Include(e => e.Warehouse)
                                           .OrderByDescending(u => u.Created)
                                           .ToListAsync();

            ViewData["UserRoles"] = await BuildUserRoleMapAsync(employees.Select(e => e.UserId).Where(id => !string.IsNullOrWhiteSpace(id))!);

            return View(employees);
        }

        public async Task<IActionResult> Pending()
        {
            var employees = await _employee.Repository
                                           .GetAll()
                                           .Include(e => e.ApplicationUser)
                                           .Include(e => e.Warehouse)
                                           .Where(e => e.ApplicationUser.Approval == false)
                                           .OrderByDescending(u => u.Created)
                                           .ToListAsync();

            ViewData["UserRoles"] = await BuildUserRoleMapAsync(employees.Select(e => e.UserId).Where(id => !string.IsNullOrWhiteSpace(id))!);

            return View(employees);
        }

        [HttpGet]
        public async Task<IActionResult> CreateEmployee()
        {
            var vm = new CreateEmployeeVM
            {
                Email = string.Empty,
                AvailableRoles = await GetAssignableRolesAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeVM model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await GetAssignableRolesAsync();
                return View(model);
            }

            var selectedRole = model.RoleName?.Trim() ?? string.Empty;
            var roleExists = await _roleManager.RoleExistsAsync(selectedRole);
            if (string.IsNullOrWhiteSpace(selectedRole) || !roleExists)
            {
                ModelState.AddModelError(nameof(model.RoleName), "Please select a valid role.");
                model.AvailableRoles = await GetAssignableRolesAsync();
                return View(model);
            }

            if (await _userManager.FindByEmailAsync(model.Email.Trim()) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "This email is already used by another account.");
                model.AvailableRoles = await GetAssignableRolesAsync();
                return View(model);
            }


            using var transaction = await _context.Database.BeginTransactionAsync();


            // ---------------- Create User ----------------
            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                Approval = true,
                CreatedDate = DateTime.UtcNow
            };

            var passwordGenerator = new Password(true, true, true, false, 5);
            var generatedPassword = passwordGenerator.Next();

            IdentityResult result;
            try
            {
                result = await _userManager.CreateAsync(user, generatedPassword);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                ModelState.AddModelError(nameof(model.Email), "This email is already used by another account.");
                model.AvailableRoles = await GetAssignableRolesAsync();
                return View(model);
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                model.AvailableRoles = await GetAssignableRolesAsync();
                return View(model);
            }

            // ---------------- Create Employee ----------------
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

            // ---------------- Role Handling ----------------
            if (!await _roleManager.RoleExistsAsync("Employee"))
                await _roleManager.CreateAsync(new IdentityRole("Employee"));

            if (!await _userManager.IsInRoleAsync(user, "Employee"))
                await _userManager.AddToRoleAsync(user, "Employee");

            if (!await _userManager.IsInRoleAsync(user, selectedRole))
                await _userManager.AddToRoleAsync(user, selectedRole);

            // ---------------- Email Confirmation ----------------
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action(
                "EmailConfirm",
                "Account",
                new { userId = user.Id, token },
                Request.Scheme
            );

            string content = ReadHtmlTemplate("ConfirmEmailWithPassword.html")
                .Replace("{Subject}", "Email Confirmation")
                .Replace("{UserName}", model.Email)
                .Replace("{Password}", generatedPassword)
                .Replace("{confirmationLink}", confirmationLink);

            var message = new Message(new[] { model.Email }, "Email Confirmation", content, null);

            try
            {
                await _emailSender.SendEmailAsync(message);
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Employee created successfully with role '{selectedRole}'.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "An unexpected error occurred";
                model.AvailableRoles = await GetAssignableRolesAsync();
                return View(model);
            }
        }

        private async Task<List<SelectListItem>> GetAssignableRolesAsync()
        {
            foreach (var role in OperationalRoles)
                await EnsureRoleExistsAsync(role);

            var blockedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Employee",
                "EmployeePending",
                "EmployeeRequest"
            };

            var roles = await _roleManager.Roles
                .AsNoTracking()
                .Where(r => r.Name != null)
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            return roles
                .Where(r => !blockedRoles.Contains(r) && OperationalRoleSet.Contains(r))
                .Select(r => new SelectListItem(r, r))
                .ToList();
        }

        private async Task<Dictionary<string, List<string>>> BuildUserRoleMapAsync(IEnumerable<string> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (!ids.Any())
                return new Dictionary<string, List<string>>();

            var roleMap = await (from userRole in _context.UserRoles
                                 join role in _context.Roles on userRole.RoleId equals role.Id
                                 where ids.Contains(userRole.UserId)
                                 select new { userRole.UserId, role.Name })
                .ToListAsync();

            return roleMap
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Name ?? string.Empty)
                          .Where(x => !string.IsNullOrWhiteSpace(x))
                          .OrderBy(x => x)
                          .ToList());
        }

        public async Task<IActionResult> EditEmployee(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var employee = await _employee.Repository
                .GetWhere(e => e.Id == id)
                .Include(e => e.ApplicationUser)
                .Include(e => e.Warehouse)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                ViewBag.ErrorMessage = $"Cannot be found employee with Id = {id}";
                return View("NotFound");
            }

            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockoutEmployee(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("NotFound");

            var user = await _userManager.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Employee == null)
                return View("NotFound");

            var currentUser = await _userManager.GetUserAsync(HttpContext.User);
            if (currentUser?.Id == userId)
            {
                TempData["ErrorMessage"] = "You cannot lockout the currently logged-in user.";
                return RedirectToAction(nameof(EditEmployee), new { id = user.Employee.Id });
            }

            try
            {
                IdentityResult result;
                bool isCurrentlyLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

                if (isCurrentlyLocked)
                {
                    result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);
                }
                else
                {
                    var lockoutEndDate = DateTimeOffset.UtcNow.AddYears(10);
                    result = await _userManager.SetLockoutEndDateAsync(user, lockoutEndDate);
                }

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Employee lockout status updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = UserMessageSanitizer.Sanitize(ex.Message, "An unexpected error occurred.");
            }

            return RedirectToAction(nameof(EditEmployee), new { id = user.Employee.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmployeeEmail(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("NotFound");

            var user = await _userManager.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Employee == null)
                return View("NotFound");

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = result.Succeeded
                ? "Employee email confirmed."
                : string.Join(" ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(EditEmployee), new { id = user.Employee.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmployeeConfirmationLink(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("NotFound");

            var user = await _userManager.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Employee == null)
                return View("NotFound");

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(
                    "EmailConfirm",
                    "Account",
                    new { userId = user.Id, token },
                    Request.Scheme);

                string content = ReadHtmlTemplate("ConfirmEmail.html")
                    .Replace("{Subject}", "Email Confirmation")
                    .Replace("{UserName}", user.Email)
                    .Replace("{confirmationLink}", confirmationLink);

                var message = new Message(new[] { user.Email }, "Email Confirmation", content, null);
                await _emailSender.SendEmailAsync(message);

                TempData["SuccessMessage"] = "Confirmation link sent.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to send confirmation link.";
            }

            return RedirectToAction(nameof(EditEmployee), new { id = user.Employee.Id });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee([Bind("Id,Name,UserId,Created")] Employee employee)
        {
            if (!ModelState.IsValid)
                return View(employee);

            var existingEmployee = await _employee.Repository
                .GetWhere(e => e.Id == employee.Id)
                .FirstOrDefaultAsync();

            if (existingEmployee == null)
                return View("NotFound");


            bool nameExists = await _employee.Repository
                .GetWhere(e => e.Id != employee.Id && e.Name == employee.Name)
                .AnyAsync();

            if (nameExists)
            {
                ModelState.AddModelError("Name", "The employee name already exists");
                return View(employee);
            }

            try
            {
                existingEmployee.Name = employee.Name;
                existingEmployee.Modified = DateTime.UtcNow;
                _employee.Repository.Update(existingEmployee);
                await _employee.SaveAsync();
            }
            catch
            {
                return View("Error");
            }

            TempData["SuccessMessage"] = "Saved successfully";
            return RedirectToAction(nameof(EditEmployee), new { id = employee.Id });
        }



        public async Task<IActionResult> ChangeEmployeeApproval(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("NotFound");

            var user = await _userManager.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return View("NotFound");


            user.Approval = !user.Approval;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Description;
                return RedirectAfterApproval(user);
            }

            if (user.Approval == true)
            {
                var selectedRole = await GetAssignedOperationalRoleAsync(user) ?? "SalesOfficer";
                await AssignOperationalRoleAsync(user, selectedRole);
                TempData["SuccessMessage"] = $"Employee approved with role '{selectedRole}'.";
            }
            else
            {
                await SetPendingRoleAsync(user);
                TempData["SuccessMessage"] = "Employee returned to pending approval.";
            }

            return RedirectAfterApproval(user);
        }

        [HttpGet]
        public async Task<IActionResult> Approve(Guid? id)
        {
            if (id == null)
                return View("NotFound");

            var employee = await _employee.Repository
                .GetWhere(e => e.Id == id)
                .FirstOrDefaultAsync();

            if (employee == null)
                return View("NotFound");

            if (string.IsNullOrWhiteSpace(employee.UserId))
                return View("NotFound");

            var user = await _userManager.FindByIdAsync(employee.UserId);
            if (user == null)
                return View("NotFound");

            var availableRoles = await GetAssignableRolesAsync();
            var availableRoleNames = availableRoles.Select(r => r.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var roleName = await GetAssignedOperationalRoleAsync(user) ?? "SalesOfficer";
            if (!availableRoleNames.Contains(roleName))
                roleName = availableRoles.FirstOrDefault()?.Value ?? "SalesOfficer";

            var vm = new ApproveEmployeeVM
            {
                EmployeeId = employee.Id,
                UserId = employee.UserId ?? string.Empty,
                EmployeeName = employee.Name,
                RoleName = roleName,
                AvailableRoles = availableRoles
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(ApproveEmployeeVM vm)
        {
            var selectedRole = vm.RoleName?.Trim() ?? string.Empty;
            var assignableRoleNames = (await GetAssignableRolesAsync())
                .Select(r => r.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(selectedRole) || !assignableRoleNames.Contains(selectedRole))
            {
                ModelState.AddModelError(nameof(vm.RoleName), "Please select a valid role.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct());
                return RedirectToAction(nameof(Approve), new { id = vm.EmployeeId });
            }

            var employee = await _employee.Repository
                .GetWhere(e => e.Id == vm.EmployeeId)
                .FirstOrDefaultAsync();

            if (employee == null)
                return View("NotFound");

            if (string.IsNullOrWhiteSpace(employee.UserId))
                return View("NotFound");

            var user = await _userManager.FindByIdAsync(employee.UserId);
            if (user == null)
                return View("NotFound");

            user.Approval = true;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Description;
                return RedirectToAction(nameof(Pending));
            }

            await AssignOperationalRoleAsync(user, selectedRole);
            TempData["SuccessMessage"] = $"Employee approved with role '{selectedRole}'.";
            return RedirectToAction(nameof(Pending));
        }

        private async Task<string?> GetAssignedOperationalRoleAsync(ApplicationUser user)
        {
            foreach (var role in OperationalRoles)
            {
                if (await _userManager.IsInRoleAsync(user, role))
                    return role;
            }

            return null;
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }

        private async Task AssignOperationalRoleAsync(ApplicationUser user, string roleName)
        {
            await EnsureRoleExistsAsync("Employee");
            await EnsureRoleExistsAsync(roleName);

            foreach (var operationalRole in OperationalRoles)
            {
                if (!string.Equals(operationalRole, roleName, StringComparison.OrdinalIgnoreCase)
                    && await _userManager.IsInRoleAsync(user, operationalRole))
                {
                    await _userManager.RemoveFromRoleAsync(user, operationalRole);
                }
            }

            if (!await _userManager.IsInRoleAsync(user, "Employee"))
                await _userManager.AddToRoleAsync(user, "Employee");

            if (!await _userManager.IsInRoleAsync(user, roleName))
                await _userManager.AddToRoleAsync(user, roleName);

            if (await _userManager.IsInRoleAsync(user, "EmployeePending"))
                await _userManager.RemoveFromRoleAsync(user, "EmployeePending");

            // Legacy role cleanup
            if (await _userManager.IsInRoleAsync(user, "EmployeeRequest"))
                await _userManager.RemoveFromRoleAsync(user, "EmployeeRequest");
        }

        private async Task SetPendingRoleAsync(ApplicationUser user)
        {
            await EnsureRoleExistsAsync("EmployeePending");

            foreach (var operationalRole in OperationalRoles)
            {
                if (await _userManager.IsInRoleAsync(user, operationalRole))
                    await _userManager.RemoveFromRoleAsync(user, operationalRole);
            }

            if (await _userManager.IsInRoleAsync(user, "Employee"))
                await _userManager.RemoveFromRoleAsync(user, "Employee");

            if (!await _userManager.IsInRoleAsync(user, "EmployeePending"))
                await _userManager.AddToRoleAsync(user, "EmployeePending");

            // Legacy role cleanup
            if (await _userManager.IsInRoleAsync(user, "EmployeeRequest"))
                await _userManager.RemoveFromRoleAsync(user, "EmployeeRequest");
        }


        private IActionResult RedirectAfterApproval(ApplicationUser user)
        {
            var referer = HttpContext.Request.Headers["Referer"].ToString(); // جلب اللنك الكامل للصفحة السابقة التي تم الاستدعاء منها

            if (!string.IsNullOrEmpty(referer) 
                   && referer.Contains("EditEmployee"))
            {
                return RedirectToAction("EditEmployee", new { id = user.Employee.Id });
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEmployee(Guid id)
        {
            var employee = await _employee.Repository
                .GetWhere(e => e.Id == id)
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync();

            if (employee == null)
                return View("NotFound");

            var user = employee.ApplicationUser;
            if (user != null)
            {
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(EditEmployee), new { id });
                }

                TempData["SuccessMessage"] = "Employee deleted successfully.";
                return RedirectToAction(nameof(Index));
            }

            _employee.Repository.Delete(employee);
            await _employee.SaveAsync();
            TempData["SuccessMessage"] = "Employee deleted successfully.";
            return RedirectToAction(nameof(Index));
        }


    }
}
