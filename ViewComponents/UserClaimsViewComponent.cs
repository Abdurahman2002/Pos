using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels.Identity;

namespace NewsApp2.ViewComponents
{

    [ViewComponent(Name = "UserClaims")]
    public class UserClaimsViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserClaimsViewComponent(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return View("NotFound");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return View("NotFound");


            var userClaims = await _userManager.GetClaimsAsync(user);

            var model = new UserClaimListVM
            {
                UserId = userId
            };


            foreach (var claim in StaticClaims.All)
            {
                model.Claims.Add(new UserClaimVM
                {
                    ClaimType = claim.Type,
                    IsSelected = userClaims.Any(c => c.Type == claim.Type && c.Value == "true")
                });
            }

            return View(model);
        }

    }
}
