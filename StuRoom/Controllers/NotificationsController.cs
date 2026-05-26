using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;

namespace StuRoom.Controllers;

[Authorize]
public class NotificationsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    // GET — danh sách thông báo (trang riêng)
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User)!;
        var list   = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
        return View(list);
    }

    // POST — đánh dấu 1 thông báo đã đọc
    [HttpPost]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl)
    {
        var userId = userManager.GetUserId(User)!;
        var notif  = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notif != null)
        {
            notif.IsRead = true;
            await db.SaveChangesAsync();
        }
        return Redirect(returnUrl ?? Url.Action("Index")!);
    }

    // POST — đánh dấu tất cả đã đọc
    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = userManager.GetUserId(User)!;
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return RedirectToAction(nameof(Index));
    }

    // GET (AJAX) — dropdown payload (last 10 unread)
    public async Task<IActionResult> Dropdown()
    {
        var userId = userManager.GetUserId(User)!;
        var items  = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync();
        return PartialView("_NotificationDropdown", items);
    }
}
