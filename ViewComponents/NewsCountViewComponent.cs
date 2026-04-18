
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;

namespace NewsApp2.ViewComponents
{
    public class NewsCountViewComponent : ViewComponent
    {
        private readonly IUnitOfWork<News> _news;
        private readonly IUnitOfWork<Section> _section;

        public NewsCountViewComponent(IUnitOfWork<News> news,
                                      IUnitOfWork<Section> section)
        {
            _news = news;
            _section = section;
        }


        public async Task<IViewComponentResult> InvokeAsync()
        {
            var sections = await _section.Repository.GetAll().ToListAsync();


            foreach (var section in sections)
            {
                section.SectionNewsCount = await _news.Repository
                    .GetWhere(n => n.SectionId == section.Id)
                    .CountAsync();

            }

            ViewBag.AllNewsCount = await _news.Repository.GetAll().CountAsync();


            return View(sections);
        }


    }

}
