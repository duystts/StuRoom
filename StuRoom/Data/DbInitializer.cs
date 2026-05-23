using Microsoft.AspNetCore.Identity;
using StuRoom.Models;

namespace StuRoom.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // ── 1. Seed roles ──────────────────────────────────────────
        string[] roles = ["Admin", "Landlord", "Tenant"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── 2. Seed Admin account ──────────────────────────────────
        const string adminEmail = "admin@sturoom.com";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName    = adminEmail,
                Email       = adminEmail,
                FullName    = "System Admin",
                EmailConfirmed = true,   // bỏ qua bước xác nhận email
                IsApproved  = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@123456");

            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
