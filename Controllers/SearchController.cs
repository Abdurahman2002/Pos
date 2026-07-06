using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Search;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    public class SearchController : Controller
    {
        private const int MaxPerGroup = 15;

        private readonly AppDbContext _context;

        public SearchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q)
        {
            var vm = new SearchResultsVM
            {
                Query = q?.Trim() ?? string.Empty,
                // Invoice numbers can expose other cashiers' sales; only full-view roles search them.
                CanSearchInvoices = User.IsInRole("Admin") || User.IsInRole("Prog") || User.IsInRole("SalesManager")
            };

            var term = vm.Query;
            if (term.Length < 2)
                return View(vm);

            vm.Items = await _context.Set<Item>()
                .AsNoTracking()
                .Where(i => i.Name.Contains(term) || (i.Barcode != null && i.Barcode.Contains(term)))
                .OrderBy(i => i.Name)
                .Take(MaxPerGroup)
                .Select(i => new SearchItemRow
                {
                    Id = i.Id,
                    Name = i.Name,
                    Barcode = i.Barcode,
                    Category = i.Category != null ? i.Category.Name : null
                })
                .ToListAsync();

            vm.Customers = await _context.Set<Customer>()
                .AsNoTracking()
                .Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)))
                .OrderBy(c => c.Name)
                .Take(MaxPerGroup)
                .Select(c => new SearchPartyRow { Id = c.Id, Name = c.Name, Phone = c.Phone })
                .ToListAsync();

            vm.Suppliers = await _context.Set<Supplier>()
                .AsNoTracking()
                .Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)))
                .OrderBy(s => s.Name)
                .Take(MaxPerGroup)
                .Select(s => new SearchPartyRow { Id = s.Id, Name = s.Name, Phone = s.Phone })
                .ToListAsync();

            if (vm.CanSearchInvoices)
            {
                vm.Invoices = await _context.Set<SalesInvoice>()
                    .AsNoTracking()
                    .Include(i => i.Customer)
                    .Where(i => i.Number.Contains(term))
                    .OrderByDescending(i => i.Created)
                    .Take(MaxPerGroup)
                    .Select(i => new SearchInvoiceRow
                    {
                        Id = i.Id,
                        Number = i.Number,
                        CustomerName = i.Customer != null ? i.Customer.Name : null,
                        TotalDinar = i.TotalDinar,
                        InvoiceDate = i.InvoiceDate
                    })
                    .ToListAsync();
            }

            return View(vm);
        }
    }
}
