using Microsoft.AspNetCore.Identity;

namespace StuRoom.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public string? StudentId { get; set; }

    /// <summary>Chỉ dùng cho Landlord — Admin duyệt trước khi được tạo Building/Room</summary>
    public bool IsApproved { get; set; }

    public string? RejectionReason { get; set; }
}

