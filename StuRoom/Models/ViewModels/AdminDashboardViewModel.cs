namespace StuRoom.Models.ViewModels;

public class AdminDashboardViewModel
{
    // Rooms
    public int TotalRooms     { get; set; }
    public int AvailableRooms { get; set; }
    public int OccupiedRooms  { get; set; }

    // Users
    public int TotalUsers       { get; set; }
    public int TotalTenants     { get; set; }
    public int TotalLandlords   { get; set; }
    public int ActiveLandlords  { get; set; }
    public int PendingLandlords { get; set; }

    // Contracts & Finance
    public int     ActiveContracts { get; set; }
    public int     TotalContracts  { get; set; }
    public decimal TotalRevenue    { get; set; }

    // Reviews
    public int PendingReviews { get; set; }
}
