# 🎬 CinemaHub - Hệ Thống Quản Lý Đặt Vé Xem Phim

Chào mừng bạn đến với **CinemaHub** – Hệ thống quản lý và đặt vé xem phim trực tuyến hiện đại, được phát triển trên nền tảng **ASP.NET Core MVC 8.0** và **SQL Server**. Hệ thống cung cấp trải nghiệm toàn diện cho cả khách hàng lẫn quản trị viên rạp chiếu phim.

---

## ✨ Tính Năng Nổi Bật

### 👥 Dành Cho Khách Hàng
- **Trang Chủ Hiện Đại**: Hiển thị phim đang chiếu, phim sắp chiếu với banner và trailer bắt mắt
- **Tìm Kiếm & Lọc Phim**: Tìm kiếm theo tên, thể loại, quốc gia
- **Chi Tiết Phim**: Xem thông tin phim, trailer, đạo diễn, diễn viên
- **Xem Lịch Chiếu**: Tra cứu lịch chiếu theo ngày và giờ
- **Đặt Vé & Chọn Ghế**: Giao diện chọn ghế trực quan (Ghế Thường, VIP)
- **Thanh Toán VNPAY**: Tích hợp cổng thanh toán trực tuyến VNPAY (Sandbox)
- **Chatbot AI**: Trợ lý ảo tích hợp Gemini API hỗ trợ tra cứu thông tin và đặt vé
- **Quản Lý Tài Khoản**: Đăng ký, đăng nhập, xem lịch sử đặt vé

### 🛡️ Dành Cho Quản Trị Viên (Admin Area)
- **Dashboard**: Thống kê doanh thu, số lượng vé, biểu đồ báo cáo
- **Quản Lý Phim**: CRUD phim, cập nhật trạng thái (Đang chiếu / Sắp chiếu / Ngừng chiếu)
- **Quản Lý Lịch Chiếu**: Phân bổ suất chiếu, chọn phòng, thiết lập giá vé
- **Quản Lý Thành Viên**: Quản lý tài khoản, phân quyền (Admin / Staff / Customer)
- **Quản Lý Doanh Thu**: Xem chi tiết doanh thu theo ngày, tháng, phim

### 🔌 API Endpoints (Swagger)
- **RESTful APIs**: Movies, Showtimes, Tickets, Users
- **Swagger UI**: Tài liệu API trực quan tại `/swagger`

---

## 🛠️ Công Nghệ Sử Dụng

| Thành phần | Công nghệ |
|------------|-----------|
| Backend | ASP.NET Core MVC 8.0, C# |
| Database | SQL Server, Entity Framework Core |
| Authentication | Cookie-based (JWT Bearer) |
| Payment | VNPAY Payment Gateway |
| AI Chatbot | Google Gemini API |
| Frontend | HTML5, CSS3, JavaScript (ES6), Bootstrap 5, Razor Views |
| API Docs | Swagger / OpenAPI 3.0 |

---

## ⚙️ Hướng Dẫn Cài Đặt

### Yêu Cầu Hệ Thống
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Khuyên dùng)

### Các Bước Thực Hiện

#### 1. Clone dự án
```bash
git clone https://github.com/ducmanh103/Cinema_Management.git
cd Cinema_Management
```

#### 2. Cấu hình Database
Mở file `appsettings.json` và cập nhật chuỗi kết nối SQL Server:
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=TEN_SERVER;Database=CinemaManagement;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Hoặc sử dụng file SQL tại `DB/CinemaManagement.sql` để import database.

#### 3. Cấu hình VNPAY (tùy chọn)
```json
"VnPay": {
    "TmnCode": "FI1FU3SC",
    "HashSecret": "IXINUVQPXBC1H3SK9LXFPXY2QQR9DHZU",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"
}
```

#### 4. Cấu hình Gemini API (cho Chatbot)
```json
"Gemini": {
    "ApiKey": "YOUR_API_KEY"
}
```

#### 5. Chạy ứng dụng
```bash
dotnet restore
dotnet run
```
Ứng dụng sẽ khởi động tại: `https://localhost:7079`

---

## 💳 Hướng Dẫn Test Thanh Toán VNPAY
1. Chọn phương thức thanh toán **VNPAY**
2. Sử dụng thông tin thẻ test:
   - **Ngân hàng**: NCB
   - **Số thẻ**: `9704198526191432198`
   - **Tên chủ thẻ**: `NGUYEN VAN A`
   - **Ngày phát hành**: `07/15`
   - **Mã OTP**: `123456`

---

## 📂 Cấu Trúc Dự Án

```
Cinema_Management/
├── Areas/Admin/               # Admin area (Dashboard, Movies, Showtimes, Users, Revenue)
├── Controllers/               # Customer-facing controllers
│   ├── ChatBotController.cs   # AI Chatbot với Gemini API
│   ├── BookingController.cs
│   ├── MoviesController.cs
│   ├── ShowtimesController.cs
│   └── PaymentController.cs
├── Models/                    # Entities, ViewModels, DTOs
│   └── StatusConstants.cs     # Các hằng số trạng thái
├── Services/                  # Business logic layer
│   ├── MovieService.cs
│   ├── ShowtimeService.cs
│   ├── TicketService.cs
│   ├── VnPayService.cs
│   └── PendingBookingCleanupService.cs
├── Data/                      # DbContext
├── Helpers/                   # Helper classes
├── Views/                     # Customer-facing views
├── wwwroot/                   # Static files (CSS, JS, images)
└── DB/                        # SQL backup files
```

---

## 🔐 Tài Khoản Mặc Định

### Tài Khoản Admin
| Trường | Giá trị |
|--------|---------|
| **Tên đăng nhập** | `admin` |
| **Mật khẩu** | `Admin@123` |
| **URL Đăng nhập** | `/Admin/Account/Login` |

---

## 🛠️ Truy Cập Tài Liệu API

Swagger UI: `https://localhost:7079/swagger`

---

## 📧 Thông Tin Liên Hệ

- **Hotline**: 0344 596 643
- **Email**: ducmanh2005vt@gmail.com
- **Địa chỉ**: 235 Hoàng Quốc Việt, phường Nghĩa Đô, quận Bắc Từ Liêm, Hà Nội

---

## 📄 License

Dự án này được phát triển cho mục đích học tập và demo.
