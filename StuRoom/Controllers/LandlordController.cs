using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
    ICloudinaryService cloudinary,
    IEmailSender emailSender) : Controller
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

    // ════════════════════════════════════════════════════════
    // FEE CONFIGS  (Task 11)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> FeeConfigs(int? buildingId)
    {
        ViewData["ActiveMenu"] = "FeeConfigs";

        var myBuildings = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var myBuildingIds = myBuildings.Select(b => b.Id).ToList();

        var query = db.FeeConfigs
            .Include(f => f.Building)
            .Include(f => f.Room)
            .Where(f =>
                (f.BuildingId != null && myBuildingIds.Contains(f.BuildingId.Value)) ||
                (f.RoomId != null && db.Rooms
                    .Where(r => myBuildingIds.Contains(r.BuildingId))
                    .Select(r => r.Id)
                    .Contains(f.RoomId.Value)));

        if (buildingId.HasValue)
            query = query.Where(f =>
                f.BuildingId == buildingId ||
                (f.Room != null && f.Room.BuildingId == buildingId));

        var configs = await query
            .OrderBy(f => f.BuildingId ?? f.Room!.BuildingId)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();

        // Rooms for building select in modal
        var myRooms = await db.Rooms
            .Where(r => myBuildingIds.Contains(r.BuildingId))
            .OrderBy(r => r.Building.Name).ThenBy(r => r.RoomNumber)
            .Select(r => new { r.Id, r.RoomNumber, r.BuildingId, BuildingName = r.Building.Name })
            .ToListAsync();

        ViewBag.Buildings  = myBuildings;
        ViewBag.BuildingId = buildingId;
        ViewBag.Rooms      = myRooms;

        return View(configs);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFeeConfig(
        string scope, int? buildingId, int? roomId,
        string name, FeeCategory feeCategory, CalcType calcType,
        decimal unitPrice, string unit, bool isActive, int sortOrder,
        int? returnBuildingId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên phí không được để trống.";
            return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
        }

        // Verify ownership
        if (scope == "building")
        {
            var ok = await db.Buildings.AnyAsync(b => b.Id == buildingId && b.LandlordId == CurrentUserId);
            if (!ok) return NotFound();
            roomId = null;
        }
        else
        {
            var ok = await db.Rooms.Include(r => r.Building)
                .AnyAsync(r => r.Id == roomId && r.Building.LandlordId == CurrentUserId);
            if (!ok) return NotFound();
            buildingId = null;
        }

        db.FeeConfigs.Add(new FeeConfig
        {
            BuildingId  = scope == "building" ? buildingId : null,
            RoomId      = scope == "room" ? roomId : null,
            Name        = name.Trim(),
            FeeCategory = feeCategory,
            CalcType    = calcType,
            UnitPrice   = unitPrice,
            Unit        = unit.Trim(),
            IsActive    = isActive,
            SortOrder   = sortOrder
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm cấu hình phí <strong>{name.Trim()}</strong>.";
        return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFeeConfig(
        int id, string name, FeeCategory feeCategory, CalcType calcType,
        decimal unitPrice, string unit, bool isActive, int sortOrder,
        int? returnBuildingId)
    {
        var config = await GetOwnedFeeConfig(id);
        if (config == null) return NotFound();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Tên phí không được để trống.";
            return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
        }

        config.Name        = name.Trim();
        config.FeeCategory = feeCategory;
        config.CalcType    = calcType;
        config.UnitPrice   = unitPrice;
        config.Unit        = unit.Trim();
        config.IsActive    = isActive;
        config.SortOrder   = sortOrder;
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật <strong>{config.Name}</strong>.";
        return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFeeConfig(int id, int? returnBuildingId)
    {
        var config = await GetOwnedFeeConfig(id);
        if (config == null) return NotFound();

        var inUse = await db.InvoiceItems.AnyAsync(i => i.FeeConfigId == id);
        if (inUse)
        {
            TempData["Error"] = $"Không thể xoá <strong>{config.Name}</strong> vì đã được dùng trong hoá đơn.";
            return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
        }

        db.FeeConfigs.Remove(config);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã xoá cấu hình phí <strong>{config.Name}</strong>.";
        return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeeConfig(int id, int? returnBuildingId)
    {
        var config = await GetOwnedFeeConfig(id);
        if (config == null) return NotFound();

        config.IsActive = !config.IsActive;
        await db.SaveChangesAsync();

        TempData["Success"] = config.IsActive
            ? $"Đã kích hoạt <strong>{config.Name}</strong>."
            : $"Đã tắt <strong>{config.Name}</strong>.";
        return RedirectToAction(nameof(FeeConfigs), new { buildingId = returnBuildingId });
    }

    // ════════════════════════════════════════════════════════
    // VIEWING REQUESTS — Landlord side  (Task 16 + 17)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> ViewingRequests(string? filter)
    {
        ViewData["ActiveMenu"] = "ViewingRequests";
        ViewData["Filter"]     = filter ?? "pending";

        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        var query = db.ViewingRequests
            .Include(v => v.Room).ThenInclude(r => r.Building)
            .Include(v => v.Tenant)
            .Where(v => myBuildingIds.Contains(v.Room.BuildingId));

        query = filter switch
        {
            "confirmed"  => query.Where(v => v.Status == ViewingStatus.Confirmed || v.Status == ViewingStatus.Rescheduled),
            "completed"  => query.Where(v => v.Status == ViewingStatus.Completed || v.Status == ViewingStatus.Cancelled),
            _            => query.Where(v => v.Status == ViewingStatus.Pending)
        };

        var list = await query.OrderBy(v => v.ProposedTime).ToListAsync();
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmViewing(int id, DateTime? confirmedTime)
    {
        var v = await GetOwnedViewing(id);
        if (v == null) return NotFound();

        v.Status        = ViewingStatus.Confirmed;
        v.ConfirmedTime = confirmedTime ?? v.ProposedTime;
        await db.SaveChangesAsync();

        // Task 17 — email Tenant
        await emailSender.SendEmailAsync(v.Tenant.Email!,
            "Lịch xem phòng đã được xác nhận — StuRoom",
            $"Xin chào <strong>{v.Tenant.FullName}</strong>,<br><br>" +
            $"Lịch xem phòng <strong>{v.Room.RoomNumber}</strong> tại <strong>{v.Room.Building.Name}</strong> " +
            $"đã được xác nhận vào lúc <strong>{v.ConfirmedTime:dd/MM/yyyy HH:mm}</strong>.<br><br>" +
            "Vui lòng đến đúng giờ. Cảm ơn bạn!");

        TempData["Success"] = "Đã xác nhận lịch hẹn và gửi email cho Tenant.";
        return RedirectToAction(nameof(ViewingRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleViewing(int id, DateTime newTime, string? landlordNote)
    {
        var v = await GetOwnedViewing(id);
        if (v == null) return NotFound();

        v.Status        = ViewingStatus.Rescheduled;
        v.ConfirmedTime = newTime;
        v.LandlordNote  = string.IsNullOrWhiteSpace(landlordNote) ? null : landlordNote.Trim();
        await db.SaveChangesAsync();

        // Task 17 — email Tenant
        await emailSender.SendEmailAsync(v.Tenant.Email!,
            "Chủ trọ đề xuất đổi giờ xem phòng — StuRoom",
            $"Xin chào <strong>{v.Tenant.FullName}</strong>,<br><br>" +
            $"Chủ trọ đề xuất đổi lịch xem phòng <strong>{v.Room.RoomNumber}</strong> " +
            $"tại <strong>{v.Room.Building.Name}</strong> sang " +
            $"<strong>{newTime:dd/MM/yyyy HH:mm}</strong>." +
            (string.IsNullOrWhiteSpace(landlordNote) ? "" : $"<br>Ghi chú: {landlordNote}") +
            "<br><br>Vui lòng vào StuRoom để xác nhận hoặc huỷ lịch.");

        TempData["Success"] = "Đã đề xuất đổi giờ và gửi email cho Tenant.";
        return RedirectToAction(nameof(ViewingRequests), new { filter = "confirmed" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelViewing(int id, string? landlordNote)
    {
        var v = await GetOwnedViewing(id);
        if (v == null) return NotFound();

        v.Status       = ViewingStatus.Cancelled;
        v.LandlordNote = string.IsNullOrWhiteSpace(landlordNote) ? null : landlordNote.Trim();
        await db.SaveChangesAsync();

        TempData["Warning"] = "Đã huỷ lịch hẹn.";
        return RedirectToAction(nameof(ViewingRequests));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteViewing(int id)
    {
        var v = await GetOwnedViewing(id);
        if (v == null) return NotFound();

        v.Status = ViewingStatus.Completed;
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã đánh dấu lịch hẹn hoàn thành.";
        return RedirectToAction(nameof(ViewingRequests), new { filter = "confirmed" });
    }

    private async Task<ViewingRequest?> GetOwnedViewing(int id)
    {
        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        return await db.ViewingRequests
            .Include(v => v.Room).ThenInclude(r => r.Building)
            .Include(v => v.Tenant)
            .FirstOrDefaultAsync(v => v.Id == id && myBuildingIds.Contains(v.Room.BuildingId));
    }

    // Helper: load FeeConfig only if it belongs to current landlord
    private async Task<FeeConfig?> GetOwnedFeeConfig(int id)
    {
        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id)
            .ToListAsync();

        return await db.FeeConfigs
            .Include(f => f.Building)
            .Include(f => f.Room)
            .FirstOrDefaultAsync(f => f.Id == id && (
                (f.BuildingId != null && myBuildingIds.Contains(f.BuildingId.Value)) ||
                (f.RoomId != null && db.Rooms
                    .Where(r => myBuildingIds.Contains(r.BuildingId))
                    .Select(r => r.Id)
                    .Contains(f.RoomId.Value))
            ));
    }
}
