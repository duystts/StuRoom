namespace StuRoom.Models;

public enum FeeCategory { Rent, Electricity, Water, Internet, Parking, Other }
public enum CalcType { Fixed, PerUnit }

public class FeeConfig
{
    public int Id { get; set; }

    /// <summary>null nếu là config cấp Room</summary>
    public int? BuildingId { get; set; }
    public Building? Building { get; set; }

    /// <summary>null nếu là config cấp Building. Room-level override Building-level cùng FeeCategory.</summary>
    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public string Name { get; set; } = string.Empty;
    public FeeCategory FeeCategory { get; set; }
    public CalcType CalcType { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
