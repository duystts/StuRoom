namespace StuRoom.Models;

public enum BookingStatus { Pending, Approved, Rejected }

public class BookingRequest
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;
    public ApplicationUser Tenant { get; set; } = null!;

    /// <summary>Lịch xem phòng liên quan (nullable — có thể đặt không qua lịch xem)</summary>
    public int? ViewingRequestId { get; set; }
    public ViewingRequest? ViewingRequest { get; set; }

    public DateTime DesiredMoveIn { get; set; }
    public string? Message { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? RejectionReason { get; set; }

    /// <summary>Được gán khi Landlord Approve — Contract.Status = Pending lúc mới tạo</summary>
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
