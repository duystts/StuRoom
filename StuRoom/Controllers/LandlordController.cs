using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using WpDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Services;

namespace StuRoom.Controllers;

[Authorize(Policy = "LandlordOnly")]
public class LandlordController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    ICloudinaryService cloudinary,
    IEmailSender emailSender,
    INotificationService notifier) : Controller
{
    private string CurrentUserId =>
        userManager.GetUserId(User)!;

    // ════════════════════════════════════════════════════════
    // DASHBOARD  (Task 32)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Dashboard()
    {
        ViewData["ActiveMenu"] = "Dashboard";

        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        // ── Stats cards ──────────────────────────────────────
        int totalRooms     = await db.Rooms.CountAsync(r => myBuildingIds.Contains(r.BuildingId));
        int occupiedRooms  = await db.Rooms.CountAsync(r => myBuildingIds.Contains(r.BuildingId)
                                 && r.Status == RoomStatus.Occupied);
        int activeContracts = await db.Contracts.CountAsync(c =>
                                 myBuildingIds.Contains(c.Room.BuildingId)
                                 && c.Status == ContractStatus.Active);
        int unpaidInvoices = await db.Invoices.CountAsync(i =>
                                 myBuildingIds.Contains(i.Contract.Room.BuildingId)
                                 && (i.Status == InvoiceStatus.Sent
                                  || i.Status == InvoiceStatus.Overdue));

        // ── Revenue chart — last 12 months ───────────────────
        var now         = DateTime.Now;
        var since       = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        var paidPayments = await db.Payments
            .Include(p => p.Invoice).ThenInclude(i => i.Contract).ThenInclude(c => c.Room)
            .Where(p => myBuildingIds.Contains(p.Invoice.Contract.Room.BuildingId)
                     && p.PaymentDate >= since)
            .Select(p => new { p.PaymentDate.Year, p.PaymentDate.Month, p.Amount })
            .ToListAsync();

        var revenueLabels = new List<string>();
        var revenueData   = new List<decimal>();
        for (int i = 11; i >= 0; i--)
        {
            var m    = now.AddMonths(-i);
            revenueLabels.Add($"{m.Month:D2}/{m.Year}");
            revenueData.Add(paidPayments
                .Where(p => p.Year == m.Year && p.Month == m.Month)
                .Sum(p => p.Amount));
        }

        // ── Overdue invoices ──────────────────────────────────
        var overdueInvoices = await db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building)
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Where(i => myBuildingIds.Contains(i.Contract.Room.BuildingId)
                     && (i.Status == InvoiceStatus.Overdue
                      || (i.Status == InvoiceStatus.Sent && i.DueDate < DateTime.Now)))
            .OrderBy(i => i.DueDate)
            .Take(10)
            .ToListAsync();

        // Mark sent-past-due as Overdue
        foreach (var inv in overdueInvoices.Where(i => i.Status == InvoiceStatus.Sent))
            inv.Status = InvoiceStatus.Overdue;
        if (overdueInvoices.Any(i => i.Status == InvoiceStatus.Overdue))
            await db.SaveChangesAsync();

        // ── Recent contracts ──────────────────────────────────
        var recentContracts = await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building)
            .Include(c => c.Tenant)
            .Where(c => myBuildingIds.Contains(c.Room.BuildingId)
                     && c.Status == ContractStatus.Active)
            .OrderByDescending(c => c.StartDate)
            .Take(5)
            .ToListAsync();

        ViewBag.TotalRooms       = totalRooms;
        ViewBag.OccupiedRooms    = occupiedRooms;
        ViewBag.ActiveContracts  = activeContracts;
        ViewBag.UnpaidInvoices   = unpaidInvoices;
        ViewBag.RevenueLabels    = revenueLabels;
        ViewBag.RevenueData      = revenueData;
        ViewBag.OverdueInvoices  = overdueInvoices;
        ViewBag.RecentContracts  = recentContracts;
        ViewBag.OccupancyRate    = totalRooms > 0
            ? Math.Round(100.0 * occupiedRooms / totalRooms, 1) : 0.0;

        return View();
    }

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
        string? description, double? latitude, double? longitude)
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
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Latitude    = latitude,
            Longitude   = longitude
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm tòa nhà <strong>{name.Trim()}</strong>.";
        return RedirectToAction(nameof(Buildings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBuilding(
        int id, string name, string address,
        string province, string district, string ward,
        string? description, double? latitude, double? longitude)
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
        building.Latitude    = latitude;
        building.Longitude   = longitude;
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

        // Task 30 — notify Tenant
        await notifier.SendAsync(v.TenantId, NotificationType.ViewingConfirmed,
            "Lịch hẹn xem phòng đã xác nhận",
            $"Lịch xem phòng {v.Room.RoomNumber} — {v.ConfirmedTime:dd/MM HH:mm}.",
            "ViewingRequest", v.Id);

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

        // Task 30 — notify Tenant
        await notifier.SendAsync(v.TenantId, NotificationType.ViewingRescheduled,
            "Đề xuất đổi giờ xem phòng",
            $"Phòng {v.Room.RoomNumber} — giờ mới: {newTime:dd/MM HH:mm}.",
            "ViewingRequest", v.Id);

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

    // ════════════════════════════════════════════════════════
    // BOOKING REQUESTS — Landlord side  (Task 19 + 20)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> BookingRequests(string? filter)
    {
        ViewData["ActiveMenu"] = "BookingRequests";
        ViewData["Filter"]     = filter ?? "pending";

        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        var query = db.BookingRequests
            .Include(b => b.Room).ThenInclude(r => r.Building)
            .Include(b => b.Tenant)
            .Include(b => b.Contract)
            .Where(b => myBuildingIds.Contains(b.Room.BuildingId));

        query = filter switch
        {
            "approved" => query.Where(b => b.Status == BookingStatus.Approved),
            "rejected" => query.Where(b => b.Status == BookingStatus.Rejected),
            _          => query.Where(b => b.Status == BookingStatus.Pending)
        };

        var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        // Lấy tất cả Rent configs đang hoạt động dạng Fixed cho các tòa nhà của Landlord này
        var rentConfigs = await db.FeeConfigs
            .Where(f => f.FeeCategory == FeeCategory.Rent && f.IsActive && f.CalcType == CalcType.Fixed)
            .Where(f => (f.BuildingId != null && myBuildingIds.Contains(f.BuildingId.Value)) ||
                        (f.RoomId != null && db.Rooms.Where(r => myBuildingIds.Contains(r.BuildingId)).Select(r => r.Id).Contains(f.RoomId.Value)))
            .ToListAsync();

        var roomPrices = new Dictionary<int, decimal>();
        foreach (var b in list)
        {
            var roomId = b.RoomId;
            var buildingId = b.Room.BuildingId;
            
            // Ưu tiên config ở cấp phòng (RoomId) trước, sau đó mới đến cấp tòa nhà (BuildingId)
            var config = rentConfigs.FirstOrDefault(f => f.RoomId == roomId)
                         ?? rentConfigs.FirstOrDefault(f => f.BuildingId == buildingId && f.RoomId == null);
                         
            if (config != null)
            {
                roomPrices[roomId] = config.UnitPrice;
            }
        }
        ViewBag.RoomPrices = roomPrices;

        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(
        int id, decimal depositAmount, decimal monthlyRent,
        DateTime startDate, DateTime? endDate)
    {
        var booking = await db.BookingRequests
            .Include(b => b.Room).ThenInclude(r => r.Building)
            .Include(b => b.Tenant)
            .FirstOrDefaultAsync(b => b.Id == id &&
                db.Buildings.Any(bl => bl.LandlordId == CurrentUserId && bl.Id == b.Room.BuildingId));

        if (booking == null) return NotFound();

        // Kiểm tra xem phòng có cấu hình giá thuê cố định đang hoạt động không
        var rentConfig = await db.FeeConfigs
            .Where(f => f.FeeCategory == FeeCategory.Rent && f.IsActive && f.CalcType == CalcType.Fixed)
            .Where(f => f.RoomId == booking.RoomId || (f.BuildingId == booking.Room.BuildingId && f.RoomId == null))
            .OrderByDescending(f => f.RoomId)
            .FirstOrDefaultAsync();

        if (rentConfig != null)
        {
            // Nếu có giá cố định, ép buộc giá thuê và tiền cọc theo cấu hình này
            monthlyRent = rentConfig.UnitPrice;
            depositAmount = rentConfig.UnitPrice; // Tiền cọc bằng 1 tháng tiền thuê
        }

        // Create Contract (Status = Pending — Landlord finalises in Contract management)
        var contract = new Contract
        {
            RoomId        = booking.RoomId,
            TenantId      = booking.TenantId,
            StartDate     = startDate,
            EndDate       = endDate,
            DepositAmount = depositAmount,
            MonthlyRent   = monthlyRent,
            Status        = ContractStatus.Pending
        };
        db.Contracts.Add(contract);

        booking.Status = BookingStatus.Approved;
        await db.SaveChangesAsync();

        // Link contract back to booking
        booking.ContractId = contract.Id;
        await db.SaveChangesAsync();

        // Task 20 — email
        await emailSender.SendEmailAsync(booking.Tenant.Email!,
            "Yêu cầu đặt phòng được chấp nhận — StuRoom",
            $"Xin chào <strong>{booking.Tenant.FullName}</strong>,<br><br>" +
            $"Yêu cầu đặt phòng <strong>{booking.Room.RoomNumber}</strong> tại " +
            $"<strong>{booking.Room.Building.Name}</strong> đã được <strong>chấp nhận</strong>.<br>" +
            $"Ngày vào ở dự kiến: <strong>{startDate:dd/MM/yyyy}</strong>.<br>" +
            $"Tiền cọc: <strong>{depositAmount:N0} ₫</strong> — Giá thuê: <strong>{monthlyRent:N0} ₫/tháng</strong>.<br><br>" +
            "Chủ trọ sẽ liên hệ để hoàn tất hợp đồng. Chúc mừng bạn!");

        // Task 30 — notify Tenant
        await notifier.SendAsync(booking.TenantId, NotificationType.BookingApproved,
            "Đặt phòng được chấp nhận!",
            $"Phòng {booking.Room.RoomNumber} — {booking.Room.Building.Name}. Vào ở: {startDate:dd/MM/yyyy}.",
            "Contract", contract.Id);

        TempData["Success"] = $"Đã duyệt đặt phòng và tạo hợp đồng #{contract.Id}.";
        return RedirectToAction(nameof(BookingRequests), new { filter = "approved" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(int id, string? rejectionReason)
    {
        var booking = await db.BookingRequests
            .Include(b => b.Room).ThenInclude(r => r.Building)
            .Include(b => b.Tenant)
            .FirstOrDefaultAsync(b => b.Id == id &&
                db.Buildings.Any(bl => bl.LandlordId == CurrentUserId && bl.Id == b.Room.BuildingId));

        if (booking == null) return NotFound();

        booking.Status          = BookingStatus.Rejected;
        booking.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim();
        await db.SaveChangesAsync();

        // Task 20 — email
        var reason = string.IsNullOrWhiteSpace(rejectionReason) ? "không có lý do cụ thể" : rejectionReason;
        await emailSender.SendEmailAsync(booking.Tenant.Email!,
            "Yêu cầu đặt phòng không được chấp nhận — StuRoom",
            $"Xin chào <strong>{booking.Tenant.FullName}</strong>,<br><br>" +
            $"Rất tiếc, yêu cầu đặt phòng <strong>{booking.Room.RoomNumber}</strong> tại " +
            $"<strong>{booking.Room.Building.Name}</strong> <strong>không được chấp nhận</strong>.<br>" +
            $"Lý do: {reason}.<br><br>" +
            "Bạn có thể tìm phòng khác trên StuRoom. Xin lỗi vì bất tiện!");

        // Task 30 — notify Tenant
        await notifier.SendAsync(booking.TenantId, NotificationType.BookingRejected,
            "Đặt phòng không được chấp nhận",
            $"Phòng {booking.Room.RoomNumber}. Lý do: {reason}.",
            "BookingRequest", booking.Id);

        TempData["Warning"] = "Đã từ chối yêu cầu đặt phòng.";
        return RedirectToAction(nameof(BookingRequests));
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

    // ════════════════════════════════════════════════════════
    // CONTRACTS  (Task 21)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Contracts(string? filter, int? buildingId)
    {
        ViewData["ActiveMenu"] = "Contracts";
        ViewData["Filter"]     = filter ?? "active";

        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        var myBuildings = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .OrderBy(b => b.Name).ToListAsync();

        var query = db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building)
            .Include(c => c.Tenant)
            .Where(c => myBuildingIds.Contains(c.Room.BuildingId));

        if (buildingId.HasValue)
            query = query.Where(c => c.Room.BuildingId == buildingId.Value);

        query = filter switch
        {
            "pending"    => query.Where(c => c.Status == ContractStatus.Pending),
            "expired"    => query.Where(c => c.Status == ContractStatus.Expired || c.Status == ContractStatus.Terminated),
            "all"        => query,
            _            => query.Where(c => c.Status == ContractStatus.Active)
        };

        var list = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        ViewBag.Buildings  = myBuildings;
        ViewBag.BuildingId = buildingId;
        return View(list);
    }

    public async Task<IActionResult> ContractDetail(int id)
    {
        ViewData["ActiveMenu"] = "Contracts";

        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        var contract = await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building)
            .Include(c => c.Tenant)
            .Include(c => c.Members).ThenInclude(m => m.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id && myBuildingIds.Contains(c.Room.BuildingId));

        if (contract == null) return NotFound();

        return View(contract);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateContract(int id)
    {
        var contract = await GetOwnedContract(id);
        if (contract == null) return NotFound();

        if (contract.Status != ContractStatus.Pending)
        {
            TempData["Error"] = "Chỉ có thể kích hoạt hợp đồng đang Pending.";
            return RedirectToAction(nameof(ContractDetail), new { id });
        }

        contract.Status = ContractStatus.Active;

        // Set room to Occupied
        var room = await db.Rooms.FindAsync(contract.RoomId);
        if (room != null) room.Status = RoomStatus.Occupied;

        await db.SaveChangesAsync();

        TempData["Success"] = "Hợp đồng đã được kích hoạt. Phòng chuyển sang trạng thái Đã thuê.";
        return RedirectToAction(nameof(ContractDetail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TerminateContract(int id, string? terminationReason, DateTime? actualEndDate)
    {
        var contract = await GetOwnedContract(id);
        if (contract == null) return NotFound();

        if (contract.Status != ContractStatus.Active && contract.Status != ContractStatus.Pending)
        {
            TempData["Error"] = "Hợp đồng đã chấm dứt hoặc hết hạn.";
            return RedirectToAction(nameof(ContractDetail), new { id });
        }

        contract.Status            = ContractStatus.Terminated;
        contract.TerminationReason = string.IsNullOrWhiteSpace(terminationReason) ? null : terminationReason.Trim();
        contract.ActualEndDate     = actualEndDate ?? DateTime.UtcNow;

        // Free up the room
        var room = await db.Rooms.FindAsync(contract.RoomId);
        if (room != null) room.Status = RoomStatus.Available;

        await db.SaveChangesAsync();

        TempData["Success"] = "Đã chấm dứt hợp đồng. Phòng trở về trạng thái Trống.";
        return RedirectToAction(nameof(ContractDetail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContractInfo(int id, int? desiredOccupancy, string? notes)
    {
        var contract = await GetOwnedContract(id);
        if (contract == null) return NotFound();

        contract.DesiredOccupancy = desiredOccupancy;
        contract.Notes            = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật thông tin hợp đồng.";
        return RedirectToAction(nameof(ContractDetail), new { id });
    }

    // ════════════════════════════════════════════════════════
    // CONTRACT MEMBERS  (Task 22)
    // ════════════════════════════════════════════════════════

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int contractId, string tenantEmail, DateTime joinDate, string? note)
    {
        var contract = await GetOwnedContract(contractId);
        if (contract == null) return NotFound();

        if (contract.Status != ContractStatus.Active && contract.Status != ContractStatus.Pending)
        {
            TempData["Error"] = "Chỉ có thể thêm thành viên vào hợp đồng Pending hoặc Active.";
            return RedirectToAction(nameof(ContractDetail), new { id = contractId });
        }

        var tenant = await db.Users.FirstOrDefaultAsync(u => u.Email == tenantEmail.Trim());
        if (tenant == null)
        {
            TempData["Error"] = $"Không tìm thấy tài khoản Tenant với email <strong>{tenantEmail}</strong>.";
            return RedirectToAction(nameof(ContractDetail), new { id = contractId });
        }

        // Check capacity
        if (contract.DesiredOccupancy.HasValue)
        {
            var activeCount = await db.ContractMembers
                .CountAsync(m => m.ContractId == contractId && m.LeaveDate == null);
            if (activeCount + 1 >= contract.DesiredOccupancy.Value) // +1 for primary tenant
            {
                TempData["Error"] = "Phòng đã đủ số người theo DesiredOccupancy.";
                return RedirectToAction(nameof(ContractDetail), new { id = contractId });
            }
        }

        // Prevent duplicate active member
        var duplicate = await db.ContractMembers.AnyAsync(m =>
            m.ContractId == contractId && m.TenantId == tenant.Id && m.LeaveDate == null);
        if (duplicate)
        {
            TempData["Error"] = $"<strong>{tenant.FullName}</strong> đã là thành viên active của hợp đồng này.";
            return RedirectToAction(nameof(ContractDetail), new { id = contractId });
        }

        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contractId,
            TenantId   = tenant.Id,
            JoinDate   = joinDate,
            Note       = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm <strong>{tenant.FullName}</strong> vào hợp đồng.";
        return RedirectToAction(nameof(ContractDetail), new { id = contractId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int memberId, DateTime leaveDate)
    {
        var member = await db.ContractMembers
            .Include(m => m.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building)
            .FirstOrDefaultAsync(m => m.Id == memberId);

        if (member == null) return NotFound();

        // Verify ownership
        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();
        if (!myBuildingIds.Contains(member.Contract.Room.BuildingId)) return Forbid();

        member.LeaveDate = leaveDate;
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã ghi nhận ngày ra của thành viên.";
        return RedirectToAction(nameof(ContractDetail), new { id = member.ContractId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSeekingRoommates(int id)
    {
        var contract = await GetOwnedContract(id);
        if (contract == null) return NotFound();

        contract.SeekingRoommates = !contract.SeekingRoommates;
        await db.SaveChangesAsync();

        TempData["Success"] = contract.SeekingRoommates
            ? "Đã bật tìm ghép phòng." : "Đã tắt tìm ghép phòng.";
        return RedirectToAction(nameof(ContractDetail), new { id });
    }

    // ── Task 23: Export PDF ───────────────────────────────────────────────────

    public async Task<IActionResult> ExportContractPdf(int id)
    {
        var contract = await GetOwnedContractFull(id);
        if (contract == null) return NotFound();

        QuestPDF.Settings.License = LicenseType.Community;
        var doc  = new ContractPdfDocument(contract);
        var bytes = doc.GeneratePdf();
        return File(bytes, "application/pdf", $"HopDong_{contract.Id:D4}.pdf");
    }

    // ── Task 24: Export Word ──────────────────────────────────────────────────

    public async Task<IActionResult> ExportContractDocx(int id)
    {
        var contract = await GetOwnedContractFull(id);
        if (contract == null) return NotFound();

        using var ms = new MemoryStream();
        BuildWordDocument(contract, ms);
        ms.Position = 0;
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"HopDong_{contract.Id:D4}.docx");
    }

    static void BuildWordDocument(Contract contract, Stream stream)
    {
        var room   = contract.Room;
        var bldg   = room.Building;
        var tenant = contract.Tenant;

        using var wordDoc = WordprocessingDocument.Create(stream,
            WordprocessingDocumentType.Document, true);

        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new WpDocument();
        var body = mainPart.Document.AppendChild(new Body());

        // page margins
        var sectPr = new SectionProperties(
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134 });
        body.AppendChild(sectPr);

        static Paragraph CenteredPara(string text, bool bold = false, int fontSize = 24)
        {
            var rp = new RunProperties(new FontSize { Val = fontSize.ToString() });
            if (bold) { rp.AppendChild(new Bold()); rp.AppendChild(new BoldComplexScript()); }
            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(rp, new Text(text)));
        }

        static Paragraph NormalPara(string text, int fontSize = 22)
            => new(new Run(new RunProperties(new FontSize { Val = fontSize.ToString() }),
                   new Text(text)));

        static Paragraph InfoPara(string label, string value)
            => new(new Run(
                   new RunProperties(new FontSize { Val = "22" }),
                   new Text($"    {label}: ")),
               new Run(
                   new RunProperties(new Bold(), new BoldComplexScript(),
                       new FontSize { Val = "22" }),
                   new Text(value)));

        static Paragraph HeadingPara(string text)
            => new(new Run(
                   new RunProperties(new Bold(), new BoldComplexScript(),
                       new FontSize { Val = "24" }),
                   new Text(text)));

        // Header
        body.AppendChild(CenteredPara("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", true, 26));
        body.AppendChild(CenteredPara("Độc lập - Tự do - Hạnh phúc", true, 24));
        body.AppendChild(CenteredPara("━━━━━━━━━━━━━━━━━━━━━", false, 20));
        body.AppendChild(CenteredPara("HỢP ĐỒNG THUÊ PHÒNG TRỌ", true, 28));
        body.AppendChild(CenteredPara(
            $"Số: {contract.Id:D4}/{DateTime.Now.Year}/HĐTT", false, 20));
        body.AppendChild(NormalPara(""));

        // Bên A
        body.AppendChild(HeadingPara("ĐIỀU 1 – BÊN CHO THUÊ (BÊN A)"));
        body.AppendChild(InfoPara("Họ và tên", bldg.Landlord?.FullName ?? bldg.Name));
        body.AppendChild(InfoPara("Tòa nhà",   bldg.Name));
        body.AppendChild(InfoPara("Địa chỉ",
            $"{bldg.Address}, {bldg.Ward}, {bldg.District}, {bldg.Province}"));
        body.AppendChild(NormalPara(""));

        // Bên B
        body.AppendChild(HeadingPara("ĐIỀU 2 – BÊN THUÊ (BÊN B)"));
        body.AppendChild(InfoPara("Họ và tên",       tenant.FullName ?? tenant.UserName ?? "—"));
        body.AppendChild(InfoPara("Email",            tenant.Email ?? "—"));
        body.AppendChild(InfoPara("Số điện thoại",   tenant.PhoneNumber ?? "—"));
        body.AppendChild(NormalPara(""));

        // Tài sản
        body.AppendChild(HeadingPara("ĐIỀU 3 – TÀI SẢN THUÊ"));
        body.AppendChild(InfoPara("Phòng số",  room.RoomNumber));
        body.AppendChild(InfoPara("Tòa nhà",   bldg.Name));
        body.AppendChild(InfoPara("Địa chỉ",
            $"{bldg.Address}, {bldg.Ward}, {bldg.District}, {bldg.Province}"));
        body.AppendChild(InfoPara("Diện tích", $"{room.Area:N1} m²"));
        body.AppendChild(InfoPara("Sức chứa",
            room.Capacity.HasValue ? $"{room.Capacity} người" : "Không giới hạn"));
        body.AppendChild(NormalPara(""));

        // Thời hạn
        body.AppendChild(HeadingPara("ĐIỀU 4 – THỜI HẠN VÀ GIÁ THUÊ"));
        body.AppendChild(InfoPara("Ngày bắt đầu",    contract.StartDate.ToString("dd/MM/yyyy")));
        body.AppendChild(InfoPara("Ngày kết thúc",
            contract.EndDate.HasValue ? contract.EndDate.Value.ToString("dd/MM/yyyy") : "Không xác định"));
        body.AppendChild(InfoPara("Giá thuê/tháng",  $"{contract.MonthlyRent:N0} đồng"));
        body.AppendChild(InfoPara("Tiền cọc",        $"{contract.DepositAmount:N0} đồng"));
        body.AppendChild(NormalPara(""));

        // Điều khoản chung
        body.AppendChild(HeadingPara("ĐIỀU 5 – ĐIỀU KHOẢN CHUNG"));
        body.AppendChild(NormalPara("1. Bên B có trách nhiệm thanh toán tiền thuê đúng hạn vào đầu mỗi tháng."));
        body.AppendChild(NormalPara("2. Bên B không được tự ý sửa chữa, cải tạo phòng khi chưa có sự đồng ý của Bên A."));
        body.AppendChild(NormalPara("3. Bên B có trách nhiệm giữ gìn vệ sinh chung và tuân thủ nội quy tòa nhà."));
        body.AppendChild(NormalPara("4. Khi chấm dứt hợp đồng, Bên B phải thông báo trước ít nhất 30 ngày."));
        body.AppendChild(NormalPara("5. Tiền cọc sẽ được hoàn trả sau khi Bên B bàn giao phòng và thanh toán đầy đủ."));
        body.AppendChild(NormalPara(""));

        if (!string.IsNullOrWhiteSpace(contract.Notes))
        {
            body.AppendChild(HeadingPara("ĐIỀU 6 – GHI CHÚ THÊM"));
            body.AppendChild(NormalPara(contract.Notes));
            body.AppendChild(NormalPara(""));
        }

        // Ký tên (2 cột dùng table)
        body.AppendChild(NormalPara(""));
        var tbl = new Table(
            new TableProperties(
                new TableWidth { Width = "9638", Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder    { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder   { Val = BorderValues.None },
                    new RightBorder  { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder   { Val = BorderValues.None })));

        var sigRow = new TableRow();
        var cellA  = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "4819", Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "22" }), new Text("BÊN A"))),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }), new Text("(Chủ trọ)"))),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }),
                    new Text(bldg.Landlord?.FullName ?? bldg.Name))));

        var cellB = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "4819", Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "22" }), new Text("BÊN B"))),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }), new Text("(Người thuê)"))),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new Text("")),
            new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new FontSize { Val = "20" }),
                    new Text(tenant.FullName ?? "—"))));

        sigRow.AppendChild(cellA);
        sigRow.AppendChild(cellB);
        tbl.AppendChild(sigRow);
        body.AppendChild(tbl);

        mainPart.Document.Save();
    }

    private async Task<Contract?> GetOwnedContract(int id)
    {
        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        return await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building)
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id && myBuildingIds.Contains(c.Room.BuildingId));
    }

    private async Task<Contract?> GetOwnedContractFull(int id)
    {
        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        return await db.Contracts
            .Include(c => c.Room).ThenInclude(r => r.Building).ThenInclude(b => b.Landlord)
            .Include(c => c.Tenant)
            .Include(c => c.Members).ThenInclude(m => m.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id && myBuildingIds.Contains(c.Room.BuildingId));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // INVOICES — Task 25, 26, 27
    // ════════════════════════════════════════════════════════════════════════════

    // Task 25 — Danh sách hoá đơn ─────────────────────────────────────────────

    public async Task<IActionResult> Invoices(string? filter, int? buildingId)
    {
        ViewData["ActiveMenu"] = "Invoices";
        filter ??= "unpaid";

        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        var buildings = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .OrderBy(b => b.Name).ToListAsync();

        var q = db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building)
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Where(i => myBuildingIds.Contains(i.Contract.Room.BuildingId));

        if (buildingId.HasValue)
            q = q.Where(i => i.Contract.Room.BuildingId == buildingId.Value);

        q = filter switch
        {
            "draft"     => q.Where(i => i.Status == InvoiceStatus.Draft),
            "sent"      => q.Where(i => i.Status == InvoiceStatus.Sent),
            "paid"      => q.Where(i => i.Status == InvoiceStatus.Paid),
            "overdue"   => q.Where(i => i.Status == InvoiceStatus.Overdue),
            "unpaid"    => q.Where(i => i.Status == InvoiceStatus.Draft
                                     || i.Status == InvoiceStatus.Sent
                                     || i.Status == InvoiceStatus.Overdue),
            _           => q
        };

        var invoices = await q.OrderByDescending(i => i.BillingYear)
                               .ThenByDescending(i => i.BillingMonth)
                               .ThenByDescending(i => i.Id)
                               .ToListAsync();

        ViewData["Filter"]     = filter;
        ViewBag.Buildings      = buildings;
        ViewBag.BuildingId     = buildingId;
        return View(invoices);
    }

    // Task 25 — Tạo hoá đơn mới (GET) ────────────────────────────────────────

    public async Task<IActionResult> CreateInvoice(int contractId)
    {
        var contract = await GetOwnedContractFull(contractId);
        if (contract == null || contract.Status != ContractStatus.Active)
            return NotFound();

        // Gộp fee configs (room-level override building-level)
        var roomFees     = await db.FeeConfigs
            .Where(f => f.RoomId == contract.RoomId && f.IsActive).ToListAsync();
        var buildingFees = await db.FeeConfigs
            .Where(f => f.BuildingId == contract.Room.BuildingId && f.RoomId == null && f.IsActive)
            .ToListAsync();
        var roomCategories = roomFees.Select(f => f.FeeCategory).ToHashSet();
        var effectiveFees  = roomFees
            .Concat(buildingFees.Where(f => !roomCategories.Contains(f.FeeCategory)))
            .OrderBy(f => f.SortOrder).ToList();

        ViewBag.Contract     = contract;
        ViewBag.EffectiveFees = effectiveFees;
        ViewBag.Now          = DateTime.Now;
        return View(contract);
    }

    // Task 25 — Tạo hoá đơn mới (POST) ───────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateInvoice(int contractId, int billingYear,
        int billingMonth, DateTime dueDate, string? notes,
        [FromForm] List<int> feeConfigIds,
        [FromForm] List<string> descriptions,
        [FromForm] List<decimal?> quantities,
        [FromForm] List<decimal> unitPrices,
        [FromForm] List<decimal?> previousReadings,
        [FromForm] List<decimal?> currentReadings)
    {
        var contract = await GetOwnedContractFull(contractId);
        if (contract == null || contract.Status != ContractStatus.Active)
            return NotFound();

        // Check duplicate invoice for same month
        var dup = await db.Invoices
            .AnyAsync(i => i.ContractId == contractId
                        && i.BillingYear == billingYear
                        && i.BillingMonth == billingMonth);
        if (dup)
        {
            TempData["Error"] = $"Đã có hoá đơn tháng {billingMonth}/{billingYear} cho hợp đồng này.";
            return RedirectToAction(nameof(CreateInvoice), new { contractId });
        }

        // Active member count (primary + members) for split calculation
        int activeMembers = 1 + contract.Members
            .Count(m => !m.LeaveDate.HasValue || m.LeaveDate > DateTime.UtcNow);

        var invoice = new Invoice
        {
            ContractId   = contractId,
            BillingYear  = billingYear,
            BillingMonth = billingMonth,
            DueDate      = dueDate,
            Notes        = notes,
            Status       = InvoiceStatus.Draft
        };

        decimal total = 0;
        for (int i = 0; i < descriptions.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(descriptions[i])) continue;

            // Tính amount
            decimal amount;
            if (quantities[i].HasValue && unitPrices[i] > 0)
            {
                decimal qty = quantities[i]!.Value;
                // PerUnit (điện/nước): chia đều 1/N người ghép
                int? cfId = i < feeConfigIds.Count ? feeConfigIds[i] : (int?)null;
                bool isPerUnit = cfId > 0 && await db.FeeConfigs
                    .AnyAsync(f => f.Id == cfId && f.CalcType == CalcType.PerUnit);
                amount = isPerUnit
                    ? Math.Round(qty * unitPrices[i] / activeMembers, 0)
                    : qty * unitPrices[i];
            }
            else
            {
                amount = unitPrices[i];
            }

            invoice.Items.Add(new InvoiceItem
            {
                FeeConfigId      = i < feeConfigIds.Count && feeConfigIds[i] > 0 ? feeConfigIds[i] : null,
                Description      = descriptions[i],
                Quantity         = quantities[i],
                UnitPrice        = unitPrices[i],
                Amount           = amount,
                PreviousReading  = i < previousReadings.Count ? previousReadings[i] : null,
                CurrentReading   = i < currentReadings.Count ? currentReadings[i] : null
            });
            total += amount;
        }

        invoice.TotalAmount = total;
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã tạo hoá đơn tháng {billingMonth}/{billingYear}.";
        return RedirectToAction(nameof(InvoiceDetail), new { id = invoice.Id });
    }

    // Task 26 — Chi tiết hoá đơn ──────────────────────────────────────────────

    public async Task<IActionResult> InvoiceDetail(int id)
    {
        ViewData["ActiveMenu"] = "Invoices";
        var invoice = await GetOwnedInvoice(id);
        if (invoice == null) return NotFound();
        return View(invoice);
    }

    // Task 26 — Gửi hoá đơn cho Tenant (email) ────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> SendInvoice(int id)
    {
        var invoice = await GetOwnedInvoice(id);
        if (invoice == null) return NotFound();

        if (invoice.Status == InvoiceStatus.Draft || invoice.Status == InvoiceStatus.Overdue)
        {
            invoice.Status = InvoiceStatus.Sent;
            await db.SaveChangesAsync();
        }

        var tenant  = invoice.Contract.Tenant;
        var room    = invoice.Contract.Room;
        var subject = $"[StuRoom] Hoá đơn tháng {invoice.BillingMonth}/{invoice.BillingYear} — Phòng {room.RoomNumber}";
        var body    = $@"
<p>Xin chào <strong>{tenant.FullName ?? tenant.Email}</strong>,</p>
<p>Bạn có hoá đơn tháng <strong>{invoice.BillingMonth}/{invoice.BillingYear}</strong> cho phòng <strong>{room.RoomNumber} — {room.Building.Name}</strong>.</p>
<table border='1' cellpadding='6' cellspacing='0' style='border-collapse:collapse;font-family:sans-serif;font-size:14px'>
  <thead><tr style='background:#f3f4f6'>
    <th>Khoản phí</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th>
  </tr></thead>
  <tbody>
    {string.Join("", invoice.Items.Select(it =>
        $"<tr><td>{it.Description}</td><td>{(it.Quantity.HasValue ? it.Quantity.Value.ToString("N2") : "—")}</td><td>{it.UnitPrice:N0} ₫</td><td>{it.Amount:N0} ₫</td></tr>"))}
  </tbody>
  <tfoot><tr><td colspan='3'><strong>Tổng cộng</strong></td><td><strong>{invoice.TotalAmount:N0} ₫</strong></td></tr></tfoot>
</table>
<p>Hạn thanh toán: <strong>{invoice.DueDate:dd/MM/yyyy}</strong></p>
<p>Vui lòng liên hệ chủ trọ nếu có thắc mắc.</p>";

        await emailSender.SendEmailAsync(tenant.Email!, subject, body);

        // Task 30 — notify Tenant
        await notifier.SendAsync(invoice.Contract.TenantId, NotificationType.InvoiceDue,
            $"Hoá đơn tháng {invoice.BillingMonth}/{invoice.BillingYear}",
            $"Tổng: {invoice.TotalAmount:N0} ₫ — Hạn: {invoice.DueDate:dd/MM/yyyy}.",
            "Invoice", invoice.Id);

        TempData["Success"] = "Đã gửi hoá đơn đến tenant.";
        return RedirectToAction(nameof(InvoiceDetail), new { id });
    }

    // Task 27 — Ghi nhận thanh toán ───────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> RecordPayment(int invoiceId, decimal amount,
        DateTime paymentDate, PaymentMethod method,
        string? transactionRef, string? notes)
    {
        var invoice = await GetOwnedInvoice(invoiceId);
        if (invoice == null) return NotFound();

        var payment = new Payment
        {
            InvoiceId      = invoiceId,
            Amount         = amount,
            PaymentDate    = paymentDate,
            Method         = method,
            TransactionRef = transactionRef,
            Notes          = notes,
            RecordedById   = CurrentUserId
        };
        db.Payments.Add(payment);

        // Tổng đã thanh toán
        var totalPaid = invoice.Payments.Sum(p => p.Amount) + amount;
        if (totalPaid >= invoice.TotalAmount)
            invoice.Status = InvoiceStatus.Paid;
        else if (invoice.Status == InvoiceStatus.Draft)
            invoice.Status = InvoiceStatus.Sent;

        await db.SaveChangesAsync();

        TempData["Success"] = $"Đã ghi nhận thanh toán {amount:N0} ₫.";
        return RedirectToAction(nameof(InvoiceDetail), new { id = invoiceId });
    }

    // Task 27 — Huỷ hoá đơn ──────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CancelInvoice(int id)
    {
        var invoice = await GetOwnedInvoice(id);
        if (invoice == null) return NotFound();

        if (invoice.Status != InvoiceStatus.Paid)
        {
            invoice.Status = InvoiceStatus.Cancelled;
            await db.SaveChangesAsync();
            TempData["Success"] = "Đã huỷ hoá đơn.";
        }
        return RedirectToAction(nameof(InvoiceDetail), new { id });
    }

    private async Task<Invoice?> GetOwnedInvoice(int id)
    {
        var myBuildingIds = await db.Buildings
            .Where(b => b.LandlordId == CurrentUserId)
            .Select(b => b.Id).ToListAsync();

        return await db.Invoices
            .Include(i => i.Contract).ThenInclude(c => c.Room).ThenInclude(r => r.Building)
            .Include(i => i.Contract).ThenInclude(c => c.Tenant)
            .Include(i => i.Contract).ThenInclude(c => c.Members)
            .Include(i => i.Items).ThenInclude(it => it.FeeConfig)
            .Include(i => i.Payments).ThenInclude(p => p.RecordedBy)
            .FirstOrDefaultAsync(i => i.Id == id
                && myBuildingIds.Contains(i.Contract.Room.BuildingId));
    }
}
