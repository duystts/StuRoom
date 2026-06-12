using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using System.Security.Claims;

namespace StuRoom.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("history/{receiverId}")]
    public async Task<IActionResult> GetChatHistory(string receiverId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Unauthorized();

        var messages = await db.ChatMessages
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == receiverId) ||
                        (m.SenderId == receiverId && m.ReceiverId == currentUserId))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new {
                m.Id,
                m.Message,
                m.CreatedAt,
                IsMine = m.SenderId == currentUserId
            })
            .ToListAsync();

        // Mark as read
        var unread = await db.ChatMessages
            .Where(m => m.SenderId == receiverId && m.ReceiverId == currentUserId && !m.IsRead)
            .ToListAsync();
        
        if (unread.Any())
        {
            foreach(var m in unread) m.IsRead = true;
            await db.SaveChangesAsync();
        }

        return Ok(messages);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Unauthorized();

        // Find all users the current user has chatted with
        var chatPartnersIds = await db.ChatMessages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToListAsync();

        var conversations = new List<object>();

        foreach(var partnerId in chatPartnersIds)
        {
            var partner = await userManager.FindByIdAsync(partnerId);
            if (partner == null) continue;

            var lastMsg = await db.ChatMessages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId) ||
                            (m.SenderId == partnerId && m.ReceiverId == currentUserId))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            var unreadCount = await db.ChatMessages
                .CountAsync(m => m.SenderId == partnerId && m.ReceiverId == currentUserId && !m.IsRead);

            conversations.Add(new {
                PartnerId = partner.Id,
                PartnerName = partner.FullName ?? partner.UserName,
                PartnerAvatar = string.IsNullOrEmpty(partner.AvatarUrl) ? "/img/avatar-default.svg" : partner.AvatarUrl,
                LastMessage = lastMsg?.Message,
                LastMessageTime = lastMsg?.CreatedAt,
                UnreadCount = unreadCount
            });
        }

        return Ok(conversations.OrderByDescending(c => ((dynamic)c).LastMessageTime));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return Unauthorized();

        var count = await db.ChatMessages
            .CountAsync(m => m.ReceiverId == currentUserId && !m.IsRead);

        return Ok(new { count });
    }
}
