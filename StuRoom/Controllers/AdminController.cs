using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Models.ViewModels;
using StuRoom.Services;

namespace StuRoom.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    ApplicationDbContext db,
    IAuditLogService auditLog) : Controller
{
    // ════════════════════════════════════════════════════════
    // DASHBOARD  (Task 33)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Dashboard()
    {
        ViewData["ActiveMenu"] = "AdminDashboard";

        var landlords = await userManager.GetUsersInRoleAsync("Landlord");
        var tenants   = await userManager.GetUsersInRoleAsync("Tenant");

        // ── System revenue chart — last 12 months ───────────────────
        var now = DateTime.Now;
        var since = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        var payments = await db.Payments
            .Where(p => p.PaymentDate >= since)
            .Select(p => new { p.PaymentDate.Year, p.PaymentDate.Month, p.Amount })
            .ToListAsync();

        var revenueLabels = new List<string>();
        var revenueData   = new List<decimal>();
        for (int i = 11; i >= 0; i--)
        {
            var m = now.AddMonths(-i);
            revenueLabels.Add($"{m.Month:D2}/{m.Year}");
            revenueData.Add(payments
                .Where(p => p.Year == m.Year && p.Month == m.Month)
                .Sum(p => p.Amount));
        }

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

            RevenueLabels   = revenueLabels,
            RevenueData     = revenueData
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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            review.IsApproved ? "ApproveReview" : "HideReview",
            "RoomReview",
            review.Id.ToString(),
            $"Admin đã {(review.IsApproved ? "duyệt/hiển thị" : "ẩn")} review #{review.Id} của user {review.ReviewerId}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            "DeleteReview",
            "RoomReview",
            id.ToString(),
            $"Admin đã xóa review #{id} của reviewer {review.ReviewerId}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            "ApproveLandlord",
            "ApplicationUser",
            user.Id,
            $"Admin đã duyệt tài khoản chủ trọ: {user.FullName} ({user.Email})",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            "RejectLandlord",
            "ApplicationUser",
            user.Id,
            $"Admin đã từ chối tài khoản chủ trọ: {user.FullName} ({user.Email}). Lý do: {reasonText}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        var amenity = new Amenity { Name = name.Trim(), IconClass = iconClass.Trim() };
        db.Amenities.Add(amenity);
        await db.SaveChangesAsync();

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            "CreateAmenity",
            "Amenity",
            amenity.Id.ToString(),
            $"Admin đã tạo tiện ích mới: {amenity.Name} (Icon: {amenity.IconClass})",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            "EditAmenity",
            "Amenity",
            amenity.Id.ToString(),
            $"Admin đã sửa tiện ích #{amenity.Id}: {amenity.Name} (Icon: {amenity.IconClass})",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            "DeleteAmenity",
            "Amenity",
            id.ToString(),
            $"Admin đã xóa tiện ích #{id}: {amenity.Name}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

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

        await auditLog.LogAsync(
            userManager.GetUserId(User),
            actionType == "block" ? "BlockRoomByReport" : "DismissReport",
            "RoomReport",
            id.ToString(),
            $"Admin đã xử lý báo cáo vi phạm #{id}. Hành động: {(actionType == "block" ? "Khóa phòng trọ" : "Bỏ qua báo cáo")}. Phản hồi: {report.AdminFeedback}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return RedirectToAction(nameof(Reports), new { status = actionType == "block" ? "resolved" : "dismissed" });
    }

    // ════════════════════════════════════════════════════════
    // AUDIT LOGS VIEW
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> AuditLogs(string? search, string? actionFilter, int page = 1)
    {
        ViewData["ActiveMenu"] = "AuditLogs";
        ViewData["Search"]     = search;
        ViewData["ActionFilter"] = actionFilter;

        var query = db.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => 
                a.Description.Contains(search) || 
                (a.UserEmail != null && a.UserEmail.Contains(search)) || 
                (a.UserFullName != null && a.UserFullName.Contains(search)) ||
                a.Action.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        // Get unique action types for filter dropdown
        var actionTypes = await db.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
        ViewBag.ActionTypes = actionTypes;

        // Pagination
        const int pageSize = 20;
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (page < 1) page = 1;

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages  = totalPages;
        ViewBag.TotalItems  = totalItems;

        return View(logs);
    }

    // ════════════════════════════════════════════════════════
    // TESTING/TRIGGER FOR VIEWING REMINDERS
    // ════════════════════════════════════════════════════════

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerViewingReminders()
    {
        var now = DateTime.Now;
        var cutoff = now.AddHours(24);

        var upcomingViewings = await db.ViewingRequests
            .Include(v => v.Tenant)
            .Include(v => v.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Where(v => !v.ReminderSent
                && (v.Status == ViewingStatus.Confirmed || v.Status == ViewingStatus.Pending)
                && (v.ConfirmedTime ?? v.ProposedTime) > now
                && (v.ConfirmedTime ?? v.ProposedTime) <= cutoff)
            .ToListAsync();

        if (upcomingViewings.Count == 0)
        {
            TempData["Warning"] = "Không tìm thấy lịch hẹn xem phòng nào sắp diễn ra trong vòng 24h tới để gửi nhắc nhở.";
            return RedirectToAction(nameof(Dashboard));
        }

        int sentCount = 0;
        foreach (var v in upcomingViewings)
        {
            var scheduledTime = v.ConfirmedTime ?? v.ProposedTime;
            var roomInfo = $"{v.Room.RoomNumber} — {v.Room.Building.Name}";
            var landlord = v.Room.Building.Landlord;

            // Send to Tenant
            try
            {
                await emailSender.SendEmailAsync(
                    v.Tenant.Email!,
                    "Nhắc lịch xem phòng ngày mai — StuRoom",
                    ViewingReminderService.BuildTenantReminderHtml(v.Tenant.FullName, roomInfo,
                        v.Room.Building.Address, scheduledTime, landlord.FullName));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi gửi mail nhắc tenant: {ex.Message}";
            }

            // Send to Landlord
            try
            {
                await emailSender.SendEmailAsync(
                    landlord.Email!,
                    "Nhắc lịch khách xem phòng ngày mai — StuRoom",
                    ViewingReminderService.BuildLandlordReminderHtml(landlord.FullName, roomInfo,
                        scheduledTime, v.Tenant.FullName));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi gửi mail nhắc landlord: {ex.Message}";
            }

            // Mark as sent
            v.ReminderSent = true;
            sentCount++;
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã kích hoạt gửi nhắc hẹn thành công! Đã gửi mail nhắc nhở cho {sentCount} lịch hẹn sắp diễn ra.";
        return RedirectToAction(nameof(Dashboard));
    }
}
