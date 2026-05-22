using Microsoft.AspNetCore.Identity;

namespace StuRoom.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public string? StudentId { get; set; }
}
