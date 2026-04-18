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
    [Authorize(Roles = "Prog,Admin")]
    public class SectionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IUnitOfWork<Section> _section;

        public SectionsController(AppDbContext context,
                                   IUnitOfWork<Section> section)
        {
            _context = context;
            _section = section;
        }

        [HttpGet]
        [Route("Sections")]
        public async Task<IActionResult> Index()
        {
            return RedirectToAction("Index", "StockBalances");
            //var sections1 = _section.Repository.GetAll(); // News = null
            //var sections2 = await _section.Repository.GetAll().ToListAsync(); // News = null
            //var sections3 = await _section.Repository.GetAll().Include(n => n.News).ToListAsync();
            //var sections4 = await _section.Repository.Include(n => n.News).ToListAsync();
            var sections = await _section.Repository.Include(n => n.News).OrderBy(s => s.Name).ToListAsync();
            //var sections5 = await _section.Repository.Include(s => s.News, s => s.Comments, s => s.Author).ToListAsync();

            ViewData["SectionsCount"] = sections.Count();
            ViewBag.SectionsCount = sections.Count();

            return View(sections);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid? id)
        {
            return RedirectToAction("Index", "StockBalances");
            if (id == null) // عند مسح خانة من id
            {
                //return NotFound();  // 404
                return View("NotFound");
            }

            var section = await _section.Repository.GetByIdAsync(id);
            if (section == null)
            {
                ViewBag.Message = "Section details is not found";
                return View("NotFound");
            }

            return View(section);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("Index", "StockBalances");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Created")] Section section)
        {
            return RedirectToAction("Index", "StockBalances");
            if (ModelState.IsValid)
            {
                try
                {
                    var exists = await _section.Repository
                                      .GetWhere(s => s.Name == section.Name) // لا يهم التحويل الى حروف كبيرة لأنه غير حساس لحالة الأحرف
                                      .FirstOrDefaultAsync();

                    if (exists != null)
                    {
                        ViewBag.Message = $"Section '{exists.Name}' has already been added";
                        //ViewBag.Message = "Section '" + exists.Name + "' has already been added";
                        return View(section);
                    }
                    section.Name = section.Name.Trim();
                    _section.Repository.Insert(section);
                    await _section.SaveAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    throw;
                }
            }
            return View(section);
        }

        [AcceptVerbs("Get", "Post")]
        public async Task<JsonResult> NameExists(string Name)
        {
            return Json(true);

            //var exists1 = await _section.Repository
            //                         .GetWhere(s => s.Name == n1.Trim()) // لا يهم التحويل الى حروف كبيرة لأنه غير حساس لحالة الأحرف
            //                         .FirstOrDefaultAsync();

            var exists = await _section.Repository.GetAll()
                                  .FirstOrDefaultAsync(n => n.Name == Name.Trim()); // لا يهم التحويل الى حروف كبيرة لأنه غير حساس لحالة الأحرف

            if (exists == null)
            {
                return Json(true);
            }
            else
            {
                return Json($"The Section '{exists.Name}' is already in use111111");
            }
        }

        [ViewLayout("_Layout")]
        [HttpGet]
        public async Task<IActionResult> Edit(Guid? id)
        {
            return RedirectToAction("Index", "StockBalances");
            if (id == null)
            {
                return View("NotFound");
            }

            var section = await _section.Repository.GetByIdAsync(id);

            if (section == null)
            {
                ViewBag.Message = "The Section Is Not Found";
                return View("NotFound");
            }
            return View(section);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Name,Id,Created,Modified")] Section section )
        {
            return RedirectToAction("Index", "StockBalances");
            //لمنع حدوث هجمات CSRF (Cross-Site Request Forgery):
            if (id != section.Id)
            {
                return View("NotFound");
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var sectionNameExists = await _section.Repository
                                            .GetWhere(a => a.Name == section.Name.Trim() & a.Id != section.Id)
                                            .FirstOrDefaultAsync();

                    if (sectionNameExists != null)
                    {
                        ViewBag.Message = $"Section '{section.Name}' has already been added";
                        return View(section); // يجب ارجاع المودل حتى في اكشن الإضافة
                    }
                    
                    _section.Repository.Update(section);
                    await _section.SaveAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!SectionExists(section.Id))
                    {
                        return View("NotFound"); // في حال غير موجود
                    }
                    else
                    {
                        //throw; //  صفحة الخطأ الإفتراضية للمتصفح
                        ViewBag.ErrorTitle = "The basic data not found in the database ";
                        //ViewBag.ErrorMessage = "Missing data row- " + ex; // ارسال عبر الايميل غالبًا
                        return View("Error");
                    }

                }
                return RedirectToAction(nameof(Index));
            }
            return View(section);
        }


        [HttpGet]
        public async Task<IActionResult> Delete(Guid? id)
        {
            return RedirectToAction("Index", "StockBalances");
            if (id == null)
            {
                return View("NotFound");
            }

            var section = await _section.Repository.GetByIdAsync(id);
            if (section == null)
            {
                return View("NotFound");
            }

            return View(section);

        }


       
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed1(Guid id, string name, string test)
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            return RedirectToAction("Index", "StockBalances");
            var section = await _section.Repository.GetByIdAsync(id);
            if (section == null)
            {
                return View("NotFound");
            }

            try
            {
                _section.Repository.Delete(section);
                await _section.SaveAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View("Error");
            }
        }


        private bool SectionExists1(Guid id)
        {
            return _context.Sections.Any(e => e.Id == id);
        }

        private bool SectionExists(Guid id)
        {
        return (_section.Repository.GetAll()?.Any(e => e.Id == id)).GetValueOrDefault();

        }

    }
}
