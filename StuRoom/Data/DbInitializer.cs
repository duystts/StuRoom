using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StuRoom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StuRoom.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db          = services.GetRequiredService<ApplicationDbContext>();

        // ── 1. Seed roles ──────────────────────────────────────────
        string[] roles = ["Admin", "Landlord", "Tenant"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── 2. Seed Admin account ──────────────────────────────────
        const string adminEmail = "admin@sturoom.com";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName    = adminEmail,
                Email       = adminEmail,
                FullName    = "Quản trị viên hệ thống",
                EmailConfirmed = true,
                IsApproved  = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@123456");

            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }

        // ── 3. Seed Amenities ──────────────────────────────────────
        if (!await db.Amenities.AnyAsync())
        {
            var defaultAmenities = new List<Amenity>
            {
                new() { Name = "WiFi Tốc độ cao", IconClass = "bi-wifi" },
                new() { Name = "Điều hoà nhiệt độ", IconClass = "bi-snow2" },
                new() { Name = "Bình nóng lạnh", IconClass = "bi-droplet" },
                new() { Name = "Máy giặt chung", IconClass = "bi-washing-machine" },
                new() { Name = "Chỗ để xe máy", IconClass = "bi-car-front" },
                new() { Name = "Nhà vệ sinh khép kín", IconClass = "bi-hospital" },
                new() { Name = "Khóa vân tay / Thẻ từ", IconClass = "bi-door-open" },
                new() { Name = "Nội thất cơ bản", IconClass = "bi-house-heart" },
                new() { Name = "Thang máy", IconClass = "bi-arrow-up-square" }
            };
            db.Amenities.AddRange(defaultAmenities);
            await db.SaveChangesAsync();
        }

        // ── 4. Seed Landlord account ────────────────────────────────
        const string landlordEmail = "landlord@sturoom.com";
        if (await userManager.FindByEmailAsync(landlordEmail) is null)
        {
            var landlord = new ApplicationUser
            {
                UserName       = landlordEmail,
                Email          = landlordEmail,
                FullName       = "Nguyễn Văn Trọ (Demo Chủ nhà)",
                EmailConfirmed = true,
                IsApproved     = true
            };
            var result = await userManager.CreateAsync(landlord, "Tenant@123456");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(landlord, "Landlord");
        }

        // ── 5. Seed Tenant account ──────────────────────────────────
        const string tenantEmail = "tenant@sturoom.com";
        if (await userManager.FindByEmailAsync(tenantEmail) is null)
        {
            var tenant = new ApplicationUser
            {
                UserName       = tenantEmail,
                Email          = tenantEmail,
                FullName       = "Trần Văn Sinh (Demo Người thuê)",
                EmailConfirmed = true,
                IsApproved     = true
            };
            var result = await userManager.CreateAsync(tenant, "Tenant@123456");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(tenant, "Tenant");
        }

        // ── 6. Seed Demo Buildings and Rooms ───────────────────────
        var landlordUser = await userManager.FindByEmailAsync(landlordEmail);
        if (landlordUser != null)
        {
            // --- TÒA NHÀ 1: StuRoom House Quận 9 (TP.HCM) ---
            var building1 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom House Quận 9" && b.LandlordId == landlordUser.Id);
            if (building1 == null)
            {
                building1 = new Building
                {
                    LandlordId  = landlordUser.Id,
                    Name        = "StuRoom House Quận 9",
                    Address     = "Số 12, Đường số 5, Phường Tăng Nhơn Phú A",
                    Province    = "Thành phố Hồ Chí Minh",
                    District    = "Quận 9",
                    Ward        = "Phường Tăng Nhơn Phú A",
                    Description = "Tòa nhà chung cư mini dịch vụ dành riêng cho sinh viên học tập tại Khu công nghệ cao và các trường đại học lân cận. Phòng ốc rộng rãi, giờ giấc tự do.",
                    Latitude    = 10.8444,
                    Longitude   = 106.7778,
                    CreatedAt   = DateTime.Now
                };
                db.Buildings.Add(building1);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 2: StuRoom Premier Bình Thạnh (TP.HCM) ---
            var building2 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom Premier Bình Thạnh" && b.LandlordId == landlordUser.Id);
            if (building2 == null)
            {
                building2 = new Building
                {
                    LandlordId  = landlordUser.Id,
                    Name        = "StuRoom Premier Bình Thạnh",
                    Address     = "Số 45/12, Đường Điện Biên Phủ, Phường 25",
                    Province    = "Thành phố Hồ Chí Minh",
                    District    = "Quận Bình Thạnh",
                    Ward        = "Phường 25",
                    Description = "Căn hộ dịch vụ cao cấp ngay ngã tư Hàng Xanh, thuận tiện di chuyển đến các trường đại học HUTECH, UEF, Ngoại thương. Phòng full nội thất, ban công thoáng mát.",
                    Latitude    = 10.8018,
                    Longitude   = 106.7119,
                    CreatedAt   = DateTime.Now
                };
                db.Buildings.Add(building2);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 3: StuRoom Garden Thủ Đức (TP.HCM) ---
            var building3 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom Garden Thủ Đức" && b.LandlordId == landlordUser.Id);
            if (building3 == null)
            {
                building3 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "StuRoom Garden Thủ Đức",
                    Address = "Số 88, Đường Lê Hồng Phong, Phường 12",
                    Province = "Thành phố Hồ Chí Minh",
                    District = "Quận Thủ Đức",
                    Ward = "Phường 12",
                    Description = "Khu căn hộ dịch vụ mini có khuôn viên xanh mát cạnh công viên, môi trường yên tĩnh thích hợp cho học tập và làm việc.",
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building3);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 4: StuRoom Homestay Cầu Giấy (Hà Nội) ---
            var building4 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom Homestay Cầu Giấy" && b.LandlordId == landlordUser.Id);
            if (building4 == null)
            {
                building4 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "StuRoom Homestay Cầu Giấy",
                    Address = "Số 15, Ngõ 86, Đường Dịch Vọng Hậu",
                    Province = "Hà Nội",
                    District = "Quận Cầu Giấy",
                    Ward = "Phường Dịch Vọng Hậu",
                    Description = "Homestay giường tầng cao cấp dành cho sinh viên ĐHQG, Học viện Báo chí. Không gian chung hiện đại, có tủ đồ cá nhân khóa riêng, bếp ăn tự nấu tiện lợi.",
                    Latitude = 21.0285,
                    Longitude = 105.7822,
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building4);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 5: Nhà trọ Sinh viên Chùa Bộc (Hà Nội) ---
            var building5 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "Nhà trọ Sinh viên Chùa Bộc" && b.LandlordId == landlordUser.Id);
            if (building5 == null)
            {
                building5 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "Nhà trọ Sinh viên Chùa Bộc",
                    Address = "Số 8, Ngõ 95, Đường Chùa Bộc",
                    Province = "Hà Nội",
                    District = "Quận Đống Đa",
                    Ward = "Phường Trung Liệt",
                    Description = "Khu nhà trọ sinh viên gần ĐH Thủy Lợi, ĐH Công Đoàn, Học viện Ngân Hàng. Giá cả bình dân, an ninh tốt, gần chợ sầm uất.",
                    Latitude = 21.0075,
                    Longitude = 105.8278,
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building5);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 6: Căn hộ dịch vụ Tô Hiến Thành (TP.HCM) ---
            var building6 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "Căn hộ dịch vụ Tô Hiến Thành" && b.LandlordId == landlordUser.Id);
            if (building6 == null)
            {
                building6 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "Căn hộ dịch vụ Tô Hiến Thành",
                    Address = "Số 220, Đường Tô Hiến Thành, Phường 15",
                    Province = "Thành phố Hồ Chí Minh",
                    District = "Quận 10",
                    Ward = "Phường 15",
                    Description = "Căn hộ dịch vụ mini Quận 10, gần ĐH Bách Khoa, ĐH Y Phạm Ngọc Thạch. Vị trí trung tâm cực kỳ thuận tiện đi lại giữa các quận 1, 3, 5.",
                    Latitude = 10.7797,
                    Longitude = 106.6669,
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building6);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 7: Phòng trọ giá rẻ Quang Trung (TP.HCM) ---
            var building7 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "Phòng trọ giá rẻ Quang Trung" && b.LandlordId == landlordUser.Id);
            if (building7 == null)
            {
                building7 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "Phòng trọ giá rẻ Quang Trung",
                    Address = "Số 450/8, Đường Quang Trung, Phường 10",
                    Province = "Thành phố Hồ Chí Minh",
                    District = "Quận Gò Vấp",
                    Ward = "Phường 10",
                    Description = "Phòng trọ giá học sinh sinh viên tại Gò Vấp, gần ĐH Công nghiệp TP.HCM (IUH). Khu trọ tự quản an toàn, không chung chủ, giờ giấc hoàn toàn tự do.",
                    Latitude = 10.8285,
                    Longitude = 106.6785,
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building7);
                await db.SaveChangesAsync();
            }

            // --- TÒA NHÀ 8: StuRoom Tây Hồ Lakeview (Hà Nội) ---
            var building8 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom Tây Hồ Lakeview" && b.LandlordId == landlordUser.Id);
            if (building8 == null)
            {
                building8 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "StuRoom Tây Hồ Lakeview",
                    Address = "Số 48, Ngõ 31, Đường Xuân Diệu",
                    Province = "Hà Nội",
                    District = "Quận Tây Hồ",
                    Ward = "Phường Quảng An",
                    Description = "Căn hộ dịch vụ cao cấp view Hồ Tây thoáng mát. Thích hợp cho sinh viên muốn trải nghiệm không gian sống chất lượng cao hoặc người đi làm ngoại quốc.",
                    Latitude = 21.0622,
                    Longitude = 105.8272,
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building8);
                await db.SaveChangesAsync();
            }

            // ── Seed building-level fee configs ──
            var allSeededBuildings = new[] { building1, building2, building3, building4, building5, building6, building7, building8 };
            foreach (var b in allSeededBuildings)
            {
                if (!await db.FeeConfigs.AnyAsync(f => f.BuildingId == b.Id && f.RoomId == null))
                {
                    decimal elecPrice = b.Province == "Hà Nội" ? 4000m : 3500m;
                    decimal waterPrice = b.Province == "Hà Nội" ? 22000m : 18000m;
                    decimal netPrice = b.Province == "Hà Nội" ? 100000m : 120000m;

                    db.FeeConfigs.AddRange(new List<FeeConfig>
                    {
                        new() { BuildingId = b.Id, Name = "Điện sinh hoạt", FeeCategory = FeeCategory.Electricity, CalcType = CalcType.PerUnit, UnitPrice = elecPrice, Unit = "kWh", IsActive = true, SortOrder = 1 },
                        new() { BuildingId = b.Id, Name = "Nước sinh hoạt", FeeCategory = FeeCategory.Water, CalcType = CalcType.PerUnit, UnitPrice = waterPrice, Unit = "m³", IsActive = true, SortOrder = 2 },
                        new() { BuildingId = b.Id, Name = "Internet cáp quang", FeeCategory = FeeCategory.Internet, CalcType = CalcType.Fixed, UnitPrice = netPrice, Unit = "tháng", IsActive = true, SortOrder = 3 }
                    });
                }
            }
            await db.SaveChangesAsync();

            // ── Seed Rooms for Building 1 ──
            var roomsB1 = new List<Room>
            {
                new() { BuildingId = building1.Id, RoomNumber = "101", FloorNumber = 1, Area = 25.5m, Capacity = 2, Description = "Phòng gác lửng tràn ngập ánh sáng tự nhiên, khu bếp riêng biệt sạch sẽ.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "102", FloorNumber = 1, Area = 22.0m, Capacity = 2, Description = "Phòng trọ ấm cúng sạch sẽ, phù hợp cho 1-2 người ở thoải mái.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "103", FloorNumber = 1, Area = 20.0m, Capacity = 2, Description = "Phòng nhỏ gọn tiện lợi, trang bị sẵn nội thất và các tiện ích cơ bản.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "201", FloorNumber = 2, Area = 28.0m, Capacity = 3, Description = "Phòng tầng cao, lộng gió thoáng mát, ban công rộng rãi hướng mát.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "202", FloorNumber = 2, Area = 30.0m, Capacity = 3, Description = "Phòng lớn ban công rộng rãi thoáng đãng, thích hợp nhóm 2-3 bạn ở ghép share tiền.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "203", FloorNumber = 2, Area = 26.0m, Capacity = 2, Description = "Phòng thiết kế hiện đại, đầy đủ tiện ích sinh hoạt khép kín.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 2 ──
            var roomsB2 = new List<Room>
            {
                new() { BuildingId = building2.Id, RoomNumber = "301", FloorNumber = 3, Area = 35.0m, Capacity = 3, Description = "Phòng Premier sang trọng cao cấp, ban công view Landmark 81 cực đẹp về đêm.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building2.Id, RoomNumber = "302", FloorNumber = 3, Area = 40.0m, Capacity = 4, Description = "Phòng cực kỳ rộng rãi thích hợp cho nhóm sinh viên muốn share chi phí tối đa.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building2.Id, RoomNumber = "303", FloorNumber = 3, Area = 32.0m, Capacity = 2, Description = "Phòng yên tĩnh, nội thất mới hoàn toàn, thích hợp cho học tập và nghiên cứu.", Status = RoomStatus.Maintenance, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 3 ──
            var roomsB3 = new List<Room>
            {
                new() { BuildingId = building3.Id, RoomNumber = "401", FloorNumber = 4, Area = 28.0m, Capacity = 2, Description = "Phòng có ban công xanh mát hướng công viên, đầy đủ nội thất cao cấp hiện đại.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building3.Id, RoomNumber = "402", FloorNumber = 4, Area = 32.0m, Capacity = 3, Description = "Phòng rộng rãi đầy đủ tiện nghi, vị trí ngay tầng trệt sát khu ăn uống sầm uất.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building3.Id, RoomNumber = "403", FloorNumber = 4, Area = 30.0m, Capacity = 2, Description = "Phòng yên tĩnh phù hợp học tập chuyên sâu, trang bị điều hòa và hệ thống WiFi riêng.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 4 (Homestay Cầu Giấy) ──
            var roomsB4 = new List<Room>
            {
                new() { BuildingId = building4.Id, RoomNumber = "501", FloorNumber = 5, Area = 35.0m, Capacity = 4, Description = "Giường tầng Homestay cao cấp cho nam, tủ đồ riêng biệt khóa vân tay an toàn.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building4.Id, RoomNumber = "502", FloorNumber = 5, Area = 35.0m, Capacity = 4, Description = "Giường tầng Homestay cao cấp cho nữ, thoáng mát sạch sẽ, giờ giấc tự do.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building4.Id, RoomNumber = "503", FloorNumber = 5, Area = 20.0m, Capacity = 1, Description = "Phòng đơn khép kín cho cá nhân thích không gian yên tĩnh tuyệt đối.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 5 (Chùa Bộc) ──
            var roomsB5 = new List<Room>
            {
                new() { BuildingId = building5.Id, RoomNumber = "101", FloorNumber = 1, Area = 18.0m, Capacity = 2, Description = "Phòng trọ giá rẻ tầng trệt, đi lại tiện lợi, đầy đủ đồng hồ điện nước riêng.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building5.Id, RoomNumber = "102", FloorNumber = 1, Area = 20.0m, Capacity = 2, Description = "Phòng khép kín sạch sẽ có gác xép để đồ, khu bếp nấu ăn nhỏ gọn.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building5.Id, RoomNumber = "201", FloorNumber = 2, Area = 22.0m, Capacity = 3, Description = "Phòng tầng 2 nhiều cửa sổ thoáng mát hướng ngõ lớn Chùa Bộc.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 6 (Tô Hiến Thành) ──
            var roomsB6 = new List<Room>
            {
                new() { BuildingId = building6.Id, RoomNumber = "201", FloorNumber = 2, Area = 25.0m, Capacity = 2, Description = "Căn hộ dịch vụ nội thất tiện nghi cao cấp, nằm tại trung tâm Quận 10.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building6.Id, RoomNumber = "202", FloorNumber = 2, Area = 30.0m, Capacity = 3, Description = "Phòng lớn có bàn làm việc, tủ quần áo 3 buồng và máy giặt riêng ngoài logia.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building6.Id, RoomNumber = "301", FloorNumber = 3, Area = 28.0m, Capacity = 2, Description = "Căn hộ ban công đón ánh sáng mát mẻ, khu vực bếp nấu âm sang trọng.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 7 (Quang Trung) ──
            var roomsB7 = new List<Room>
            {
                new() { BuildingId = building7.Id, RoomNumber = "Phòng 1", FloorNumber = 1, Area = 15.0m, Capacity = 2, Description = "Phòng trọ tự quản giá bình dân, toilet khép kín, gần chợ và trạm xe buýt.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building7.Id, RoomNumber = "Phòng 2", FloorNumber = 1, Area = 18.0m, Capacity = 2, Description = "Phòng sạch sẽ lát gạch men sạch, có ban công phơi đồ nhỏ phía sau.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building7.Id, RoomNumber = "Phòng 3", FloorNumber = 2, Area = 20.0m, Capacity = 2, Description = "Phòng tầng 2 thiết kế thoáng có gác lửng cao, không bị đụng đầu.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            // ── Seed Rooms for Building 8 (Tây Hồ) ──
            var roomsB8 = new List<Room>
            {
                new() { BuildingId = building8.Id, RoomNumber = "601", FloorNumber = 6, Area = 45.0m, Capacity = 2, Description = "Căn hộ dịch vụ cao cấp view Hồ Tây lộng gió, trang bị đầy đủ bồn tắm nằm nằm thư giãn.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building8.Id, RoomNumber = "602", FloorNumber = 6, Area = 50.0m, Capacity = 2, Description = "Căn hộ Penthouse áp mái kính tràn viền view Hồ Tây cực kỳ lãng mạn.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            var allRooms = new List<Room>();
            allRooms.AddRange(roomsB1);
            allRooms.AddRange(roomsB2);
            allRooms.AddRange(roomsB3);
            allRooms.AddRange(roomsB4);
            allRooms.AddRange(roomsB5);
            allRooms.AddRange(roomsB6);
            allRooms.AddRange(roomsB7);
            allRooms.AddRange(roomsB8);

            var wif = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-wifi");
            var ac = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-snow2");
            var wc = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-hospital");
            var washing = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-washing-machine");
            var bed = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-house-heart");
            var parking = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-car-front");
            var elevator = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-arrow-up-square");

            foreach (var r in allRooms)
            {
                if (!await db.Rooms.AnyAsync(room => room.BuildingId == r.BuildingId && room.RoomNumber == r.RoomNumber))
                {
                    db.Rooms.Add(r);
                    await db.SaveChangesAsync();

                    // Gán tiện ích động dựa trên loại phòng
                    if (wif != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wif.Id });
                    if (ac != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = ac.Id });
                    if (wc != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wc.Id });
                    if (parking != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = parking.Id });
                    
                    if (r.Area >= 25 && bed != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = bed.Id });
                    if (r.FloorNumber >= 3 && elevator != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = elevator.Id });
                    if (r.RoomNumber.Contains("202") && washing != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = washing.Id });

                    // Tính giá thuê
                    decimal rentPrice = 3000000m;
                    if (r.BuildingId == building1.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "101" => 3200000m, "102" => 3000000m, "103" => 2800000m,
                            "201" => 3600000m, "202" => 3800000m, "203" => 3400000m, _ => 3000000m
                        };
                    }
                    else if (r.BuildingId == building2.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "301" => 4500000m, "302" => 5200000m, "303" => 4200000m, _ => 4000000m
                        };
                    }
                    else if (r.BuildingId == building3.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "401" => 3000000m, "402" => 3400000m, "403" => 3200000m, _ => 3000000m
                        };
                    }
                    else if (r.BuildingId == building4.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "501" => 1500000m, "502" => 1500000m, "503" => 2500000m, _ => 1500000m
                        };
                    }
                    else if (r.BuildingId == building5.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "101" => 2200000m, "102" => 2500000m, "201" => 2800000m, _ => 2200000m
                        };
                    }
                    else if (r.BuildingId == building6.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "201" => 4200000m, "202" => 4800000m, "301" => 4500000m, _ => 4000000m
                        };
                    }
                    else if (r.BuildingId == building7.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "Phòng 1" => 1800000m, "Phòng 2" => 2000000m, "Phòng 3" => 2200000m, _ => 1800000m
                        };
                    }
                    else if (r.BuildingId == building8.Id)
                    {
                        rentPrice = r.RoomNumber switch
                        {
                            "601" => 7000000m, "602" => 8000000m, _ => 7000000m
                        };
                    }

                    db.FeeConfigs.Add(new FeeConfig
                    {
                        RoomId = r.Id,
                        Name = $"Tiền thuê Phòng {r.RoomNumber}",
                        FeeCategory = FeeCategory.Rent,
                        CalcType = CalcType.Fixed,
                        UnitPrice = rentPrice,
                        Unit = "tháng",
                        IsActive = true,
                        SortOrder = 1
                    });

                    // Ảnh ngẫu nhiên từ Unsplash
                    string mainImg = r.RoomNumber switch
                    {
                        "101" => "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?auto=format&fit=crop&w=1200&q=80",
                        "102" => "https://images.unsplash.com/photo-1505691938895-1758d7feb511?auto=format&fit=crop&w=1200&q=80",
                        "103" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=1200&q=80",
                        "301" => "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?auto=format&fit=crop&w=1200&q=80",
                        "302" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80",
                        "401" => "https://images.unsplash.com/photo-1580587771525-78b9dba3b914?auto=format&fit=crop&w=1200&q=80",
                        "402" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80",
                        "403" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=1200&q=80",
                        "501" => "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=1200&q=80", // bunkbed
                        "502" => "https://images.unsplash.com/photo-1505691938895-1758d7feb511?auto=format&fit=crop&w=1200&q=80",
                        "601" => "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=1200&q=80", // premium view
                        "602" => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
                        _ => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80"
                    };

                    db.RoomImages.Add(new RoomImage
                    {
                        RoomId = r.Id,
                        ImageUrl = mainImg,
                        CloudinaryPublicId = $"mock_img_{r.BuildingId}_{r.RoomNumber}_1",
                        IsPrimary = true,
                        SortOrder = 1
                    });
                }
            }
            await db.SaveChangesAsync();

            // ── 7. Seed Contracts & Reviews ─────────────────────────────
            var tenantUser = await userManager.FindByEmailAsync(tenantEmail);
            if (tenantUser != null)
            {
                var room101 = await db.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == "101" && r.BuildingId == building1.Id);
                if (room101 != null && !await db.Contracts.AnyAsync(c => c.RoomId == room101.Id && c.TenantId == tenantUser.Id))
                {
                    var contract = new Contract
                    {
                        RoomId = room101.Id,
                        TenantId = tenantUser.Id,
                        StartDate = DateTime.Now.AddMonths(-3),
                        EndDate = DateTime.Now.AddMonths(9),
                        Status = ContractStatus.Active,
                        DepositAmount = 3200000m,
                        MonthlyRent = 3200000m,
                        CreatedAt = DateTime.Now.AddMonths(-3)
                    };
                    db.Contracts.Add(contract);
                    await db.SaveChangesAsync();

                    if (!await db.RoomReviews.AnyAsync(rv => rv.ContractId == contract.Id))
                    {
                        var review = new RoomReview
                        {
                            RoomId = room101.Id,
                            ReviewerId = tenantUser.Id,
                            ContractId = contract.Id,
                            Rating = 5,
                            Content = "Phòng sạch sẽ, rất đẹp, bác chủ nhà cực kỳ hỗ trợ nhiệt tình và chu đáo. Giá dịch vụ, điện nước hợp lý, khu vực an ninh cao, gần nhiều trạm xe buýt thuận tiện di chuyển đến trường.",
                            IsApproved = true,
                            CreatedAt = DateTime.Now.AddMonths(-1)
                        };
                        db.RoomReviews.Add(review);
                        await db.SaveChangesAsync();
                    }
                }

                var room202 = await db.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == "202" && r.BuildingId == building1.Id);
                if (room202 != null && !await db.Contracts.AnyAsync(c => c.RoomId == room202.Id && c.TenantId == tenantUser.Id))
                {
                    var contract = new Contract
                    {
                        RoomId = room202.Id,
                        TenantId = tenantUser.Id,
                        StartDate = DateTime.Now.AddMonths(-6),
                        EndDate = DateTime.Now.AddMonths(6),
                        Status = ContractStatus.Active,
                        DepositAmount = 3800000m,
                        MonthlyRent = 3800000m,
                        CreatedAt = DateTime.Now.AddMonths(-6)
                    };
                    db.Contracts.Add(contract);
                    await db.SaveChangesAsync();

                    if (!await db.RoomReviews.AnyAsync(rv => rv.ContractId == contract.Id))
                    {
                        var review = new RoomReview
                        {
                            RoomId = room202.Id,
                            ReviewerId = tenantUser.Id,
                            ContractId = contract.Id,
                            Rating = 4,
                            Content = "Không gian phòng rộng rãi thoải mái, ban công đón gió mát mẻ và nhiều ánh sáng. Giá điện nước hơi cao so với mặt bằng chung một chút nhưng dịch vụ vệ sinh và an ninh ở đây cực tốt.",
                            IsApproved = true,
                            CreatedAt = DateTime.Now.AddMonths(-2)
                        };
                        db.RoomReviews.Add(review);
                        await db.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
