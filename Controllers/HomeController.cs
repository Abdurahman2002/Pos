using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels;

namespace NewsApp2.Controllers
{
    //[ViewLayout("_Layout")]
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly IUnitOfWork<SiteInfo> _siteInfo;
        private readonly IUnitOfWork<Contact> _contact;
        private readonly IUnitOfWork<News> _news;
        private readonly IUnitOfWork<SiteState> _siteState;
        private readonly IEmailSender _emailSender;
        private readonly IUnitOfWork<Section> _section;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public HomeController(IUnitOfWork<SiteInfo> siteInfo,
                               IUnitOfWork<Contact> contact,
                               IUnitOfWork<News> news,
                               IUnitOfWork<SiteState> siteState,
                               IWebHostEnvironment host,
                               IEmailSender emailSender,
                               IUnitOfWork<Section> section,
                               SignInManager<ApplicationUser> signInManager) : base(host)
        {
            _siteInfo = siteInfo;
            _contact = contact;
            _news = news;
            _siteState = siteState;
            _emailSender = emailSender;
            _section = section;
            _signInManager = signInManager;
        }

        [ViewLayout("_LayoutTemplate")]
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Cashier"))
                return RedirectToAction("Create", "SalesInvoices");

            return RedirectToAction("Index", "StockBalances");

            var sections = await _section.Repository.GetAll().ToListAsync();

            foreach (var section in sections)
            {
                section.News = await _news.Repository
                                   .GetWhere(n => n.SectionId == section.Id & n.State == true)
                                   .OrderByDescending(n => n.Created)
                                   .Take(5)
                                   .ToListAsync();
            }

            return View(sections);
        }


        [ViewLayout("_Layout")] // زايدة
        public async Task<IActionResult> About()
        {
            return RedirectToAction("Index", "StockBalances");

            var contact = await _contact.Repository.GetAll().FirstOrDefaultAsync();
            var siteInfo = await _siteInfo.Repository.GetAll().FirstOrDefaultAsync();

            SiteVM siteVM = new SiteVM
            {
                Contact = contact,
                SiteInfo = siteInfo
            };


            return View(siteVM);
        }


        [HttpPost]
        public async Task<IActionResult> Contact(string contactName, string contactEmail, string contactMessage)
        {
            return RedirectToAction("Index", "StockBalances");


            string content = ReadHtmlTemplate("Contact.html");

            content = content.Replace("{SubjectName}", contactName);
            content = content.Replace("{SubjectEmail}", contactEmail);
            content = content.Replace("{Content}", contactMessage);
            string? email = await _contact.Repository.GetAll()
                                                 .Select(n => n.Email)
                                                 .FirstOrDefaultAsync();

            //List<string>? email = await _contact.Repository.GetAll()
            //                                  .Select(n => n.Email)
            //                                  .ToListAsync();

            var message = new Message(new string[] { email }, contactEmail + " - " + contactName, content, null);

            try
            {
                await _emailSender.SendEmailAsync(message);
                TempData["SuccessMessage"] = "The email has been sent successfully";
            }
            catch
            {
                TempData["ErrorMessage"] = "Failed to send email";

                ViewBag.ErrorTitle = "Email Send Error";
                return View("Error");
            }

            return RedirectToAction("Index");
        }


        public async Task<IActionResult> Section(Guid? id)
        {
            return RedirectToAction("Index", "StockBalances");
            if (id == null)
            {
                return View("NotFound");
            }

            var section = await _section.Repository.GetWhere(s => s.Id == id).FirstOrDefaultAsync();
            if (section == null)
            {
                return View("NotFound");
            }

            section.News = await _news.Repository
                                   .GetWhere(n => n.SectionId == section.Id & n.State == true)
                                   .OrderByDescending(n => n.Created)
                                   .ToListAsync();

            return View(section);

        }

        public async Task<IActionResult> News(Guid? id)
        {
            return RedirectToAction("Index", "StockBalances");

            if (id == null)
                return View("NotFound");

            var news = await _news.Repository
                                  .Include(n => n.Sections)
                                  .Where(n => n.Id == id)
                                  .FirstOrDefaultAsync();

            if (news == null)
                return View("NotFound");

            var newsList = await _news.Repository
                                      .GetWhere(n => n.SectionId == news.SectionId && n.Id != news.Id && n.State == true)
                                      .OrderByDescending(n => n.Created)
                                      .Take(10)
                                      .ToListAsync();

            var newsVM = new NewsVM
            {
                News = news,
                NewsList = newsList
            };

            return View(newsVM);



        }



        [AllowAnonymous]
        //[Route("Closing")]
        public async Task<IActionResult> Closing()
        {
            var closingVM = new ClosingVM
            {
                SiteState = await _siteState.Repository.GetAll().FirstOrDefaultAsync(),
                Contact = await _contact.Repository.GetAll().FirstOrDefaultAsync(),
                SiteInfo = await _siteInfo.Repository.GetAll().FirstOrDefaultAsync()
            };

            if (closingVM.SiteState?.State == false)
            {
                await _signInManager.SignOutAsync();
                return View(closingVM);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }



        //-----طرق استدعاء الصفحات---------------------------------------
        //return View(); // استدعاء نفس الصفحة بدون ارسال باراميتر
        //return View(model);// استدعاء نفس الصفحة مع ارسال باراميتر مودل
        //return View("Contact");//  استدعاء صفحة مختلفة بدون ارسال باراميتر
        //return View("About", Id); // استدعاء صفحة مختلفة مع ارسال باراميتر عادي 
        //return View("About", model); // استدعاء صفحة مختلفة مع ارسال باراميتر مودل 
        //return RedirectToAction("About"); // استدعاء وظيفة
        //return RedirectToAction("About" new { successMessage = successMessage}); // استدعاء وظيفة مع ارسال باراميتر
    }

}
