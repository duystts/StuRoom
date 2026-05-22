namespace StuRoom.Models;

public class RoomReview
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string ReviewerId { get; set; } = string.Empty;
    public ApplicationUser Reviewer { get; set; } = null!;

    /// <summary>Xác minh reviewer đã từng ở phòng này</summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public int Rating { get; set; }
    public string? Content { get; set; }
    public bool IsApproved { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
