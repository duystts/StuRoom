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
                FullName    = "System Admin",
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
                new() { Name = "Bình nước nóng", IconClass = "bi-droplet" },
                new() { Name = "Máy giặt", IconClass = "bi-washing-machine" },
                new() { Name = "Bãi đỗ xe", IconClass = "bi-car-front" },
                new() { Name = "Nhà vệ sinh riêng", IconClass = "bi-hospital" },
                new() { Name = "Chìa khoá thẻ", IconClass = "bi-door-open" },
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
                FullName       = "Nguyễn Văn Trọ (Chủ trọ mẫu)",
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
                FullName       = "Trần Văn Sinh (Sinh viên mẫu)",
                EmailConfirmed = true,
                IsApproved     = true
            };
            var result = await userManager.CreateAsync(tenant, "Tenant@123456");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(tenant, "Tenant");
        }

        // ── 6. Seed Demo Building, Rooms, Fees, and Images ──────────
        var landlordUser = await userManager.FindByEmailAsync(landlordEmail);
        if (landlordUser != null)
        {
            // Seed "StuRoom House Quận 9" if not exists
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
                    Description = "Tòa nhà căn hộ dịch vụ mini dành riêng cho sinh viên học tập tại Khu Công Nghệ Cao và các trường đại học lân cận. Phòng ốc khang trang, giờ giấc tự do.",
                    Latitude    = 10.8444,
                    Longitude   = 106.7778,
                    CreatedAt   = DateTime.Now
                };
                db.Buildings.Add(building1);
                await db.SaveChangesAsync();
            }

            // Seed "StuRoom Premier Bình Thạnh" if not exists
            var building2 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom Premier Bình Thạnh" && b.LandlordId == landlordUser.Id);
            if (building2 == null)
            {
                building2 = new Building
                {
                    LandlordId  = landlordUser.Id,
                    Name        = "StuRoom Premier Bình Thạnh",
                    Address     = "Số 45/12, Đường Điện Biên Phủ, Phường 25",
                    Province    = "Thành phố Hồ Chí Minh",
                    District    = "Bình Thạnh",
                    Ward        = "Phường 25",
                    Description = "Căn hộ dịch vụ cao cấp gần ngã tư Hàng Xanh, thuận tiện di chuyển đến các trường Đại học HUTECH, UEF, Ngoại Thương. Phòng đầy đủ tiện nghi, ban công thoáng mát.",
                    Latitude    = 10.8018,
                    Longitude   = 106.7119,
                    CreatedAt   = DateTime.Now
                };
                db.Buildings.Add(building2);
                await db.SaveChangesAsync();
            }

            // Seed building-level fee configs for Building 1 if not exists
            if (!await db.FeeConfigs.AnyAsync(f => f.BuildingId == building1.Id && f.RoomId == null))
            {
                db.FeeConfigs.AddRange(new List<FeeConfig>
                {
                    new() { BuildingId = building1.Id, Name = "Tiền phòng", FeeCategory = FeeCategory.Rent, CalcType = CalcType.Fixed, UnitPrice = 3500000m, Unit = "tháng", IsActive = true, SortOrder = 1 },
                    new() { BuildingId = building1.Id, Name = "Tiền điện", FeeCategory = FeeCategory.Electricity, CalcType = CalcType.PerUnit, UnitPrice = 3500m, Unit = "kWh", IsActive = true, SortOrder = 2 },
                    new() { BuildingId = building1.Id, Name = "Tiền nước", FeeCategory = FeeCategory.Water, CalcType = CalcType.PerUnit, UnitPrice = 18000m, Unit = "m³", IsActive = true, SortOrder = 3 },
                    new() { BuildingId = building1.Id, Name = "Phí internet", FeeCategory = FeeCategory.Internet, CalcType = CalcType.Fixed, UnitPrice = 100000m, Unit = "tháng", IsActive = true, SortOrder = 4 }
                });
                await db.SaveChangesAsync();
            }

            // Seed building-level fee configs for Building 2 if not exists
            if (!await db.FeeConfigs.AnyAsync(f => f.BuildingId == building2.Id && f.RoomId == null))
            {
                db.FeeConfigs.AddRange(new List<FeeConfig>
                {
                    new() { BuildingId = building2.Id, Name = "Tiền điện", FeeCategory = FeeCategory.Electricity, CalcType = CalcType.PerUnit, UnitPrice = 4000m, Unit = "kWh", IsActive = true, SortOrder = 2 },
                    new() { BuildingId = building2.Id, Name = "Tiền nước", FeeCategory = FeeCategory.Water, CalcType = CalcType.PerUnit, UnitPrice = 20000m, Unit = "m³", IsActive = true, SortOrder = 3 },
                    new() { BuildingId = building2.Id, Name = "Phí internet", FeeCategory = FeeCategory.Internet, CalcType = CalcType.Fixed, UnitPrice = 120000m, Unit = "tháng", IsActive = true, SortOrder = 4 }
                });
                await db.SaveChangesAsync();
            }

            // Rooms for Building 1
            var roomsB1 = new List<Room>
            {
                new() { BuildingId = building1.Id, RoomNumber = "101", FloorNumber = 1, Area = 25.5m, Capacity = 2, Description = "Phòng trọ gác lửng đầy đủ ánh sáng tự nhiên, có khu bếp nấu ăn riêng biệt.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "102", FloorNumber = 1, Area = 22.0m, Capacity = 2, Description = "Phòng trọ ấm cúng sạch sẽ, thích hợp ở 1-2 người.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "103", FloorNumber = 1, Area = 20.0m, Capacity = 2, Description = "Phòng trọ nhỏ gọn tiện lợi, đầy đủ nội thất cơ bản.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "201", FloorNumber = 2, Area = 28.0m, Capacity = 3, Description = "Phòng tầng cao mát mẻ, ban công rộng rãi thoáng gió.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "202", FloorNumber = 2, Area = 30.0m, Capacity = 3, Description = "Phòng rộng có ban công rộng rãi, thoáng mát, phù hợp nhóm 2-3 bạn ở ghép.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building1.Id, RoomNumber = "203", FloorNumber = 2, Area = 26.0m, Capacity = 2, Description = "Phòng thiết kế hiện đại, đầy đủ tiện nghi.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            foreach (var r in roomsB1)
            {
                if (!await db.Rooms.AnyAsync(room => room.BuildingId == building1.Id && room.RoomNumber == r.RoomNumber))
                {
                    db.Rooms.Add(r);
                    await db.SaveChangesAsync();

                    var wif = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-wifi");
                    var ac = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-snow2");
                    var wc = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-hospital");
                    var washing = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-washing-machine");

                    if (wif != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wif.Id });
                    if (ac != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = ac.Id });
                    if (wc != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wc.Id });
                    if (r.RoomNumber == "202" && washing != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = washing.Id });

                    decimal rentPrice = r.RoomNumber switch
                    {
                        "101" => 3200000m,
                        "102" => 3000000m,
                        "103" => 2800000m,
                        "201" => 3600000m,
                        "202" => 3800000m,
                        "203" => 3400000m,
                        _ => 3000000m
                    };

                    db.FeeConfigs.Add(new FeeConfig
                    {
                        RoomId = r.Id,
                        Name = $"Tiền phòng {r.RoomNumber}",
                        FeeCategory = FeeCategory.Rent,
                        CalcType = CalcType.Fixed,
                        UnitPrice = rentPrice,
                        Unit = "tháng",
                        IsActive = true,
                        SortOrder = 1
                    });

                    string mainImg = r.RoomNumber switch
                    {
                        "101" => "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?auto=format&fit=crop&w=1200&q=80",
                        "102" => "https://images.unsplash.com/photo-1505691938895-1758d7feb511?auto=format&fit=crop&w=1200&q=80",
                        "103" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=1200&q=80",
                        _ => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80"
                    };

                    db.RoomImages.Add(new RoomImage
                    {
                        RoomId = r.Id,
                        ImageUrl = mainImg,
                        CloudinaryPublicId = $"mock_img_{building1.Id}_{r.RoomNumber}_1",
                        IsPrimary = true,
                        SortOrder = 1
                    });
                }
            }

            // Rooms for Building 2
            var roomsB2 = new List<Room>
            {
                new() { BuildingId = building2.Id, RoomNumber = "301", FloorNumber = 3, Area = 35.0m, Capacity = 3, Description = "Phòng Premier sang trọng, ban công hướng Landmark 81 cực đẹp.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building2.Id, RoomNumber = "302", FloorNumber = 3, Area = 40.0m, Capacity = 4, Description = "Phòng rộng rãi phù hợp cho nhóm sinh viên ở ghép tiết kiệm chi phí.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building2.Id, RoomNumber = "303", FloorNumber = 3, Area = 32.0m, Capacity = 2, Description = "Phòng yên tĩnh, thích hợp học tập và nghiên cứu.", Status = RoomStatus.Maintenance, CreatedAt = DateTime.Now }
            };

            foreach (var r in roomsB2)
            {
                if (!await db.Rooms.AnyAsync(room => room.BuildingId == building2.Id && room.RoomNumber == r.RoomNumber))
                {
                    db.Rooms.Add(r);
                    await db.SaveChangesAsync();

                    var wif = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-wifi");
                    var ac = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-snow2");
                    var wc = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-hospital");
                    var bed = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-house-heart");

                    if (wif != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wif.Id });
                    if (ac != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = ac.Id });
                    if (wc != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wc.Id });
                    if (bed != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = bed.Id });

                    decimal rentPrice = r.RoomNumber switch
                    {
                        "301" => 4500000m,
                        "302" => 5200000m,
                        "303" => 4200000m,
                        _ => 4000000m
                    };

                    db.FeeConfigs.Add(new FeeConfig
                    {
                        RoomId = r.Id,
                        Name = $"Tiền phòng {r.RoomNumber}",
                        FeeCategory = FeeCategory.Rent,
                        CalcType = CalcType.Fixed,
                        UnitPrice = rentPrice,
                        Unit = "tháng",
                        IsActive = true,
                        SortOrder = 1
                    });

                    string mainImg = r.RoomNumber switch
                    {
                        "301" => "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?auto=format&fit=crop&w=1200&q=80",
                        "302" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80",
                        _ => "https://images.unsplash.com/photo-1505691938895-1758d7feb511?auto=format&fit=crop&w=1200&q=80"
                    };

                    db.RoomImages.Add(new RoomImage
                    {
                        RoomId = r.Id,
                        ImageUrl = mainImg,
                        CloudinaryPublicId = $"mock_img_{building2.Id}_{r.RoomNumber}_1",
                        IsPrimary = true,
                        SortOrder = 1
                    });
                }
            }

            // Seed "StuRoom Garden Thủ Đức" if not exists
            var building3 = await db.Buildings.FirstOrDefaultAsync(b => b.Name == "StuRoom Garden Thủ Đức" && b.LandlordId == landlordUser.Id);
            if (building3 == null)
            {
                building3 = new Building
                {
                    LandlordId = landlordUser.Id,
                    Name = "StuRoom Garden Thủ Đức",
                    Address = "Số 88, Đường Lê Hồng Phong, Phường 12, Quận Thủ Đức",
                    Province = "Thành phố Hồ Chí Minh",
                    District = "Thủ Đức",
                    Ward = "Phường 12",
                    Description = "Khu căn hộ dịch vụ xanh, gần công viên, môi trường yên tĩnh, thích hợp cho sinh viên và nhân viên văn phòng.",
                    CreatedAt = DateTime.Now
                };
                db.Buildings.Add(building3);
                await db.SaveChangesAsync();
            }

            // Building-level fee configs for Building 3
            if (!await db.FeeConfigs.AnyAsync(f => f.BuildingId == building3.Id && f.RoomId == null))
            {
                db.FeeConfigs.AddRange(new List<FeeConfig>
                {
                    new() { BuildingId = building3.Id, Name = "Tiền phòng", FeeCategory = FeeCategory.Rent, CalcType = CalcType.Fixed, UnitPrice = 3000000m, Unit = "tháng", IsActive = true, SortOrder = 1 },
                    new() { BuildingId = building3.Id, Name = "Tiền điện", FeeCategory = FeeCategory.Electricity, CalcType = CalcType.PerUnit, UnitPrice = 3500m, Unit = "kWh", IsActive = true, SortOrder = 2 },
                    new() { BuildingId = building3.Id, Name = "Tiền nước", FeeCategory = FeeCategory.Water, CalcType = CalcType.PerUnit, UnitPrice = 18000m, Unit = "m³", IsActive = true, SortOrder = 3 },
                    new() { BuildingId = building3.Id, Name = "Phí internet", FeeCategory = FeeCategory.Internet, CalcType = CalcType.Fixed, UnitPrice = 100000m, Unit = "tháng", IsActive = true, SortOrder = 4 }
                });
                await db.SaveChangesAsync();
            }

            // Rooms for Building 3
            var roomsB3 = new List<Room>
            {
                new() { BuildingId = building3.Id, RoomNumber = "401", FloorNumber = 4, Area = 28.0m, Capacity = 2, Description = "Phòng có ban công xanh, view công viên, nội thất hiện đại.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building3.Id, RoomNumber = "402", FloorNumber = 4, Area = 32.0m, Capacity = 3, Description = "Phòng rộng, trang bị đầy đủ tiện nghi, gần khu vực ăn uống.", Status = RoomStatus.Available, CreatedAt = DateTime.Now },
                new() { BuildingId = building3.Id, RoomNumber = "403", FloorNumber = 4, Area = 30.0m, Capacity = 2, Description = "Phòng yên tĩnh, thích hợp học tập, có máy lạnh và wifi.", Status = RoomStatus.Available, CreatedAt = DateTime.Now }
            };

            foreach (var r in roomsB3)
            {
                if (!await db.Rooms.AnyAsync(room => room.BuildingId == building3.Id && room.RoomNumber == r.RoomNumber))
                {
                    db.Rooms.Add(r);
                    await db.SaveChangesAsync();

                    var wif = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-wifi");
                    var ac = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-snow2");
                    var wc = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-hospital");
                    var bed = await db.Amenities.FirstOrDefaultAsync(a => a.IconClass == "bi-house-heart");

                    if (wif != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wif.Id });
                    if (ac != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = ac.Id });
                    if (wc != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = wc.Id });
                    if (bed != null) db.RoomAmenities.Add(new RoomAmenity { RoomId = r.Id, AmenityId = bed.Id });

                    decimal rentPrice = r.RoomNumber switch
                    {
                        "401" => 3000000m,
                        "402" => 3400000m,
                        "403" => 3200000m,
                        _ => 3000000m
                    };

                    db.FeeConfigs.Add(new FeeConfig
                    {
                        RoomId = r.Id,
                        Name = $"Tiền phòng {r.RoomNumber}",
                        FeeCategory = FeeCategory.Rent,
                        CalcType = CalcType.Fixed,
                        UnitPrice = rentPrice,
                        Unit = "tháng",
                        IsActive = true,
                        SortOrder = 1
                    });

                    string mainImg = r.RoomNumber switch
                    {
                        "401" => "https://images.unsplash.com/photo-1580587771525-78b9dba3b914?auto=format&fit=crop&w=1200&q=80",
                        "402" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80",
                        "403" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=1200&q=80",
                        _ => "https://images.unsplash.com/photo-1505691938895-1758d7feb511?auto=format&fit=crop&w=1200&q=80"
                    };

                    db.RoomImages.Add(new RoomImage
                    {
                        RoomId = r.Id,
                        ImageUrl = mainImg,
                        CloudinaryPublicId = $"mock_img_{building3.Id}_{r.RoomNumber}_1",
                        IsPrimary = true,
                        SortOrder = 1
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
