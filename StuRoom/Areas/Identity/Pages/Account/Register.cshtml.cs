// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using StuRoom.Models;

namespace StuRoom.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IUserEmailStore<ApplicationUser> _emailStore;
    private readonly ILogger<RegisterModel> _logger;
    private readonly IEmailSender _emailSender;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _logger = logger;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ReturnUrl { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, ErrorMessage = "{0} phải có ít nhất {2} và tối đa {1} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }

        /// <summary>Chỉ nhận "Tenant" hoặc "Landlord".</summary>
        [Required(ErrorMessage = "Vui lòng chọn loại tài khoản.")]
        [RegularExpression("^(Tenant|Landlord)$", ErrorMessage = "Loại tài khoản không hợp lệ.")]
        [Display(Name = "Loại tài khoản")]
        public string Role { get; set; }
    }

    public async Task OnGetAsync(string returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                FullName   = Input.FullName.Trim(),
                // Tenant không cần duyệt; Landlord cần Admin duyệt → IsApproved = false
                IsApproved = Input.Role == "Tenant"
            };

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Tài khoản mới được tạo: {Email} — Role: {Role}", Input.Email, Input.Role);

                // Gán role
                await _userManager.AddToRoleAsync(user, Input.Role);

                // Gửi email xác nhận
                var userId = await _userManager.GetUserIdAsync(user);
                var code   = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId, code, returnUrl },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Xác nhận tài khoản StuRoom",
                    $"Vui lòng xác nhận tài khoản của bạn bằng cách <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>nhấn vào đây</a>.");

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    TempData["Success"] = "Đăng ký thành công! Vui lòng kiểm tra hộp thư email để xác nhận tài khoản.";
                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
                }

                await _signInManager.SignInAsync(user, isPersistent: false);

                // Landlord chưa được duyệt → về trang chờ
                if (Input.Role == "Landlord")
                {
                    TempData["Success"] = "Đăng ký thành công! Tài khoản Chủ trọ đang chờ ban quản trị phê duyệt.";
                    return RedirectToAction("PendingApproval", "AccountStatus");
                }

                TempData["Success"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với StuRoom.";
                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }

    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
            throw new NotSupportedException("UI mặc định yêu cầu user store hỗ trợ email.");
        return (IUserEmailStore<ApplicationUser>)_userStore;
    }
}
