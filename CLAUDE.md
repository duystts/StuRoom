# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tài liệu thiết kế

> **Quan trọng**: Toàn bộ thiết kế database, business rules và các quyết định kiến trúc được lưu ở [`DESIGN.html`](DESIGN.html) (mở bằng trình duyệt).
> Mọi thay đổi schema phải cập nhật `DESIGN.html` trước, sau đó mới tạo migration.

## Project Overview

StuRoom là ứng dụng web quản lý phòng trọ dành cho sinh viên — kết hợp marketplace (tìm/thuê phòng) và công cụ quản lý cho chủ trọ (hợp đồng, hoá đơn, phí linh hoạt). Stack: ASP.NET Core MVC (.NET 10), EF Core, SQL Server, Cloudinary.

## Commands

Tất cả lệnh chạy từ thư mục `StuRoom/StuRoom/` (nơi có `.csproj`):

```bash
dotnet run                                 # Chạy ứng dụng
dotnet build                               # Build
dotnet watch run                           # Chạy với hot-reload
dotnet ef migrations add <TênMigration>    # Tạo migration mới
dotnet ef database update                  # Áp dụng migration vào DB
dotnet ef migrations remove                # Xóa migration cuối

# Nếu dotnet-ef chưa cài:
dotnet tool install --global dotnet-ef
```

## Architecture

Ứng dụng dùng kết hợp **MVC** (cho logic chính) và **Razor Pages** (cho Identity):

- **Controllers + Views** — luồng MVC chính, hiện có `HomeController`
- **Areas/Identity** — Razor Pages của ASP.NET Identity (đăng ký, đăng nhập, v.v.)
- **Data/ApplicationDbContext** — extends `IdentityDbContext`; thêm `DbSet<T>` vào đây khi tạo entity mới
- **Models** — view models và domain models

## Database

- Dev: SQL Server LocalDB, connection string trong `appsettings.json`
- Secrets nhạy cảm (production connection string, v.v.) lưu qua **User Secrets** (`UserSecretsId` đã được cấu hình trong `.csproj`)

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<chuỗi kết nối>"
```

## Identity

`SignIn.RequireConfirmedAccount = true` — tài khoản cần xác nhận email trước khi đăng nhập. Khi phát triển cần cấu hình email sender hoặc tắt tùy chọn này tạm thời.

Để scaffold thêm trang Identity (ví dụ trang đăng ký tùy chỉnh):
```bash
dotnet aspnet-codegenerator identity -dc StuRoom.Data.ApplicationDbContext --files "Account.Register"
```
