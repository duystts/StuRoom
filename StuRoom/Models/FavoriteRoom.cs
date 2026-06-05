using System;
using System.ComponentModel.DataAnnotations;

namespace StuRoom.Models;

public class FavoriteRoom
{
    [Required]
    public string TenantId { get; set; } = string.Empty;
    public ApplicationUser Tenant { get; set; } = null!;
    
    [Required]
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
