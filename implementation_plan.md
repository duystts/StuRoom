# Plan to Keep Only Vietnamese and Enrich Database Seed (HCMC & Hanoi)

This plan outlines the steps to:
1. Remove the English translation/language switcher feature and lock the default culture to Vietnamese (`vi`).
2. Translate all seed data back to Vietnamese in `DbInitializer.cs`.
3. Add multiple new buildings and rooms located in Ho Chi Minh City (e.g., District 10, Go Vap) and Hanoi (e.g., Cau Giay, Dong Da, Tay Ho) to provide a rich dataset.
4. Drop and recreate the database to seed the updated Vietnamese records.

## User Review Required

> [!IMPORTANT]
> **Key Configuration Changes:**
> - The language switcher dropdown (`ENG` / `VIE`) will be removed from the Top Navbar.
> - The default culture in `Program.cs` will be locked to Vietnamese (`vi`).
> - The existing database will be dropped and recreated to apply the new Vietnamese-only seed data with multiple new rooms.

---

## Proposed Changes

### [Core App Configuration]

#### [MODIFY] [Program.cs](file:///c:/Users/Lenovo/OneDrive/Documents/CODE/LapTrinhWeb/StuRoom/StuRoom/Program.cs)
- Revert default culture back to `vi` and remove `en` from supported cultures:
  ```csharp
  var supportedCultures = new[] { "vi" };
  var localizationOptions = new RequestLocalizationOptions()
      .SetDefaultCulture("vi")
      .AddSupportedCultures(supportedCultures)
      .AddSupportedUICultures(supportedCultures);
  ```

#### [MODIFY] [_Navbar.cshtml](file:///c:/Users/Lenovo/OneDrive/Documents/CODE/LapTrinhWeb/StuRoom/StuRoom/Views/Shared/_Navbar.cshtml)
- Remove the language selector dropdown list (lines 92-124).

---

### [Database Seed Data Enrichment]

#### [MODIFY] [DbInitializer.cs](file:///c:/Users/Lenovo/OneDrive/Documents/CODE/LapTrinhWeb/StuRoom/StuRoom/Data/DbInitializer.cs)
- Translate all existing records (Amenities, Buildings, Rooms, Reviews, Fee categories) back to Vietnamese.
- Add new Vietnamese buildings and rooms:
  - **Building 4 (Hanoi - Cau Giay):** "StuRoom Homestay Cầu Giấy" (Dịch Vọng Hậu, Cầu Giấy, Hà Nội) - 3 rooms.
  - **Building 5 (Hanoi - Dong Da):** "Nhà trọ Sinh viên Chùa Bộc" (Chùa Bộc, Đống Đa, Hà Nội) - 3 rooms.
  - **Building 6 (HCMC - District 10):** "Căn hộ dịch vụ Tô Hiến Thành" (Tô Hiến Thành, Quận 10, TP.HCM) - 3 rooms.
  - **Building 7 (HCMC - Go Vap):** "Phòng trọ giá rẻ Quang Trung" (Quang Trung, Gò Vấp, TP.HCM) - 3 rooms.
  - **Building 8 (Hanoi - Tay Ho):** "StuRoom Tây Hồ Lakeview" (Xuân Diệu, Tây Hồ, Hà Nội) - 2 rooms.
- Update all associated amenities, fee configurations (Rent, Electricity, Water, Internet, Parking), reviews, and mock images for these new rooms.

---

### [Resource Clean up]

#### [DELETE] [.en.resx Files]
- Delete the unused `.en.resx` files created in the previous task to clean up the project:
  - `Resources/Views/Rooms/Compare.en.resx`
  - `Resources/Views/Shared/_Layout.en.resx`
  - `Resources/Views/Shared/_Navbar.en.resx`
  - `Resources/Views/Shared/_SidebarAdmin.en.resx`
  - `Resources/Views/Shared/_SidebarLandlord.en.resx`
  - `Resources/Views/Rooms/Index.en.resx`
  - `Resources/Views/Rooms/Favorites.en.resx`
  - `Resources/Views/Rooms/Detail.en.resx`
  - `Resources/Views/Home/Index.en.resx`

---

## Verification Plan

### Automated Tests
- Drop the old database, update migrations, and build:
  ```powershell
  dotnet ef database drop --force
  ```
  ```powershell
  dotnet ef database update
  ```
  ```powershell
  dotnet build
  ```

### Manual Verification
1. **Launch browser subagent** to verify:
   - Homepage lists a diverse set of new rooms from Hanoi and HCMC.
   - Search page filters by region/city successfully.
   - The Top Navbar does not display any language selector dropdown.
