using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Classes;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Inventory;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    [Authorize(Roles = "Admin,Prog,SalesManager")]
    public class PricingController : Controller
    {
        private readonly AppDbContext _context;

        public PricingController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.CanManagePricingPolicy = CanManagePricingPolicy();
            var vm = await BuildViewModelAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(PricingPolicyVM vm)
        {
            var canManagePricingPolicy = CanManagePricingPolicy();
            ViewBag.CanManagePricingPolicy = canManagePricingPolicy;
            vm.Items = vm.Items?.Where(i => i != null).ToList() ?? new List<ItemSalePriceVM>();

            if (!vm.Items.Any())
            {
                ModelState.AddModelError(nameof(vm.Items), "لا توجد أصناف لتحديث الأسعار.");
            }

            if (vm.Items.Any(i => i.DefaultSalePriceLyd.HasValue && i.DefaultSalePriceLyd.Value < 0))
            {
                ModelState.AddModelError(nameof(vm.Items), "سعر البيع لا يمكن أن يكون سالبا.");
            }

            if (!ModelState.IsValid)
            {
                var fresh = await BuildViewModelAsync();
                vm.Items = fresh.Items;
                return View(vm);
            }

            var settings = await _context.Set<InventorySettings>().FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new InventorySettings
                {
                    BaseCurrencyCode = "EUR",
                    DinarCurrencyCode = "LYD",
                    IsSingleWarehouseMode = true,
                    SingleWarehouseName = "Main Warehouse",
                    DefaultRateSource = "Manual",
                    MaxCashierDiscountPercent = canManagePricingPolicy
                        ? Math.Clamp(vm.MaxCashierDiscountPercent, 0m, 100m)
                        : 10m,
                    CommissionSalesStepLyd = canManagePricingPolicy && vm.CommissionSalesStepLyd > 0
                        ? vm.CommissionSalesStepLyd
                        : 1000m,
                    CommissionAmountPerStepLyd = canManagePricingPolicy
                        ? Math.Max(0m, vm.CommissionAmountPerStepLyd)
                        : 10m
                };
                _context.Set<InventorySettings>().Add(settings);
            }
            else if (canManagePricingPolicy)
            {
                settings.MaxCashierDiscountPercent = Math.Clamp(vm.MaxCashierDiscountPercent, 0m, 100m);
                settings.CommissionSalesStepLyd = vm.CommissionSalesStepLyd > 0 ? vm.CommissionSalesStepLyd : 1000m;
                settings.CommissionAmountPerStepLyd = Math.Max(0m, vm.CommissionAmountPerStepLyd);
            }

            var postedIds = vm.Items.Select(i => i.ItemId).Distinct().ToList();
            var items = await _context.Set<Item>()
                .Where(i => postedIds.Contains(i.Id))
                .ToListAsync();

            var map = vm.Items.ToDictionary(i => i.ItemId, i => i.DefaultSalePriceLyd);
            foreach (var item in items)
            {
                if (map.TryGetValue(item.Id, out var price))
                {
                    if (canManagePricingPolicy)
                    {
                        item.DefaultSalePriceLyd = price;
                    }
                    else
                    {
                        var hasNoPrice = !item.DefaultSalePriceLyd.HasValue || item.DefaultSalePriceLyd.Value <= 0;
                        var postedPrice = price.GetValueOrDefault();
                        if (hasNoPrice && postedPrice > 0)
                        {
                            item.DefaultSalePriceLyd = postedPrice;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = canManagePricingPolicy
                ? "تم حفظ سياسة التسعير وحد الخصم بنجاح."
                : "تم حفظ أسعار الأصناف غير المسعرة بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<PricingPolicyVM> BuildViewModelAsync()
        {
            var settings = await _context.Set<InventorySettings>()
                .AsNoTracking()
                .FirstOrDefaultAsync();

            var items = await _context.Set<Item>()
                .AsNoTracking()
                .Include(i => i.Category)
                .OrderBy(i => i.Name)
                .Select(i => new ItemSalePriceVM
                {
                    ItemId = i.Id,
                    ItemName = i.Name,
                    CategoryName = i.Category != null ? i.Category.Name : null,
                    DefaultSalePriceLyd = i.DefaultSalePriceLyd
                })
                .ToListAsync();

            return new PricingPolicyVM
            {
                MaxCashierDiscountPercent = settings?.MaxCashierDiscountPercent ?? 10m,
                CommissionSalesStepLyd = settings?.CommissionSalesStepLyd > 0 ? settings.CommissionSalesStepLyd : 1000m,
                CommissionAmountPerStepLyd = settings?.CommissionAmountPerStepLyd ?? 10m,
                Items = items
            };
        }

        private bool CanManagePricingPolicy()
        {
            return User.IsInRole("Admin") || User.IsInRole("Prog") || User.IsInRole("SalesManager");
        }
    }
}
