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
    // DASHBOARD  (Task 33)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Dashboard()
    {
        ViewData["ActiveMenu"] = "AdminDashboard";

        var landlords = await userManager.GetUsersInRoleAsync("Landlord");
        var tenants   = await userManager.GetUsersInRoleAsync("Tenant");

        var vm = new AdminDashboardViewModel
        {
            TotalRooms      = await db.Rooms.CountAsync(),
            AvailableRooms  = await db.Rooms.CountAsync(r => r.Status == RoomStatus.Available),
            OccupiedRooms   = await db.Rooms.CountAsync(r => r.Status == RoomStatus.Occupied),

            TotalUsers      = await db.Users.CountAsync(),
            TotalTenants    = tenants.Count,
            TotalLandlords  = landlords.Count,
            ActiveLandlords = landlords.Count(u => u.IsApproved),
            PendingLandlords = landlords.Count(u =>
                !u.IsApproved &&
                !(u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow)),

            TotalContracts  = await db.Contracts.CountAsync(),
            ActiveContracts = await db.Contracts.CountAsync(c => c.Status == ContractStatus.Active),
            TotalRevenue    = await db.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0,

            PendingReviews  = await db.RoomReviews.CountAsync(r => !r.IsApproved),
        };

        return View(vm);
    }

    // ════════════════════════════════════════════════════════
    // REVIEW MODERATION  (Task 29)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> ReviewModeration(string? filter)
    {
        ViewData["ActiveMenu"] = "ReviewModeration";
        ViewData["Filter"]     = filter ?? "all";

        var query = db.RoomReviews
            .Include(r => r.Reviewer)
            .Include(r => r.Room)
                .ThenInclude(r => r.Building)
            .AsQueryable();

        query = filter switch
        {
            "approved" => query.Where(r => r.IsApproved),
            "hidden"   => query.Where(r => !r.IsApproved),
            _          => query
        };

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(reviews);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReview(int id, string? returnFilter)
    {
        var review = await db.RoomReviews.FindAsync(id);
        if (review == null) return NotFound();

        review.IsApproved = !review.IsApproved;
        await db.SaveChangesAsync();

        TempData["Success"] = review.IsApproved
            ? "Đã hiển thị review."
            : "Đã ẩn review.";

        return RedirectToAction(nameof(ReviewModeration), new { filter = returnFilter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id, string? returnFilter)
    {
        var review = await db.RoomReviews.FindAsync(id);
        if (review == null) return NotFound();

        db.RoomReviews.Remove(review);
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã xoá review.";
        return RedirectToAction(nameof(ReviewModeration), new { filter = returnFilter });
    }

    // ════════════════════════════════════════════════════════
    // LANDLORD APPROVAL  (Task 31)
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
                Id              = u.Id,
                FullName        = u.FullName,
                Email           = u.Email ?? "",
                AvatarUrl       = u.AvatarUrl,
                CreatedAt       = DateTime.UtcNow,
                RejectionReason = u.RejectionReason
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
        user.RejectionReason = null;
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

        user.RejectionReason = reason;
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await userManager.UpdateAsync(user);

        var reasonText = string.IsNullOrWhiteSpace(reason) ? "không đáp ứng yêu cầu" : reason;
        await emailSender.SendEmailAsync(user.Email!,
            "Tài khoản StuRoom không được phê duyệt",
            $"Xin chào <strong>{user.FullName}</strong>,<br><br>" +
            $"Tài khoản chủ trọ của bạn <strong>không được phê duyệt</strong> vì: {reasonText}.");

        TempData["Warning"] = $"Đã từ chối tài khoản <strong>{user.FullName}</strong>.";
        return RedirectToAction(nameof(LandlordApproval));
    }

    // ════════════════════════════════════════════════════════
    // AMENITY CRUD  (Task 6)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Amenities()
    {
        ViewData["ActiveMenu"] = "Amenities";
        var list = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAmenity(string name, string iconClass)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên tiện ích không được để trống.";
            return RedirectToAction(nameof(Amenities));
        }

        db.Amenities.Add(new Amenity { Name = name.Trim(), IconClass = iconClass.Trim() });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm tiện ích <strong>{name.Trim()}</strong>.";
        return RedirectToAction(nameof(Amenities));
    }

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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAmenity(int id)
    {
        var amenity = await db.Amenities.FindAsync(id);
        if (amenity == null) return NotFound();

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

    // ════════════════════════════════════════════════════════
    // ROOM VIOLATION REPORTS
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Reports(string? status)
    {
        ViewData["ActiveMenu"] = "Reports";
        ViewData["StatusFilter"] = status ?? "pending";

        var query = db.RoomReports
            .Include(r => r.Room).ThenInclude(r => r.Building)
            .Include(r => r.Reporter)
            .AsQueryable();

        query = status switch
        {
            "resolved"  => query.Where(r => r.Status == ReportStatus.Resolved),
            "dismissed" => query.Where(r => r.Status == ReportStatus.Dismissed),
            _           => query.Where(r => r.Status == ReportStatus.Pending)
        };

        var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HandleReport(int id, string actionType, string? feedback)
    {
        var report = await db.RoomReports
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (report == null) return NotFound();

        report.AdminFeedback = feedback?.Trim();
        report.HandledAt = DateTime.UtcNow;

        if (actionType == "block")
        {
            report.Status = ReportStatus.Resolved;
            report.Room.Status = RoomStatus.Inactive; // Block room
            TempData["Success"] = "Đã khoá phòng và giải quyết báo cáo.";
        }
        else if (actionType == "dismiss")
        {
            report.Status = ReportStatus.Dismissed;
            TempData["Success"] = "Đã bỏ qua báo cáo.";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Reports), new { status = actionType == "block" ? "resolved" : "dismissed" });
    }
}
