using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    //[Authorize(Roles = "Prog,Admin,Employee")]
    [Authorize(Policy = "ProgOrAdminOrEmployeePolicy")]
    public class NewsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly IUnitOfWork<News> _news;
        private readonly IUnitOfWork<Section> _section;
        //private readonly IWebHostEnvironment _host;

        public NewsController(AppDbContext context,
                              IUnitOfWork<News> news,
                              IUnitOfWork<Section> section,
                              IWebHostEnvironment host) : base(host)

        {
            _context = context;
            _news = news;
            _section = section;
            //_host = host;
        }


        //[Route("News")] // أي اسم وليس بالضرورة اسم الكونترولر
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return RedirectToAction("Index", "StockBalances");
            var news2 = _news.Repository.GetAll().ToList(); //Section=null 
            var news3 = _news.Repository.Include(n => n.Sections).ToList();
            
            var news4 = await _news.Repository.Include(n => n.Sections).ToListAsync();
            
            //var news5 = _news.Repository.Include(n => n.Sections);
            //return View(await news5.ToListAsync());

            var news = await _news.Repository.Include(n => n.Sections)
                            .OrderByDescending(n => n.Created)
                            .ToListAsync();
            ViewBag.Section = "All News";  //ViewComponent في Default يستطيع تمرير قيمة الى صفحة Controller 
            return View(news);

        }

        [HttpGet]
        public async Task<IActionResult> DisplaySectionNews(Guid? sectionId)
        {
            return RedirectToAction("Index", "StockBalances");
            if (sectionId is null)
                return View("NotFound");



            var news = await _news.Repository
                                 .Include(n => n.Sections)
                                 .Where(n => n.SectionId == sectionId)
                                 .OrderByDescending(n => n.Created)
                                 .ToListAsync();

            string? sectionName = await _section.Repository
                           .GetWhere(n => n.Id == sectionId)
                           .Select(n => n.Name)
                           .FirstOrDefaultAsync();

            ViewBag.Section = sectionName ?? "";
            //-------------------------------------------------------
            {
                var section = await _section.Repository
                               .GetWhere(n => n.Id == sectionId)
                               .Include(n => n.News)
                               .FirstOrDefaultAsync();

                var news1 = section?.News;
                ViewBag.Section = section?.Name;
            }
            //-------------------------------------------------------

            return View("Index", news);
        }




        [HttpGet]
        [Authorize(Policy = "CreatePolicy")]
        public async Task<IActionResult> Create()
        {
            return RedirectToAction("Index", "StockBalances");
            ViewData["Sections"] =
                new SelectList(await _section.Repository.GetAll().ToListAsync(), "Id", "Name");

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Details,State,SectionId,File,ImageUrl")] News news, string? isThereImg1)
        {
            return RedirectToAction("Index", "StockBalances");
            //هذه لا تستعمل ابدا مع المفتاح الأجنبي لكن لو احتجنا لها في حقول اخرى
           // ModelState.Remove("Sections");// طريقة أخرى لمنع التحقق داخل وظيفة معينة وليس في الكلاس سيمنع التحقق بالمطلق لكل الوظائف

            if (ModelState.IsValid)
            {
                if (!CheckImgExtension(news.File))
                {
                    ViewBag.Message = "The attached file is not an image file";
                    return View();
                }

                string? fileName = UploadFile("news", news.File, news.ImageUrl, isThereImg1);

                try
                {
                    news.ImageUrl = fileName;
                    news.Created = DateTime.Now;
                    _news.Repository.Insert(news);
                    await _news.SaveAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorTitle = "faild save in the database";
                    ViewBag.ErrorMessage = "Missing data row- " + ex;  // ارسالها للإيميل وعدم عرضها
                    return View("Error");
                }
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {

            if (id == null)
            {
                return View("NotFound");
            }

            //var news1= _news.Repository.Include(s => s.Sections).GetByIdAsync(id);

            var news = await _news.Repository.Include(s => s.Sections)
                                         .Where(n => n.Id == id)
                                         .FirstOrDefaultAsync();
            if (news == null)
            {
                return View("NotFound");
            }

            ViewData["Sections"] = new SelectList(await _section.Repository.GetAll().ToListAsync(), "Id", "Name", news.SectionId);

            return View(news);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Prog,Admin")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            return RedirectToAction("Index", "StockBalances");
            if (id == null)
            {
                return View("NotFound");
            }
                return RedirectToAction("Index", "StockBalances");

            try
            {
                var news = await _news.Repository.GetByIdAsync(id);
                if (news == null)
                {
                    return View("NotFound");
                }

                _news.Repository.Delete(news);
                await _news.SaveAsync();

                DeleteOldFile(news.ImageUrl);

            }
            catch (Exception ex)
            {
                ViewBag.ErrorTitle = "The basic data not found in the database ";
                // ViewBag.ErrorMessage = "Missing data row- " + ex;  // ارسالها للإيميل وعدم عرضها
                return View("Error");

            }
            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Title,Details,State,SectionId,Id,Created,File,ImageUrl")] News news, string? isThereImg1)
        {

            return RedirectToAction("Index", "StockBalances");
            // (Cross-Site Request Forgery)

            if (id != news.Id)
            {
                return View("NotFound");
            }

            if (ModelState.IsValid)
            {
                if (!CheckImgExtension(news.File))
                {
                    ViewBag.Message = "The attached file is not an image file";
                    return View();
                }
                string? fileName = UploadFile("news", news.File, news.ImageUrl, isThereImg1);

                try
                {
                    news.ImageUrl = fileName;
                    news.Modified = DateTime.Now;
                    _news.Repository.Update(news);
                    await _news.SaveAsync();


                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!NewsExists(news.Id)) //في حال محذوف
                    {
                        return View("NotFound");
                    }
                    else
                    {
                        ViewBag.ErrorTitle = "The basic data not found in the database ";
                        //ViewBag.ErrorMessage = "Missing data row- " + ex;  // ارسالها للإيميل وعدم عرضها
                        return View("Error");
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(news);
        }



        private bool NewsExists(Guid id)
        {
            return (_news.Repository.GetAll()?.Any(e => e.Id == id)).GetValueOrDefault();
        }


    }
}
