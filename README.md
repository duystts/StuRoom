# Hướng dẫn Tài khoản Đăng nhập (Credentials) - StuRoom

Dưới đây là danh sách các tài khoản thử nghiệm được cấu hình sẵn trong hệ thống (được tạo tự động thông qua `DbInitializer` khi khởi chạy ứng dụng lần đầu).

---

## 🔑 Danh sách tài khoản

| Vai trò | Email | Mật khẩu | Mô tả quyền hạn |
| :--- | :--- | :--- | :--- |
| **Admin (Quản trị viên)** | `admin@sturoom.com` | `Admin@123456` | Toàn quyền hệ thống, duyệt chủ trọ, duyệt tiện ích, kiểm duyệt đánh giá, quản lý báo cáo vi phạm. |
| **Chủ trọ (Landlord)** | `landlord@sturoom.com` | `Tenant@123456` | Quản lý toà nhà, đăng tin phòng trọ, quản lý hợp đồng thuê, quản lý yêu cầu đặt phòng / xem phòng. |
| **Người thuê (Tenant)** | `tenant@sturoom.com` | `Tenant@123456` | Tìm phòng, lưu phòng yêu thích, gửi yêu cầu xem phòng, gửi yêu cầu đặt phòng, viết đánh giá. |

---

## 🛠️ Lưu ý quan trọng khi chạy dự án
1. **Cơ sở dữ liệu tự động**: Hệ thống đã được tích hợp tính năng tự động chạy `MigrateAsync()` khi khởi động. Do đó, khi bạn chạy dự án lần đầu, cơ sở dữ liệu sẽ tự động được dựng và seed sẵn các tài khoản trên cùng với dữ liệu phòng trọ demo tại TP.HCM và Hà Nội.
2. **Trường hợp chạy lỗi kết nối**: Hãy đảm bảo tiến trình cũ đã được tắt hẳn để tránh bị khoá file `.exe` trong thư mục `bin\Debug\net10.0\`.
