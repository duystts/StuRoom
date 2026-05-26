using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Services;

namespace StuRoom.Controllers;

[Authorize(Policy = "TenantOnly")]
public class ReviewsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    INotificationService notifier) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int roomId, int contractId,
        int rating, string? content)
    {
        var userId = userManager.GetUserId(User)!;

        // Verify contract belongs to this tenant and is completed
        var contract = await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building)
            .FirstOrDefaultAsync(c => c.Id == contractId
                && c.TenantId == userId
                && c.RoomId   == roomId
                && (c.Status == ContractStatus.Expired
                 || c.Status == ContractStatus.Terminated));

        if (contract == null)
        {
            TempData["ReviewError"] = "Không thể gửi đánh giá.";
            return RedirectToAction("Detail", "Rooms", new { id = roomId });
        }

        // Check duplicate
        var already = await db.RoomReviews
            .AnyAsync(rv => rv.ContractId == contractId && rv.ReviewerId == userId);
        if (already)
        {
            TempData["ReviewError"] = "Bạn đã đánh giá hợp đồng này rồi.";
            return RedirectToAction("Detail", "Rooms", new { id = roomId });
        }

        if (rating < 1 || rating > 5)
        {
            TempData["ReviewError"] = "Số sao không hợp lệ (1–5).";
            return RedirectToAction("Detail", "Rooms", new { id = roomId });
        }

        db.RoomReviews.Add(new RoomReview
        {
            RoomId     = roomId,
            ReviewerId = userId,
            ContractId = contractId,
            Rating     = rating,
            Content    = content,
            IsApproved = true
        });
        await db.SaveChangesAsync();

        // Notify landlord
        var landlordId = contract.Room.Building.LandlordId;
        var reviewer   = await userManager.GetUserAsync(User);
        await notifier.SendAsync(landlordId,
            NotificationType.NewReview,
            "Đánh giá mới",
            $"{reviewer?.FullName ?? "Tenant"} vừa đánh giá {rating}⭐ cho phòng {contract.Room.RoomNumber}.",
            "Room", roomId);

        TempData["ReviewSuccess"] = "Cảm ơn bạn đã đánh giá!";
        return RedirectToAction("Detail", "Rooms", new { id = roomId });
    }
}
