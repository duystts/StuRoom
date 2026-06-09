using System;
using System.ComponentModel.DataAnnotations;

namespace StuRoom.Models;

public enum ReportStatus { Pending, Resolved, Dismissed }

public class RoomReport
{
    public int Id { get; set; }
    
    [Required]
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    
    [Required]
    public string ReporterId { get; set; } = string.Empty;
    public ApplicationUser Reporter { get; set; } = null!;
    
    [Required]
    [MaxLength(100)]
    public string Reason { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    
    [MaxLength(1000)]
    public string? AdminFeedback { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
}
