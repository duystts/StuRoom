using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Services;

namespace StuRoom.Controllers;

[Authorize(Policy = "TenantOnly")]
public class BookingRequestsController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    INotificationService notifier) : Controller
{
    private string CurrentUserId => userManager.GetUserId(User)!;

    // ════════════════════════════════════════════════════════
    // TENANT — danh sách đặt phòng của mình  (Task 18)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Index()
    {
        var requests = await db.BookingRequests
            .Include(b => b.Room).ThenInclude(r => r.Building)
            .Include(b => b.Room.Images)
            .Include(b => b.Contract)
            .Where(b => b.TenantId == CurrentUserId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    // ── Tạo đặt phòng ────────────────────────────────────────

    public async Task<IActionResult> Create(int roomId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Status == RoomStatus.Available);

        if (room == null) return NotFound();

        // Block duplicate pending booking
        var existing = await db.BookingRequests.AnyAsync(b =>
            b.RoomId == roomId &&
            b.TenantId == CurrentUserId &&
            b.Status == BookingStatus.Pending);

        if (existing)
        {
            TempData["Error"] = "Bạn đã có yêu cầu đặt phòng này đang chờ duyệt.";
            return RedirectToAction("Detail", "Rooms", new { id = roomId });
        }

        // Pre-link most recent completed viewing request
        var viewingId = await db.ViewingRequests
            .Where(v => v.RoomId == roomId && v.TenantId == CurrentUserId
                     && v.Status == ViewingStatus.Completed)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync();

        ViewBag.Room      = room;
        ViewBag.ViewingId = viewingId;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int roomId, DateTime desiredMoveIn,
        string? message, int? viewingRequestId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Status == RoomStatus.Available);

        if (room == null) return NotFound();

        if (desiredMoveIn.Date < DateTime.Today)
        {
            ViewBag.Room      = room;
            ViewBag.ViewingId = viewingRequestId;
            ModelState.AddModelError("", "Ngày dự kiến vào ở không được ở quá khứ.");
            return View();
        }

        db.BookingRequests.Add(new BookingRequest
        {
            RoomId           = roomId,
            TenantId         = CurrentUserId,
            DesiredMoveIn    = desiredMoveIn,
            Message          = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            ViewingRequestId = viewingRequestId,
            Status           = BookingStatus.Pending
        });
        await db.SaveChangesAsync();

        // Task 30 — notify Landlord
        await notifier.SendAsync(room.Building.LandlordId, NotificationType.NewBookingRequest,
            "Yêu cầu đặt phòng mới",
            $"Tenant muốn đặt phòng {room.RoomNumber} — dự kiến vào: {desiredMoveIn:dd/MM/yyyy}.",
            "BookingRequest", db.BookingRequests.Local.Last().Id);

        TempData["Success"] = "Đã gửi yêu cầu đặt phòng. Chủ trọ sẽ phản hồi sớm!";
        return RedirectToAction(nameof(Index));
    }

    // ── Tenant huỷ đặt phòng ─────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await db.BookingRequests
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == CurrentUserId);

        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Chỉ có thể huỷ yêu cầu đang Pending.";
            return RedirectToAction(nameof(Index));
        }

        booking.Status = BookingStatus.Rejected;
        booking.RejectionReason = "Tenant tự huỷ.";
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã huỷ yêu cầu đặt phòng.";
        return RedirectToAction(nameof(Index));
    }
}
