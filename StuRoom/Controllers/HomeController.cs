using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StuRoom.Data;
using StuRoom.Models;
using StuRoom.Models.ViewModels;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace StuRoom.Controllers
{
    public class HomeController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager) : Controller
    {
        public async Task<IActionResult> Index()
        {
            if (User.Identity!.IsAuthenticated)
            {
                var userId = userManager.GetUserId(User);
                ViewBag.FavoriteRoomIds = await db.FavoriteRooms
                    .Where(f => f.TenantId == userId)
                    .Select(f => f.RoomId)
                    .ToListAsync();
            }
            else
            {
                ViewBag.FavoriteRoomIds = new List<int>();
            }

            var rooms = await db.Rooms
                .Include(r => r.Building)
                .Include(r => r.Images)
                .Include(r => r.RoomAmenities).ThenInclude(ra => ra.Amenity)
                .Include(r => r.FeeConfigs)
                .Include(r => r.Building.FeeConfigs)
                .Include(r => r.Reviews.Where(rv => rv.IsApproved))
                .Where(r => r.Status == RoomStatus.Available)
                .OrderByDescending(r => r.CreatedAt)
                .Take(6)
                .ToListAsync();

            var cards = rooms.Select(r =>
            {
                var rentCfg = r.FeeConfigs.FirstOrDefault(f => f.RoomId == r.Id && f.FeeCategory == FeeCategory.Rent && f.IsActive)
                           ?? r.Building.FeeConfigs.FirstOrDefault(f => f.BuildingId == r.BuildingId && f.FeeCategory == FeeCategory.Rent && f.IsActive);

                var approvedReviews = r.Reviews.Where(rv => rv.IsApproved).ToList();

                return new RoomCardViewModel
                {
                    Id              = r.Id,
                    RoomNumber      = r.RoomNumber,
                    BuildingName    = r.Building.Name,
                    Address         = r.Building.Address,
                    Province        = r.Building.Province,
                    District        = r.Building.District,
                    Ward            = r.Building.Ward,
                    Area            = r.Area,
                    Capacity        = r.Capacity,
                    PrimaryImageUrl = r.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                                   ?? r.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
                    RentPrice       = rentCfg?.UnitPrice,
                    RentUnit        = rentCfg?.Unit ?? "tháng",
                    Amenities       = r.RoomAmenities.Select(ra => (ra.Amenity.IconClass, ra.Amenity.Name)).ToList(),
                    AvgRating       = approvedReviews.Any() ? approvedReviews.Average(rv => (double)rv.Rating) : null,
                    ReviewCount     = approvedReviews.Count
                };
            }).ToList();

            return View(cards);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
