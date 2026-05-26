using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;

namespace StuRoom.Services;

public interface INotificationService
{
    Task SendAsync(string userId, NotificationType type, string title, string content,
        string? entityType = null, int? entityId = null);
}

public class NotificationService(ApplicationDbContext db) : INotificationService
{
    public async Task SendAsync(string userId, NotificationType type, string title,
        string content, string? entityType = null, int? entityId = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId            = userId,
            Type              = type,
            Title             = title,
            Content           = content,
            RelatedEntityType = entityType,
            RelatedEntityId   = entityId,
            IsRead            = false
        });
        await db.SaveChangesAsync();
    }
}
