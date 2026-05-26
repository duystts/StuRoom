using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    INotificationService notifier) : Controller
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
}
