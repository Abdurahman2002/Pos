using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApp2.Classes;
using NewsApp2.Classes.Helpers;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.ViewModels;
using System.Text.RegularExpressions;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]

    [Authorize(Roles = "Prog,Admin,Employee")] // يملك احدى الصلاحيات

    //----------يجب أن يملك الصلاحيات معا---------
    //[Authorize(Roles = "Prog")]
    //[Authorize(Roles = "Admin")]
    //-------------------------------
    public class AdminController : Controller
    {

        private readonly IUnitOfWork<SiteState> _siteState;
        private readonly IUnitOfWork<SiteInfo> _siteInfo;
        private readonly IUnitOfWork<Contact> _contact;
        private readonly IUnitOfWork<Section> _section;
        private readonly IUnitOfWork<News> _news;
        private readonly IWebHostEnvironment _host;
        private readonly IMemoryCache _cache;

        public AdminController(IUnitOfWork<SiteState> siteState,
                               IUnitOfWork<SiteInfo> siteInfo,
                               IUnitOfWork<Contact> contact,
                               IUnitOfWork<Section> section,
                               IUnitOfWork<News> news,
                               IWebHostEnvironment host,
                               IMemoryCache cache)

        {
            _siteState = siteState;
            _siteInfo = siteInfo;
            _contact = contact;
            _section = section;
            _news = news;
            _host = host;
            _cache = cache;
        }


        [HttpGet]
        [Route("Dashboard")]
        [Authorize(Roles = "Prog,Admin,Employee")]
        public async Task<IActionResult> Index()
        {
            ViewBag.sectionCount = await _section.Repository.GetAll().CountAsync();
            ViewBag.newsCount = await _news.Repository.GetAll().CountAsync();

            return View();
        }

        public JsonResult NewsSectionsChart()
        {
            var data1 = _news.Repository.GetAll().Include(s => s.Sections)
                         .Where(n => n.State == true)
                         .GroupBy(g => g.Sections.Name)
                         .ToDictionary(g => g.Key, g => g.Count());

            return Json(data1);
        }
        public JsonResult NewsMonthChart()
        {
            var data1 = _news.Repository.GetAll()
                         .GroupBy(g => g.Created.Month)
                         .ToDictionary(g => g.Key, g => g.Count());

            return Json(data1);
        }

        [HttpGet]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> SiteDetails()
        {
            return View(await GetSiteVM());
        }

        public async Task<SiteVM> GetSiteVM()
        {
            var siteInfo = await _siteInfo.Repository.GetAll().FirstOrDefaultAsync();
            var contact = await _contact.Repository.GetAll().FirstOrDefaultAsync();


            var siteVM = new SiteVM
            {
                SiteInfo = siteInfo,
                Contact = contact
            };
            return siteVM;
        }

        [HttpGet]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> SiteEdit()
        {
            return View(await GetSiteVM());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> SiteEdit(SiteVM siteVM, string? isThereImg1, string? isThereImg2)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string? logoFileName = FileHelper.UploadFile("", siteVM.SiteInfo.Logo, siteVM.SiteInfo.LogoUrl, isThereImg1, _host);

                    string? coverFileName = FileHelper.UploadFile("", siteVM.SiteInfo.CoverImage, siteVM.SiteInfo.CoverImageUrl, isThereImg2, _host);

                    var siteInfo = await _siteInfo.Repository.GetAll().FirstOrDefaultAsync();
                    if (siteInfo != null)
                    {
                        siteInfo.Name = siteVM.SiteInfo.Name;
                        siteInfo.Activity = siteVM.SiteInfo.Activity;
                        siteInfo.About = siteVM.SiteInfo.About;
                        siteInfo.LogoUrl = logoFileName;
                        siteInfo.CoverImageUrl = coverFileName;
                        siteInfo.Created = siteVM.SiteInfo.Created;
                        siteInfo.Modified = DateTime.Now;

                        _siteInfo.Repository.Update(siteInfo);
                    }
                    var contact = await _contact.Repository.GetAll().FirstOrDefaultAsync();
                    if (contact != null)
                    {
                        contact.Email = siteVM.Contact.Email;
                        contact.Phone = siteVM.Contact.Phone;
                        contact.Facebook = siteVM.Contact.Facebook;
                        contact.Twitter = siteVM.Contact.Twitter;
                        contact.Instagram = siteVM.Contact.Instagram;
                        contact.Created = siteVM.Contact.Created;
                        contact.Modified = DateTime.Now;

                        _contact.Repository.Update(contact);
                    }

                    await _siteInfo.SaveAsync();



                    TempData["SuccessMessage"] = "The website data has been successfully saved";
                }
                catch
                {
                    TempData["ErrorMessage"] = "Error Save";
                    //throw;
                }

                return RedirectToAction("SiteDetails");

            }

            return RedirectToAction("SiteEdit");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<ActionResult> SiteEdit1(SiteVM siteVM, string? isThereImg1, string? isThereImg2)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string? logoFileName = UploadFile(siteVM.SiteInfo.Logo, siteVM.SiteInfo.LogoUrl, isThereImg1);
                    string? coverFileName = UploadFile(siteVM.SiteInfo.CoverImage, siteVM.SiteInfo.CoverImageUrl, isThereImg2);


                    var contact = await _contact.Repository.GetAll().FirstOrDefaultAsync();
                    if (contact != null)
                    {
                        contact.Email = siteVM.Contact.Email;
                        contact.Phone = siteVM.Contact.Phone;
                        contact.Facebook = siteVM.Contact.Facebook;
                        contact.Twitter = siteVM.Contact.Twitter;
                        contact.Instagram = siteVM.Contact.Instagram;
                        contact.Created = siteVM.Contact.Created;
                        contact.Modified = DateTime.Now;

                        _contact.Repository.Update(contact);
                    }

                    var siteInfo = await _siteInfo.Repository.GetAll().FirstOrDefaultAsync();
                    if (siteInfo != null)
                    {
                        siteInfo.Name = siteVM.SiteInfo.Name;
                        siteInfo.Activity = siteVM.SiteInfo.Activity;
                        siteInfo.About = siteVM.SiteInfo.About;
                        siteInfo.Created = siteVM.SiteInfo.Created;
                        siteInfo.Modified = DateTime.Now;

                        siteInfo.LogoUrl = logoFileName;
                        siteInfo.CoverImageUrl = coverFileName;

                        _siteInfo.Repository.Update(siteInfo);
                    }

                    await _siteInfo.SaveAsync();
                    TempData["SuccessMessage"] = "The website data has been successfully saved";

                }
                catch
                {
                    TempData["ErrorMessage"] = "Error Save";
                    throw;
                }
                return RedirectToAction("SiteDetails");
            }

            return RedirectToAction("SiteEdit");
        }


        public string? UploadFile(IFormFile? img, string? imageUrl, string? isImg)
        {
            if (isImg == null) // في حال تم حذف الصورة فقط
            {
                DeleteOldFile(imageUrl);
                return null;
            }

            if (img != null)// في حال تم تحميل صورة جديدة
            {
                DeleteOldFile(imageUrl);

                string pictures = Path.Combine(_host.WebRootPath, "pictures");
                string NewPath = Path.Combine(pictures, img.FileName);
                if (!System.IO.File.Exists(NewPath))
                    img.CopyTo(new FileStream(NewPath, FileMode.CreateNew));

                return img.FileName;
            }
            return imageUrl; // في حال لم يتم تحميل صورة جديدة تبقى الصورة القديمة كما هي
        }
        public void DeleteOldFile(string? imageUrl)
        {
            if (imageUrl != null)
            {
                string picturesPath = Path.Combine(_host.WebRootPath, "pictures");
                string oldPath = Path.Combine(picturesPath, imageUrl);
                if (System.IO.File.Exists(oldPath))
                {
                    GC.Collect(); GC.WaitForPendingFinalizers();
                    System.IO.File.Delete(oldPath);
                }
            }
        }


        [AcceptVerbs("Get", "Post")]
        public async Task<JsonResult> EmailIsValid(Contact contact)
        {
            var regex = new Regex(@"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4})$");

            return Json(regex.IsMatch(contact.Email));

        }

        [HttpGet]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> SiteState(string? saveMessage)
        {
            ViewBag.Message = saveMessage;

            var siteState = await _siteState.Repository.GetAll().FirstOrDefaultAsync();
            if (siteState == null)
            {
                return View("NotFound");
            }
            return View(siteState);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog")]
        public async Task<IActionResult> SiteState(string save, [Bind("ClosingMessage")] SiteState siteStat)
        {
            try
            {
                string? message = null;

                var siteStateEdit = await _siteState.Repository.GetAll().FirstOrDefaultAsync();

                if (siteStateEdit == null)
                {
                    return View("NotFound");
                }

                if (save == "Activating Site")
                {
                    siteStateEdit.State = true;
                }
                else if (save == "Deactivate Site")
                {
                    siteStateEdit.State = false;
                }
                else if (save == "Update colsing message")
                {
                    siteStateEdit.ClosingMessage = siteStat.ClosingMessage;
                    message = "Save Success...";
                }

                siteStateEdit.Modified = DateTime.UtcNow;
                _siteState.Repository.Update(siteStateEdit);
                await _siteState.SaveAsync();
                _cache.Remove("SiteState"); // مهم جدا

                TempData["SuccessMessage"] = "Save Success...";

                return RedirectToAction("SiteState", new { saveMessage = message });
            }
            catch
            {
                throw;
            }
        }


    }
}
