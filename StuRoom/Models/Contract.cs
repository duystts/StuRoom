namespace StuRoom.Models;

public enum ContractStatus { Pending, Active, Expired, Terminated }

public class Contract
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;
    public ApplicationUser Tenant { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Pending;
    public decimal DepositAmount { get; set; }
    public decimal MonthlyRent { get; set; }

    /// <summary>Đang tìm người ghép — hiển thị trên listing</summary>
    public bool SeekingRoommates { get; set; }

    /// <summary>Tổng số người muốn ở (primary + members). null = không giới hạn</summary>
    public int? DesiredOccupancy { get; set; }

    public string? GeneratedDocUrl { get; set; }
    public string? TerminationReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<RoomReview> Reviews { get; set; } = [];
    public ICollection<ContractMember> Members { get; set; } = [];
}
