// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StuRoom.Models;

namespace StuRoom.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager  = userManager;
        _signInManager = signInManager;
    }

    public string Email { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [Url(ErrorMessage = "URL ảnh đại diện không hợp lệ.")]
        [StringLength(500)]
        [Display(Name = "Ảnh đại diện (URL)")]
        public string AvatarUrl { get; set; }
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Email = await _userManager.GetEmailAsync(user);
        Input = new InputModel
        {
            FullName    = user.FullName,
            PhoneNumber = await _userManager.GetPhoneNumberAsync(user),
            AvatarUrl   = user.AvatarUrl
        };
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Không tìm thấy user ID '{_userManager.GetUserId(User)}'.");

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Không tìm thấy user ID '{_userManager.GetUserId(User)}'.");

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        // Cập nhật PhoneNumber qua Identity
        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (Input.PhoneNumber != phoneNumber)
        {
            var setPhone = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!setPhone.Succeeded)
            {
                StatusMessage = "Lỗi: không thể cập nhật số điện thoại.";
                return RedirectToPage();
            }
        }

        // Cập nhật FullName và AvatarUrl trực tiếp vào ApplicationUser
        var changed = false;

        if (Input.FullName?.Trim() != user.FullName)
        {
            user.FullName = Input.FullName?.Trim();
            changed = true;
        }

        if (Input.AvatarUrl != user.AvatarUrl)
        {
            user.AvatarUrl = Input.AvatarUrl;
            changed = true;
        }

        if (changed)
        {
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                StatusMessage = "Lỗi: không thể cập nhật thông tin.";
                return RedirectToPage();
            }
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Cập nhật thông tin thành công.";
        return RedirectToPage();
    }
}
