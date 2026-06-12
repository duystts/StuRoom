namespace StuRoom.Models.ViewModels;

public class RoomSearchViewModel
{
    // ── Filter inputs ──────────────────────────────
    public string?      Q          { get; set; }
    public string?      Province   { get; set; }
    public string?      District   { get; set; }
    public decimal?     MinPrice   { get; set; }
    public decimal?     MaxPrice   { get; set; }
    public decimal?     MinArea    { get; set; }
    public decimal?     MaxElectricityPrice { get; set; }
    public decimal?     MaxWaterPrice       { get; set; }
    public decimal?     MaxInternetPrice    { get; set; }
    public List<int>    AmenityIds { get; set; } = [];

    // ── Results ────────────────────────────────────
    public List<RoomCardViewModel> Rooms      { get; set; } = [];
    public int                     TotalCount { get; set; }

    // ── Filter option data ─────────────────────────
    public List<Amenity>  AllAmenities { get; set; } = [];
    public List<string>   Provinces    { get; set; } = [];
}

public class RoomCardViewModel
{
    public int     Id             { get; set; }
    public string  RoomNumber     { get; set; } = string.Empty;
    public string  BuildingName   { get; set; } = string.Empty;
    public string  Address        { get; set; } = string.Empty;
    public string  Province       { get; set; } = string.Empty;
    public string  District       { get; set; } = string.Empty;
    public string  Ward           { get; set; } = string.Empty;
    public decimal Area           { get; set; }
    public int?    Capacity       { get; set; }

    public string?  PrimaryImageUrl { get; set; }
    public decimal? RentPrice       { get; set; }   // null = chưa cấu hình giá
    public string   RentUnit        { get; set; } = "tháng";

    public List<(string Icon, string Name)> Amenities   { get; set; } = [];
    public double?                          AvgRating    { get; set; }
    public int                              ReviewCount  { get; set; }
}
