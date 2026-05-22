namespace StuRoom.Models;

public class Amenity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;

    public ICollection<RoomAmenity> RoomAmenities { get; set; } = [];
}

public class RoomAmenity
{
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public int AmenityId { get; set; }
    public Amenity Amenity { get; set; } = null!;
}
