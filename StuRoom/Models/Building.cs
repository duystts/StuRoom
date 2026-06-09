namespace StuRoom.Models;

public class Building
{
    public int Id { get; set; }
    public string LandlordId { get; set; } = string.Empty;
    public ApplicationUser Landlord { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Room> Rooms { get; set; } = [];
    public ICollection<FeeConfig> FeeConfigs { get; set; } = [];
}
