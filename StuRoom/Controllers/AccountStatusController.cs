using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StuRoom.Controllers;

[Authorize]
public class AccountStatusController : Controller
{
    /// <summary>
    /// Hiển thị khi Landlord đã đăng nhập nhưng chưa được Admin duyệt.
    /// </summary>
    [AllowAnonymous]
    public IActionResult PendingApproval() => View();

    /// <summary>
    /// Trang từ chối truy cập chung (dùng làm AccessDeniedPath trong cookie options).
    /// </summary>
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
