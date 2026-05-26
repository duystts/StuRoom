using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Services;

namespace StuRoom.Controllers;

[Authorize(Policy = "LandlordOnly")]
public class LandlordController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    ICloudinaryService cloudinary) : Controller
{
    private string CurrentUserId =>
        userManager.GetUserId(User)!;

    // ════════════════════════════════════════════════════════
    // BUILDINGS  (Task 7)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Buildings()
    {
        ViewData["ActiveMenu"] = "Buildings";

        var buildings = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Include(b => b.Rooms)
            .OrderBy(b => b.Name)
            .ToListAsync();

        return View(buildings);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBuilding(
        string name, string address,
        string province, string district, string ward,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            TempData["Error"] = "Tên và địa chỉ tòa nhà không được để trống.";
            return RedirectToAction(nameof(Buildings));
        }

        db.Buildings.Add(new Building
        {
            LandlordId  = CurrentUserId,
            Name        = name.Trim(),
            Address     = address.Trim(),
            Province    = province.Trim(),
            District    = district.Trim(),
            Ward        = ward.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm tòa nhà <strong>{name.Trim()}</strong>.";
        return RedirectToAction(nameof(Buildings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBuilding(
        int id, string name, string address,
        string province, string district, string ward,
        string? description)
    {
        var building = await db.Buildings
            .FirstOrDefaultAsync(b => b.Id == id && b.LandlordId == CurrentUserId);
        if (building == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            TempData["Error"] = "Tên và địa chỉ tòa nhà không được để trống.";
            return RedirectToAction(nameof(Buildings));
        }

        building.Name        = name.Trim();
        building.Address     = address.Trim();
        building.Province    = province.Trim();
        building.District    = district.Trim();
        building.Ward        = ward.Trim();
        building.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật tòa nhà <strong>{building.Name}</strong>.";
        return RedirectToAction(nameof(Buildings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBuilding(int id)
    {
        var building = await db.Buildings
            .Include(b => b.Rooms)
            .FirstOrDefaultAsync(b => b.Id == id && b.LandlordId == CurrentUserId);
        if (building == null) return NotFound();

        if (building.Rooms.Any())
        {
            TempData["Error"] = $"Không thể xoá <strong>{building.Name}</strong> vì còn phòng bên trong.";
            return RedirectToAction(nameof(Buildings));
        }

        db.Buildings.Remove(building);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã xoá tòa nhà <strong>{building.Name}</strong>.";
        return RedirectToAction(nameof(Buildings));
    }

    // ════════════════════════════════════════════════════════
    // ROOMS  (Task 8)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Rooms(int? buildingId)
    {
        ViewData["ActiveMenu"] = "Rooms";

        var myBuildings = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var query = db.Rooms
            .Include(r => r.Building)
            .Where(r => r.Building.LandlordId == CurrentUserId);

        if (buildingId.HasValue)
            query = query.Where(r => r.BuildingId == buildingId.Value);

        var rooms = await query
            .OrderBy(r => r.Building.Name)
            .ThenBy(r => r.RoomNumber)
            .ToListAsync();

        ViewBag.Buildings    = myBuildings;
        ViewBag.BuildingId   = buildingId;
        ViewBag.BuildingList = new SelectList(myBuildings, "Id", "Name", buildingId);

        return View(rooms);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(
        int buildingId, string roomNumber, int? floorNumber,
        decimal area, int? capacity, string? description)
    {
        // Ensure building belongs to this landlord
        var building = await db.Buildings
            .FirstOrDefaultAsync(b => b.Id == buildingId && b.LandlordId == CurrentUserId);
        if (building == null) return NotFound();

        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            TempData["Error"] = "Số phòng không được để trống.";
            return RedirectToAction(nameof(Rooms), new { buildingId });
        }

        db.Rooms.Add(new Room
        {
            BuildingId   = buildingId,
            RoomNumber   = roomNumber.Trim(),
            FloorNumber  = floorNumber,
            Area         = area,
            Capacity     = capacity,
            Description  = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status       = RoomStatus.Available
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm phòng <strong>{roomNumber.Trim()}</strong>.";
        return RedirectToAction(nameof(Rooms), new { buildingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(
        int id, string roomNumber, int? floorNumber,
        decimal area, int? capacity, string? description,
        int? returnBuildingId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == id && r.Building.LandlordId == CurrentUserId);
        if (room == null) return NotFound();

        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            TempData["Error"] = "Số phòng không được để trống.";
            return RedirectToAction(nameof(Rooms), new { buildingId = returnBuildingId });
        }

        room.RoomNumber  = roomNumber.Trim();
        room.FloorNumber = floorNumber;
        room.Area        = area;
        room.Capacity    = capacity;
        room.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật phòng <strong>{room.RoomNumber}</strong>.";
        return RedirectToAction(nameof(Rooms), new { buildingId = returnBuildingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int id, int? returnBuildingId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Contracts)
            .FirstOrDefaultAsync(r => r.Id == id && r.Building.LandlordId == CurrentUserId);
        if (room == null) return NotFound();

        var hasActive = room.Contracts.Any(c => c.Status == ContractStatus.Active);
        if (hasActive)
        {
            TempData["Error"] = $"Không thể xoá phòng <strong>{room.RoomNumber}</strong> vì đang có hợp đồng active.";
            return RedirectToAction(nameof(Rooms), new { buildingId = returnBuildingId });
        }

        db.Rooms.Remove(room);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã xoá phòng <strong>{room.RoomNumber}</strong>.";
        return RedirectToAction(nameof(Rooms), new { buildingId = returnBuildingId });
    }

    // ════════════════════════════════════════════════════════
    // ROOM DETAIL — images + amenities  (Task 9 + 10)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> RoomDetail(int id)
    {
        ViewData["ActiveMenu"] = "Rooms";

        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images.OrderBy(i => i.SortOrder))
            .Include(r => r.RoomAmenities)
                .ThenInclude(ra => ra.Amenity)
            .FirstOrDefaultAsync(r => r.Id == id && r.Building.LandlordId == CurrentUserId);

        if (room == null) return NotFound();

        ViewBag.AllAmenities = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(room);
    }

    // ── Images (Task 9) ──────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImages(int id, List<IFormFile> files)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id && r.Building.LandlordId == CurrentUserId);
        if (room == null) return NotFound();

        const int maxImages = 10;
        var remaining = maxImages - room.Images.Count;
        if (remaining <= 0)
        {
            TempData["Error"] = $"Phòng đã đạt giới hạn {maxImages} ảnh.";
            return RedirectToAction(nameof(RoomDetail), new { id });
        }

        var toUpload = files.Where(f => f.Length > 0).Take(remaining).ToList();
        if (!toUpload.Any())
        {
            TempData["Error"] = "Vui lòng chọn ít nhất một ảnh.";
            return RedirectToAction(nameof(RoomDetail), new { id });
        }

        var nextOrder = room.Images.Any() ? room.Images.Max(i => i.SortOrder) + 1 : 0;
        var isFirst   = !room.Images.Any();

        foreach (var file in toUpload)
        {
            try
            {
                var (url, publicId) = await cloudinary.UploadAsync(file);
                db.RoomImages.Add(new RoomImage
                {
                    RoomId            = id,
                    ImageUrl          = url,
                    CloudinaryPublicId = publicId,
                    IsPrimary         = isFirst,
                    SortOrder         = nextOrder++
                });
                isFirst = false;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi upload ảnh: {ex.Message}";
                return RedirectToAction(nameof(RoomDetail), new { id });
            }
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã tải lên {toUpload.Count} ảnh.";
        return RedirectToAction(nameof(RoomDetail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int imageId, int roomId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Building.LandlordId == CurrentUserId);
        if (room == null) return NotFound();

        foreach (var img in room.Images)
            img.IsPrimary = img.Id == imageId;

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã đặt ảnh đại diện.";
        return RedirectToAction(nameof(RoomDetail), new { id = roomId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int imageId, int roomId)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.Building.LandlordId == CurrentUserId);
        if (room == null) return NotFound();

        var image = await db.RoomImages.FindAsync(imageId);
        if (image == null || image.RoomId != roomId) return NotFound();

        // Delete from Cloudinary
        if (!string.IsNullOrWhiteSpace(image.CloudinaryPublicId))
            await cloudinary.DeleteAsync(image.CloudinaryPublicId);

        var wasPrimary = image.IsPrimary;
        db.RoomImages.Remove(image);
        await db.SaveChangesAsync();

        // If deleted image was primary, promote the first remaining image
        if (wasPrimary)
        {
            var next = await db.RoomImages
                .Where(i => i.RoomId == roomId)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync();
            if (next != null)
            {
                next.IsPrimary = true;
                await db.SaveChangesAsync();
            }
        }

        TempData["Success"] = "Đã xoá ảnh.";
        return RedirectToAction(nameof(RoomDetail), new { id = roomId });
    }

    // ── Amenities (Task 10) ──────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAmenities(int id, List<int> amenityIds)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.RoomAmenities)
            .FirstOrDefaultAsync(r => r.Id == id && r.Building.LandlordId == CurrentUserId);
        if (room == null) return NotFound();

        // Remove all current, then re-add selected
        db.RoomAmenities.RemoveRange(room.RoomAmenities);

        foreach (var amenityId in amenityIds.Distinct())
        {
            db.RoomAmenities.Add(new RoomAmenity { RoomId = id, AmenityId = amenityId });
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã lưu tiện ích phòng.";
        return RedirectToAction(nameof(RoomDetail), new { id });
    }
}
