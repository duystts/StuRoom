namespace StuRoom.Models;

public enum RoomStatus { Available, Occupied, Maintenance, Inactive }

public class Room
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public string RoomNumber { get; set; } = string.Empty;
    public int? FloorNumber { get; set; }
    public decimal Area { get; set; }

    /// <summary>null = không giới hạn số người thuê</summary>
    public int? Capacity { get; set; }

    public string? Description { get; set; }

    /// <summary>Tự động cập nhật dựa theo Contract active — không chỉnh thủ công</summary>
    public RoomStatus Status { get; set; } = RoomStatus.Available;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoomImage> Images { get; set; } = [];
    public ICollection<RoomAmenity> RoomAmenities { get; set; } = [];
    public ICollection<FeeConfig> FeeConfigs { get; set; } = [];
    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<ViewingRequest> ViewingRequests { get; set; } = [];
    public ICollection<RoomReview> Reviews { get; set; } = [];
}
