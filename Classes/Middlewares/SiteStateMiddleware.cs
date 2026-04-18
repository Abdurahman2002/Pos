using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;

namespace NewsApp2.Classes.Middlewares
{
    public class SiteStateMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SiteStateMiddleware(RequestDelegate next,
                                   IMemoryCache cache,
                                   IHttpContextAccessor httpContextAccessor
                                   /*IUnitOfWork<SiteState> siteState*/) // لا يمكن حقنها مباشرة
        {
            _next = next;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            //--------------Inject----------------------
            var _siteState = context.RequestServices.GetRequiredService<IUnitOfWork<SiteState>>();
            //------------------------------------------

            //  جلب قيمة SiteState من الكاش أو من الداتا بيز
            var siteState = await _cache.GetOrCreateAsync("SiteState", async entry =>
            {
                // مدة الكاش 
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(360);

                // أول مرة فقط يتم عمل استعلام DB
                return await _siteState.Repository.GetAll().FirstOrDefaultAsync();
            });


            if (siteState != null && !siteState.State)
            {

                var path = context.Request.Path.Value?.ToLower();

                if (path != "/account/login" &&
                    path != "/home/closing")
                {
                    //--------------Inject----------------------
                    var signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
                    var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    //-----------------------------------------
                    bool isSigned = signInManager.IsSignedIn(context.User);
                    bool isProg = false;

                    if (isSigned)
                    {
                        var appUser = await userManager.GetUserAsync(context.User);
                        if (appUser != null)
                            isProg = await userManager.IsInRoleAsync(appUser, "Prog");
                    }

                    if (!isSigned || (isSigned && !isProg))
                    {
                        context.Response.Redirect("/Home/Closing");
                        return;
                    }

                }
            }

            //  متابعة PipeLine الطبيعي
            await _next(context);
        }
    }

}
