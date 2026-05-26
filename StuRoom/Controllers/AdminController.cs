using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using StuRoom.Models;
using StuRoom.Models.ViewModels;

namespace StuRoom.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender) : Controller
{
    // ── GET /Admin/LandlordApproval ────────────────────────────────────
    public async Task<IActionResult> LandlordApproval()
    {
        ViewData["ActiveMenu"] = "ApproveLandlord";

        var landlords = await userManager.GetUsersInRoleAsync("Landlord");

        var vm = new LandlordApprovalViewModel();

        foreach (var u in landlords.OrderByDescending(u => u.LockoutEnd).ThenBy(u => u.FullName))
        {
            var item = new LandlordItem
            {
                Id        = u.Id,
                FullName  = u.FullName,
                Email     = u.Email ?? "",
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow
                    ? DateTime.MinValue  // bị từ chối
                    : DateTime.UtcNow
            };

            if (!u.IsApproved && !(u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow))
                vm.Pending.Add(item);
            else if (u.IsApproved)
                vm.Approved.Add(item);
            else
                vm.Rejected.Add(item);
        }

        return View(vm);
    }

    // ── POST /Admin/ApproveLandlord ────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLandlord(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsApproved = true;
        // Gỡ lockout nếu có
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.UpdateAsync(user);

        // Gửi email thông báo (no-op trong dev, hoạt động khi Task 17 xong)
        await emailSender.SendEmailAsync(
            user.Email!,
            "Tài khoản StuRoom đã được duyệt",
            $"Xin chào <strong>{user.FullName}</strong>,<br><br>" +
            "Tài khoản chủ trọ của bạn trên StuRoom đã được Admin phê duyệt.<br>" +
            "Bạn có thể đăng nhập và bắt đầu đăng phòng ngay bây giờ.");

        TempData["Success"] = $"Đã duyệt tài khoản <strong>{user.FullName}</strong>.";
        return RedirectToAction(nameof(LandlordApproval));
    }

    // ── POST /Admin/RejectLandlord ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectLandlord(string id, string? reason)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Khóa tài khoản vĩnh viễn (DateTimeOffset.MaxValue)
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        // Gửi email thông báo từ chối
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "không đáp ứng yêu cầu" : reason;
        await emailSender.SendEmailAsync(
            user.Email!,
            "Tài khoản StuRoom không được phê duyệt",
            $"Xin chào <strong>{user.FullName}</strong>,<br><br>" +
            $"Rất tiếc, tài khoản chủ trọ của bạn <strong>không được phê duyệt</strong> " +
            $"vì lý do: {reasonText}.<br><br>" +
            "Nếu bạn có thắc mắc, vui lòng liên hệ Admin.");

        TempData["Warning"] = $"Đã từ chối tài khoản <strong>{user.FullName}</strong>.";
        return RedirectToAction(nameof(LandlordApproval));
    }
}
