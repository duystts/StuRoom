using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Services;

namespace StuRoom.Controllers;

[Authorize(Policy = "TenantOnly")]
public class ViewingRequestsController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    INotificationService notifier,
    IEmailSender emailSender) : Controller
{
    private string CurrentUserId => userManager.GetUserId(User)!;

    // ════════════════════════════════════════════════════════
    // TENANT — danh sách lịch hẹn của mình  (Task 15)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Index()
    {
        var requests = await db.ViewingRequests
            .Include(v => v.Room).ThenInclude(r => r.Building)
            .Include(v => v.Room.Images)
            .Where(v => v.TenantId == CurrentUserId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    // ── Tạo lịch hẹn ─────────────────────────────────────────

    public async Task<IActionResult> Create(int roomId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Status == RoomStatus.Available);

        if (room == null) return NotFound();

        // Check if tenant already has a pending/confirmed request for this room
        var existing = await db.ViewingRequests.AnyAsync(v =>
            v.RoomId == roomId &&
            v.TenantId == CurrentUserId &&
            (v.Status == ViewingStatus.Pending || v.Status == ViewingStatus.Confirmed));

        if (existing)
        {
            TempData["Error"] = "Bạn đã có lịch hẹn xem phòng này đang chờ xử lý.";
            return RedirectToAction("Detail", "Rooms", new { id = roomId });
        }

        ViewBag.Room = room;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int roomId, DateTime proposedTime, string? tenantNote)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Status == RoomStatus.Available);

        if (room == null) return NotFound();

        if (proposedTime <= DateTime.Now)
        {
            ViewBag.Room  = room;
            ModelState.AddModelError("", "Thời gian đề xuất phải ở tương lai.");
            return View();
        }

        db.ViewingRequests.Add(new ViewingRequest
        {
            RoomId       = roomId,
            TenantId     = CurrentUserId,
            ProposedTime = proposedTime,
            TenantNote   = string.IsNullOrWhiteSpace(tenantNote) ? null : tenantNote.Trim(),
            Status       = ViewingStatus.Pending
        });
        await db.SaveChangesAsync();

        // Task 30 — notify Landlord
        await notifier.SendAsync(room.Building.LandlordId, NotificationType.NewViewingRequest,
            "Yêu cầu xem phòng mới",
            $"Tenant muốn xem phòng {room.RoomNumber} — {proposedTime:dd/MM HH:mm}.",
            "ViewingRequest", db.ViewingRequests.Local.Last().Id);

        TempData["Success"] = "Đã gửi yêu cầu xem phòng. Chủ trọ sẽ liên hệ xác nhận sớm!";
        return RedirectToAction(nameof(Index));
    }

    // ── Tenant huỷ lịch hẹn ──────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var request = await db.ViewingRequests
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == CurrentUserId);

        if (request == null) return NotFound();

        if (request.Status != ViewingStatus.Pending && request.Status != ViewingStatus.Confirmed)
        {
            TempData["Error"] = "Không thể huỷ lịch hẹn ở trạng thái này.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = ViewingStatus.Cancelled;
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã huỷ lịch hẹn.";
        return RedirectToAction(nameof(Index));
    }

    // ── Tenant chấp nhận đổi giờ hẹn ──────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptReschedule(int id)
    {
        var request = await db.ViewingRequests
            .Include(v => v.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(v => v.Tenant)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == CurrentUserId);

        if (request == null) return NotFound();

        if (request.Status != ViewingStatus.Rescheduled)
        {
            TempData["Error"] = "Lịch hẹn không ở trạng thái yêu cầu đổi giờ.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = ViewingStatus.Confirmed;
        await db.SaveChangesAsync();

        // Notify Landlord
        await notifier.SendAsync(request.Room.Building.LandlordId, NotificationType.NewViewingRequest,
            "Lịch hẹn xem phòng được xác nhận",
            $"Khách thuê {request.Tenant.FullName} đã đồng ý với đề xuất đổi giờ hẹn phòng {request.Room.RoomNumber} sang lúc {request.ConfirmedTime:dd/MM HH:mm}.",
            "ViewingRequest", request.Id);

        // Email Landlord
        await emailSender.SendEmailAsync(request.Room.Building.Landlord.Email!,
            "Khách thuê đồng ý đổi giờ xem phòng — StuRoom",
            $"Xin chào <strong>{request.Room.Building.Landlord.FullName}</strong>,<br><br>" +
            $"Khách thuê <strong>{request.Tenant.FullName}</strong> đã <strong>chấp nhận</strong> đổi lịch xem phòng <strong>{request.Room.RoomNumber}</strong> " +
            $"tại <strong>{request.Room.Building.Name}</strong> sang " +
            $"lúc <strong>{request.ConfirmedTime:dd/MM/yyyy HH:mm}</strong>.<br><br>" +
            "Vui lòng đến đúng giờ. Cảm ơn bạn!");

        TempData["Success"] = "Đã chấp nhận thay đổi lịch hẹn.";
        return RedirectToAction(nameof(Index));
    }

    // ── Tenant từ chối đổi giờ hẹn ──────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineReschedule(int id)
    {
        var request = await db.ViewingRequests
            .Include(v => v.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(v => v.Tenant)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == CurrentUserId);

        if (request == null) return NotFound();

        if (request.Status != ViewingStatus.Rescheduled)
        {
            TempData["Error"] = "Lịch hẹn không ở trạng thái yêu cầu đổi giờ.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = ViewingStatus.Cancelled;
        await db.SaveChangesAsync();

        // Notify Landlord
        await notifier.SendAsync(request.Room.Building.LandlordId, NotificationType.NewViewingRequest,
            "Lịch hẹn xem phòng bị từ chối",
            $"Khách thuê {request.Tenant.FullName} không đồng ý với đề xuất đổi giờ hẹn phòng {request.Room.RoomNumber} sang lúc {request.ConfirmedTime:dd/MM HH:mm} và đã huỷ lịch.",
            "ViewingRequest", request.Id);

        // Email Landlord
        await emailSender.SendEmailAsync(request.Room.Building.Landlord.Email!,
            "Khách thuê từ chối đổi giờ xem phòng — StuRoom",
            $"Xin chào <strong>{request.Room.Building.Landlord.FullName}</strong>,<br><br>" +
            $"Khách thuê <strong>{request.Tenant.FullName}</strong> đã <strong>từ chối</strong> đổi lịch xem phòng <strong>{request.Room.RoomNumber}</strong> " +
            $"tại <strong>{request.Room.Building.Name}</strong> sang " +
            $"lúc <strong>{request.ConfirmedTime:dd/MM/yyyy HH:mm}</strong>. Lịch hẹn này đã bị huỷ.<br><br>" +
            "Cảm ơn bạn!");

        TempData["Success"] = "Đã từ chối đổi giờ hẹn và huỷ lịch.";
        return RedirectToAction(nameof(Index));
    }
}
