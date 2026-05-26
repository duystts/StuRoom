using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Models.ViewModels;

namespace StuRoom.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    ApplicationDbContext db) : Controller
{
    // ════════════════════════════════════════════════════════
    // LANDLORD APPROVAL
    // ════════════════════════════════════════════════════════

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
                CreatedAt = DateTime.UtcNow
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLandlord(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsApproved = true;
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.UpdateAsync(user);

        await emailSender.SendEmailAsync(user.Email!,
            "Tài khoản StuRoom đã được duyệt",
            $"Xin chào <strong>{user.FullName}</strong>,<br><br>" +
            "Tài khoản chủ trọ của bạn đã được Admin phê duyệt. Bạn có thể đăng nhập ngay.");

        TempData["Success"] = $"Đã duyệt tài khoản <strong>{user.FullName}</strong>.";
        return RedirectToAction(nameof(LandlordApproval));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectLandlord(string id, string? reason)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        var reasonText = string.IsNullOrWhiteSpace(reason) ? "không đáp ứng yêu cầu" : reason;
        await emailSender.SendEmailAsync(user.Email!,
            "Tài khoản StuRoom không được phê duyệt",
            $"Xin chào <strong>{user.FullName}</strong>,<br><br>" +
            $"Tài khoản chủ trọ của bạn <strong>không được phê duyệt</strong> vì: {reasonText}.");

        TempData["Warning"] = $"Đã từ chối tài khoản <strong>{user.FullName}</strong>.";
        return RedirectToAction(nameof(LandlordApproval));
    }

    // ════════════════════════════════════════════════════════
    // AMENITY CRUD
    // ════════════════════════════════════════════════════════

    // GET /Admin/Amenities
    public async Task<IActionResult> Amenities()
    {
        ViewData["ActiveMenu"] = "Amenities";
        var list = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(list);
    }

    // POST /Admin/CreateAmenity
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAmenity(string name, string iconClass)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên tiện ích không được để trống.";
            return RedirectToAction(nameof(Amenities));
        }

        db.Amenities.Add(new Amenity
        {
            Name      = name.Trim(),
            IconClass = iconClass.Trim()
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm tiện ích <strong>{name.Trim()}</strong>.";
        return RedirectToAction(nameof(Amenities));
    }

    // POST /Admin/EditAmenity
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAmenity(int id, string name, string iconClass)
    {
        var amenity = await db.Amenities.FindAsync(id);
        if (amenity == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên tiện ích không được để trống.";
            return RedirectToAction(nameof(Amenities));
        }

        amenity.Name      = name.Trim();
        amenity.IconClass = iconClass.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật tiện ích <strong>{amenity.Name}</strong>.";
        return RedirectToAction(nameof(Amenities));
    }

    // POST /Admin/DeleteAmenity
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAmenity(int id)
    {
        var amenity = await db.Amenities.FindAsync(id);
        if (amenity == null) return NotFound();

        // Kiểm tra xem tiện ích có đang được dùng không
        var inUse = await db.RoomAmenities.AnyAsync(ra => ra.AmenityId == id);
        if (inUse)
        {
            TempData["Error"] = $"Không thể xoá <strong>{amenity.Name}</strong> vì đang được gán cho phòng.";
            return RedirectToAction(nameof(Amenities));
        }

        db.Amenities.Remove(amenity);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã xoá tiện ích <strong>{amenity.Name}</strong>.";
        return RedirectToAction(nameof(Amenities));
    }
}
