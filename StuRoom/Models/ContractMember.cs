namespace StuRoom.Models;

public class ContractMember
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;
    public ApplicationUser Tenant { get; set; } = null!;

    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    public DateTime? LeaveDate { get; set; }
    public string? Note { get; set; }
}
