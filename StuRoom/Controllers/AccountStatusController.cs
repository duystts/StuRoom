using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StuRoom.Models;

namespace StuRoom.Controllers;

[Authorize]
public class AccountStatusController(UserManager<ApplicationUser> userManager) : Controller
{
    /// <summary>
    /// Hiển thị khi Landlord đã đăng nhập nhưng chưa được Admin duyệt.
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> PendingApproval()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(User);
            return View(user);
        }
        return View(null);
    }

    /// <summary>
    /// Trang từ chối truy cập chung (dùng làm AccessDeniedPath trong cookie options).
    /// </summary>
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
