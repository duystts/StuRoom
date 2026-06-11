using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using System;
using System.Threading.Tasks;

namespace StuRoom.Services;

public interface IAuditLogService
{
    Task LogAsync(string? userId, string action, string? entityName, string? entityId, string description, string? ipAddress = null);
}

public class AuditLogService(ApplicationDbContext db) : IAuditLogService
{
    public async Task LogAsync(string? userId, string action, string? entityName, string? entityId, string description, string? ipAddress = null)
    {
        string? email = null;
        string? fullName = null;

        if (!string.IsNullOrEmpty(userId))
        {
            var user = await db.Users.FindAsync(userId);
            if (user != null)
            {
                email = user.Email;
                fullName = user.FullName;
            }
        }

        var log = new AuditLog
        {
            UserId = userId,
            UserEmail = email,
            UserFullName = fullName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
