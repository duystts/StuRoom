using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using StuRoom.Models;

namespace StuRoom.Authorization;

public class LandlordApprovedHandler(UserManager<ApplicationUser> userManager)
    : AuthorizationHandler<LandlordApprovedRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LandlordApprovedRequirement requirement)
    {
        // Nếu không phải Landlord thì không liên quan — cho qua
        if (!context.User.IsInRole("Landlord"))
        {
            context.Succeed(requirement);
            return;
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user?.IsApproved == true)
            context.Succeed(requirement);
        // else: context.Fail() ngầm định — sẽ redirect về AccessDenied
    }
}
