using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Classes.Authorization
{
    public sealed class ApprovedUserHandler : AuthorizationHandler<ApprovedUserRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ApprovedUserHandler(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApprovedUserRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
                return;

            if (context.User.IsInRole("Prog") || context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return;
            }

            var userId = _userManager.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var user = await _userManager.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.Approval, HasEmployee = u.Employee != null })
                .FirstOrDefaultAsync();

            if (user?.Approval == true && user.HasEmployee)
            {
                context.Succeed(requirement);
            }
        }
    }
}
