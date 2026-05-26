namespace StuRoom.Models.ViewModels;

public class LandlordApprovalViewModel
{
    public List<LandlordItem> Pending  { get; set; } = [];
    public List<LandlordItem> Approved { get; set; } = [];
    public List<LandlordItem> Rejected { get; set; } = [];
}

public class LandlordItem
{
    public string Id         { get; set; } = "";
    public string FullName   { get; set; } = "";
    public string Email      { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
