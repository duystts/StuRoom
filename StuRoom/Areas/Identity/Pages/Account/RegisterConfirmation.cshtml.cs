// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using StuRoom.Models;

namespace StuRoom.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterConfirmationModel(
    UserManager<ApplicationUser> userManager) : PageModel
{
    public string Email { get; set; }
    public bool IsLandlord { get; set; }

    /// <summary>
    /// Chỉ hiển thị link xác nhận trong môi trường dev (chưa cấu hình SMTP thật).
    /// Khi Task 17 (Email service) hoàn thành, đặt lại thành false.
    /// </summary>
    public bool DisplayConfirmAccountLink { get; set; }
    public string EmailConfirmationUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(string email, string returnUrl = null)
    {
        if (email == null)
            return RedirectToPage("/Index");

        returnUrl ??= Url.Content("~/");

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
            return NotFound($"Không tìm thấy user với email '{email}'.");

        Email = email;
        IsLandlord = await userManager.IsInRoleAsync(user, "Landlord");

        // TODO: Gỡ bỏ block này sau khi Task 17 (SMTP) hoàn thành
        DisplayConfirmAccountLink = true;
        if (DisplayConfirmAccountLink)
        {
            var userId = await userManager.GetUserIdAsync(user);
            var code   = await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            EmailConfirmationUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl },
                protocol: Request.Scheme);
        }

        return Page();
    }
}
