using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewsApp2.Classes;
using NewsApp2.Models.Services;

namespace NewsApp2.Controllers
{
    [ViewLayout("_LayoutDashboard")]
    [Authorize(Policy = "InventoryCreatePolicy")]
    [Authorize(Policy = "ApprovedUserPolicy")]
    [Authorize(Policy = "NotCashierPolicy")]
    public class PartyAccountsController : Controller
    {
        private readonly AccountBalanceService _accountBalanceService;

        public PartyAccountsController(AccountBalanceService accountBalanceService)
        {
            _accountBalanceService = accountBalanceService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var vm = await _accountBalanceService.GetSummaryAsync(search, 10);
            return View(vm);
        }
    }
}
