using System;

namespace StuRoom.Models;

public class AuditLog
{
    public int Id { get; set; }
    
    // The user who performed the action (nullable in case of system action)
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Redundant fields to preserve history if user is deleted
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
