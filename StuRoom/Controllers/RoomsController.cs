using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Models.ViewModels;

namespace StuRoom.Controllers;

/// <summary>Public marketplace — no authentication required.</summary>
public class RoomsController(ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    // ════════════════════════════════════════════════════════
    // INDEX — danh sách + bộ lọc  (Task 12 + 13)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Index(RoomSearchViewModel filter)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.FavoriteRoomIds = await db.FavoriteRooms
                    .Where(f => f.TenantId == userId)
                    .Select(f => f.RoomId)
                    .ToListAsync();
            }
        }
        else
        {
            ViewBag.FavoriteRoomIds = new List<int>();
        }

        // Base query: only Available rooms
        var query = db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images)
            .Include(r => r.RoomAmenities).ThenInclude(ra => ra.Amenity)
            .Include(r => r.FeeConfigs)
            .Include(r => r.Building.FeeConfigs)
            .Include(r => r.Reviews.Where(rv => rv.IsApproved))
            .Where(r => r.Status == RoomStatus.Available)
            .AsQueryable();

        // ── Filters ──────────────────────────────────────────

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim().ToLower();
            query = query.Where(r =>
                r.Building.Name.ToLower().Contains(q) ||
                r.Building.Address.ToLower().Contains(q) ||
                r.Building.Province.ToLower().Contains(q) ||
                r.Building.District.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(filter.Province))
            query = query.Where(r => r.Building.Province == filter.Province);

        if (!string.IsNullOrWhiteSpace(filter.District))
            query = query.Where(r => r.Building.District.Contains(filter.District));

        if (filter.MinArea.HasValue)
            query = query.Where(r => r.Area >= filter.MinArea.Value);

        // Load into memory for rent-price filtering (derived field)
        var rooms = await query.ToListAsync();

        // Compute rent price per room (room-level override → building-level)
        decimal? GetRentPrice(Room r)
        {
            var roomRent = r.FeeConfigs.FirstOrDefault(f =>
                f.RoomId == r.Id && f.FeeCategory == FeeCategory.Rent && f.IsActive);
            if (roomRent != null) return roomRent.UnitPrice;

            var buildingRent = r.Building.FeeConfigs.FirstOrDefault(f =>
                f.BuildingId == r.BuildingId && f.FeeCategory == FeeCategory.Rent && f.IsActive);
            return buildingRent?.UnitPrice;
        }

        // Price filter (in-memory)
        if (filter.MinPrice.HasValue)
            rooms = rooms.Where(r => GetRentPrice(r) >= filter.MinPrice.Value).ToList();
        if (filter.MaxPrice.HasValue)
            rooms = rooms.Where(r => GetRentPrice(r) == null || GetRentPrice(r) <= filter.MaxPrice.Value).ToList();

        // Amenity filter: room must have ALL selected amenities
        if (filter.AmenityIds.Any())
        {
            rooms = rooms.Where(r =>
                filter.AmenityIds.All(aid =>
                    r.RoomAmenities.Any(ra => ra.AmenityId == aid))).ToList();
        }

        // ── Project to card VM ────────────────────────────────
        var cards = rooms.Select(r =>
        {
            var rentCfg = r.FeeConfigs.FirstOrDefault(f => f.RoomId == r.Id && f.FeeCategory == FeeCategory.Rent && f.IsActive)
                       ?? r.Building.FeeConfigs.FirstOrDefault(f => f.BuildingId == r.BuildingId && f.FeeCategory == FeeCategory.Rent && f.IsActive);

            var approvedReviews = r.Reviews.Where(rv => rv.IsApproved).ToList();

            return new RoomCardViewModel
            {
                Id            = r.Id,
                RoomNumber    = r.RoomNumber,
                BuildingName  = r.Building.Name,
                Address       = r.Building.Address,
                Province      = r.Building.Province,
                District      = r.Building.District,
                Ward          = r.Building.Ward,
                Area          = r.Area,
                Capacity      = r.Capacity,
                PrimaryImageUrl = r.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? r.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
                RentPrice     = rentCfg?.UnitPrice,
                RentUnit      = rentCfg?.Unit ?? "tháng",
                Amenities     = r.RoomAmenities.Select(ra => (ra.Amenity.IconClass, ra.Amenity.Name)).ToList(),
                AvgRating     = approvedReviews.Any() ? approvedReviews.Average(rv => (double)rv.Rating) : null,
                ReviewCount   = approvedReviews.Count
            };
        }).ToList();

        // Sort: has-image first, then by price asc
        cards = [.. cards
            .OrderByDescending(c => c.PrimaryImageUrl != null)
            .ThenBy(c => c.RentPrice ?? decimal.MaxValue)];

        // Filter options
        var allProvinces = await db.Buildings
            .Where(b => !string.IsNullOrEmpty(b.Province))
            .Select(b => b.Province).Distinct().OrderBy(p => p).ToListAsync();

        filter.Rooms        = cards;
        filter.TotalCount   = cards.Count;
        filter.AllAmenities = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
        filter.Provinces    = allProvinces;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.FavoriteRoomIds = await db.FavoriteRooms
                    .Where(f => f.TenantId == userId)
                    .Select(f => f.RoomId)
                    .ToListAsync();
            }
        }
        else
        {
            ViewBag.FavoriteRoomIds = new List<int>();
        }

        return View(filter);
    }

    // ════════════════════════════════════════════════════════
    // DETAIL  (Task 14)
    // ════════════════════════════════════════════════════════

    public async Task<IActionResult> Detail(int id)
    {
        var room = await db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images.OrderBy(i => i.SortOrder))
            .Include(r => r.RoomAmenities).ThenInclude(ra => ra.Amenity)
            .Include(r => r.FeeConfigs.Where(f => f.IsActive))
            .Include(r => r.Building.FeeConfigs.Where(f => f.IsActive))
            .Include(r => r.Reviews.Where(rv => rv.IsApproved))
                .ThenInclude(rv => rv.Reviewer)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound();

        // Merge fee configs: room-level overrides building-level for same category
        var feeConfigs = new List<FeeConfig>();
        var roomLevelCats = room.FeeConfigs.Select(f => f.FeeCategory).ToHashSet();
        feeConfigs.AddRange(room.FeeConfigs);
        feeConfigs.AddRange(room.Building.FeeConfigs.Where(f => !roomLevelCats.Contains(f.FeeCategory)));
        feeConfigs = [.. feeConfigs.OrderBy(f => f.SortOrder).ThenBy(f => f.FeeCategory)];

        var rentCfg = feeConfigs.FirstOrDefault(f => f.FeeCategory == FeeCategory.Rent);
        var reviews = room.Reviews.Where(rv => rv.IsApproved)
                          .OrderByDescending(rv => rv.CreatedAt).ToList();

        // ── Check review eligibility ──────────────────────────
        bool canReview       = false;
        int? reviewContractId = null;
        if (User.IsInRole("Tenant"))
        {
            var userId = userManager.GetUserId(User);
            var eligibleContract = await db.Contracts
                .Where(c => c.RoomId == id
                         && c.TenantId == userId
                         && (c.Status == ContractStatus.Expired
                          || c.Status == ContractStatus.Terminated))
                .Where(c => !db.RoomReviews
                    .Any(rv => rv.ContractId == c.Id && rv.ReviewerId == userId))
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();

            if (eligibleContract != null)
            {
                canReview       = true;
                reviewContractId = eligibleContract.Id;
            }
        }

        var vm = new RoomPublicDetailViewModel
        {
            Room             = room,
            Images           = room.Images.ToList(),
            Amenities        = room.RoomAmenities.Select(ra => ra.Amenity).ToList(),
            FeeConfigs       = feeConfigs,
            RentPrice        = rentCfg?.UnitPrice,
            RentUnit         = rentCfg?.Unit ?? "tháng",
            Reviews          = reviews,
            AvgRating        = reviews.Any() ? reviews.Average(rv => (double)rv.Rating) : null,
            CanBook          = User.IsInRole("Tenant") && room.Status == RoomStatus.Available,
            CanReview        = canReview,
            ReviewContractId = reviewContractId
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.IsFavorite = await db.FavoriteRooms
                    .AnyAsync(f => f.TenantId == userId && f.RoomId == id);
            }
        }
        else
        {
            ViewBag.IsFavorite = false;
        }

        return View(vm);
    }

    // ════════════════════════════════════════════════════════
    // FAVORITES & REPORTS
    // ════════════════════════════════════════════════════════

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ToggleFavorite(int roomId)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "Unauthorized" });

        var favorite = await db.FavoriteRooms
            .FirstOrDefaultAsync(f => f.TenantId == userId && f.RoomId == roomId);

        bool isFavorite;
        if (favorite != null)
        {
            db.FavoriteRooms.Remove(favorite);
            isFavorite = false;
        }
        else
        {
            db.FavoriteRooms.Add(new FavoriteRoom
            {
                TenantId = userId,
                RoomId = roomId,
                CreatedAt = DateTime.UtcNow
            });
            isFavorite = true;
        }

        await db.SaveChangesAsync();
        return Json(new { success = true, isFavorite = isFavorite });
    }

    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> Favorites()
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account", new { area = "Identity" });

        var favoriteRoomIds = await db.FavoriteRooms
            .Where(f => f.TenantId == userId)
            .Select(f => f.RoomId)
            .ToListAsync();

        var query = db.Rooms
            .Include(r => r.Building)
            .Include(r => r.Images)
            .Include(r => r.RoomAmenities).ThenInclude(ra => ra.Amenity)
            .Include(r => r.FeeConfigs)
            .Include(r => r.Building.FeeConfigs)
            .Where(r => favoriteRoomIds.Contains(r.Id) && r.Status == RoomStatus.Available);

        var rooms = await query.ToListAsync();

        var cards = rooms.Select(r =>
        {
            var rentCfg = r.FeeConfigs.FirstOrDefault(f => f.RoomId == r.Id && f.FeeCategory == FeeCategory.Rent && f.IsActive)
                       ?? r.Building.FeeConfigs.FirstOrDefault(f => f.BuildingId == r.BuildingId && f.FeeCategory == FeeCategory.Rent && f.IsActive);

            return new RoomCardViewModel
            {
                Id            = r.Id,
                RoomNumber    = r.RoomNumber,
                BuildingName  = r.Building.Name,
                Address       = r.Building.Address,
                Province      = r.Building.Province,
                District      = r.Building.District,
                Ward          = r.Building.Ward,
                Area          = r.Area,
                Capacity      = r.Capacity,
                PrimaryImageUrl = r.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                               ?? r.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
                RentPrice     = rentCfg?.UnitPrice,
                RentUnit      = rentCfg?.Unit ?? "tháng",
                Amenities     = r.RoomAmenities.Select(ra => (ra.Amenity.IconClass, ra.Amenity.Name)).ToList()
            };
        }).ToList();

        return View(cards);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportRoom(int roomId, string reason, string description)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(description))
        {
            TempData["ReviewError"] = "Lý do và nội dung báo cáo không được để trống.";
            return RedirectToAction(nameof(Detail), new { id = roomId });
        }

        var report = new RoomReport
        {
            RoomId = roomId,
            ReporterId = userId,
            Reason = reason.Trim(),
            Description = description.Trim(),
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.RoomReports.Add(report);
        await db.SaveChangesAsync();

        TempData["ReviewSuccess"] = "Đã gửi báo cáo vi phạm. Ban quản trị sẽ sớm xem xét.";
        return RedirectToAction(nameof(Detail), new { id = roomId });
    }
}
