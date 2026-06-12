namespace StuRoom.Models;

public enum ViewingStatus { Pending, Confirmed, Rescheduled, Cancelled, Completed }

public class ViewingRequest
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;
    public ApplicationUser Tenant { get; set; } = null!;

    public DateTime ProposedTime { get; set; }

    /// <summary>Chủ trọ đặt lại giờ khi không hợp (Status = Rescheduled)</summary>
    public DateTime? ConfirmedTime { get; set; }

    public ViewingStatus Status { get; set; } = ViewingStatus.Pending;
    public string? TenantNote { get; set; }
    public string? LandlordNote { get; set; }

    /// <summary>Đã gửi email nhắc hẹn trước 1 ngày chưa</summary>
    public bool ReminderSent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
