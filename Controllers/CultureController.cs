using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace NewsApp2.Controllers
{
    public class CultureController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string? returnUrl = null)
        {
            var allowedCultures = new[] { "ar-IQ", "en-GB" };
            var selectedCulture = allowedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)
                ? culture
                : "ar-IQ";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selectedCulture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(2),
                    IsEssential = true,
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax
                });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "StockBalances");
        }
    }
}
