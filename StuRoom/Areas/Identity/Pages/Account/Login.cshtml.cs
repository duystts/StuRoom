// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StuRoom.Models;

namespace StuRoom.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager  = userManager;
        _logger       = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; set; }
    public string ReturnUrl { get; set; }

    [TempData]
    public string ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
            ModelState.AddModelError(string.Empty, ErrorMessage);

        returnUrl ??= Url.Content("~/");
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (!ModelState.IsValid) return Page();

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("Đăng nhập thành công: {Email}", Input.Email);
            TempData["Success"] = "Đăng nhập thành công! Chào mừng bạn quay trở lại.";

            // Landlord chưa duyệt → chuyển về trang chờ
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user != null
                && await _userManager.IsInRoleAsync(user, "Landlord")
                && !user.IsApproved)
            {
                return RedirectToAction("PendingApproval", "AccountStatus");
            }

            return LocalRedirect(returnUrl);
        }

        if (result.RequiresTwoFactor)
            return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, Input.RememberMe });

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Tài khoản bị khoá: {Email}", Input.Email);
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return Page();
    }
}
