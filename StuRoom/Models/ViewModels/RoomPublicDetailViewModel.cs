namespace StuRoom.Models.ViewModels;

public class RoomPublicDetailViewModel
{
    public Room              Room        { get; set; } = null!;
    public List<RoomImage>   Images      { get; set; } = [];
    public List<Amenity>     Amenities   { get; set; } = [];

    /// <summary>Active fee configs — room-level first, then building-level for same category</summary>
    public List<FeeConfig>   FeeConfigs  { get; set; } = [];

    public decimal?          RentPrice   { get; set; }
    public string            RentUnit    { get; set; } = "tháng";

    public List<RoomReview>  Reviews     { get; set; } = [];
    public double?           AvgRating   { get; set; }

    /// <summary>True nếu user đang đăng nhập với role Tenant</summary>
    public bool CanBook { get; set; }
}
