using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NewsApp2.Classes;
using NewsApp2.Classes.Helpers;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels;
using NewsApp2.ViewModels.Identity;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Roles = "Prog,Admin")]
    public class MailController : Controller
    {


        private readonly MailSettings _mailSettings;
        private readonly IWebHostEnvironment _host;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public MailController(MailSettings mailSettings,
                              IWebHostEnvironment host,
                              IEmailSender emailSender, 
                              RoleManager<IdentityRole> roleManager,
                              UserManager<ApplicationUser> userManager)
        {
            _mailSettings = mailSettings;
            _host = host;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        [HttpGet]
        public ActionResult EmailSettings()
        {
            var mailSettings = _mailSettings;
            if (mailSettings == null)
            {
                return View("Notfound");
            }

            return View(mailSettings);
        }


        [HttpGet]
        public IActionResult SendEmail()
        {
            var emailVM = new EmailVM
            {
                MailSettings = _mailSettings
            };

            return View(emailVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendEmail(EmailVM emailVM)
        {
            //-------------------------تجهيز التمبلت فقط--------------------------------
            var filePath = _host.WebRootPath + "\\templates" + "\\Email.html";
            StreamReader htmlFile = new StreamReader(filePath);
            string content = htmlFile.ReadToEnd(); // string content1 = "<!DOCTYPE html>\n <html>";
            htmlFile.Close();

            //تم استعماله مرتين: مرة ضمن الرسالة ومرة اخرى في عنوان الايميل ولكنه نفس العنوان// Subject
            content = content.Replace("{Subject}", emailVM.Subject); // يظهر داخل الرسالة
            content = content.Replace("{Content}", emailVM.Content);

            //var domain = $"{Request.Scheme}://{Request.Host.Host}";
            //content = content.Replace("{domain}", domain);
            //---------------------------------------------------------

            var message = new Message(new string[] { emailVM.To }, emailVM.Subject, content, emailVM.Attachments);

            try
            {
                await _emailSender.SendEmailAsync(message);
                TempData["SuccessMessage"] = "The email has been sent successfully";
            }
            catch
            {
                ViewBag.errorMessage = "Failed to send email";
                TempData["ErrorMessage"] = "Failed to send email";
            }

            emailVM.MailSettings = _mailSettings;
            return View(emailVM);

        }


        [HttpGet]
        public async Task<IActionResult> SendEmailToRole(string roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId))
                return View("NotFound");

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                ViewBag.Message = $"Role with Id={roleId} not found";
                return View("NotFound");
            }

            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);

            var model = new RoleEmailsVM
            {
                Id = role.Id,
                Name = role.Name,
                UsersCount = usersInRole.Count,
                MailSettings = _mailSettings,
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmailToRole(RoleEmailsVM roleEmailsVM)
        {


            string content = FileHelper.ReadHtmlTemplate("Email.html", _host);
            content = content.Replace("{Subject}", roleEmailsVM.Subject?.Trim());
            content = content.Replace("{Content}", roleEmailsVM.Content?.Trim());

            //var usersEmails1 = _userManager.Users.Select(u => u.Email).ToList();
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleEmailsVM.Name);

            if (!usersInRole.Any())
            {
                TempData["ErrorMessage"] = $"No users found in the role '{roleEmailsVM.Name}'";
                roleEmailsVM.MailSettings = _mailSettings;
                roleEmailsVM.UsersCount = 0;
                return View(roleEmailsVM);
            }


            usersInRole = usersInRole.Where(u => u.EmailConfirmed).ToList();

            //var usersEmails = usersInRole.Select(u => u.Email).ToArray();


            var usersEmails = usersInRole.Select(u => u.Email).ToList();

            var message = new Message(usersEmails.ToArray(), roleEmailsVM.Subject, content, roleEmailsVM.Attachments);

            try
            {
                await _emailSender.SendEmailAsync(message);
                TempData["SuccessMessage"] = "The email has been sent successfully";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = UserMessageSanitizer.Sanitize(ex.Message, "Failed to send email.");
            }

            roleEmailsVM.MailSettings = _mailSettings;
            roleEmailsVM.UsersCount = usersInRole.Count;

            return View(roleEmailsVM);
        }



    }
}
