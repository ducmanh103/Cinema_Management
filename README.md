# CinemaHub - Hệ Thống Quản Lý Và Đặt Vé Xem Phim Trực Tuyến

**CinemaHub** là hệ thống quản lý rạp chiếu phim và đặt vé trực tuyến được xây dựng trên nền tảng **ASP.NET Core MVC 8.0** và **PostgreSQL 16**. Hệ thống cung cấp đầy đủ phân hệ nghiệp vụ cho khách hàng, nhân viên rạp và quản trị viên, đồng thời tích hợp cổng thanh toán VNPAY, trợ lý ảo Gemini AI, xác thực Google OAuth 2.0 và kiến trúc bảo mật đa tầng.

---

## Tính Năng Hệ Thống

### 1. Phân Hệ Khách Hàng
- **Tra cứu phim và lịch chiếu**: Xem danh sách phim đang chiếu, phim sắp chiếu, thông tin chi tiết phim, trailer và lọc suất chiếu theo rạp, ngày chiếu.
- **Đặt vé và giữ ghế thời gian thực**: Sơ đồ ghế trực quan (Ghế Thường, Ghế VIP), cơ chế khóa ghế tạm thời tránh trùng lặp giao dịch đồng thời (Race Condition) và dịch vụ nền tự động giải phóng ghế quá hạn thanh toán.
- **Thanh toán trực tuyến**: Tích hợp cổng thanh toán VNPAY (kiểm tra chữ ký số HMAC-SHA512) và thanh toán tại quầy đối với nhân viên.
- **Trợ lý ảo AI (Chatbot)**: Tích hợp Google Gemini API hỗ trợ tư vấn phim, tra cứu lịch chiếu, giá vé và hướng dẫn đặt vé trực tiếp.
- **Quản lý tài khoản và hồ sơ**:
  - Đăng ký, đăng nhập nội bộ (mã hóa mật khẩu BCrypt) hoặc đăng nhập một chạm qua **Google OAuth 2.0**.
  - Khôi phục mật khẩu qua email (SMTP) với liên kết băm SHA-256 có thời hạn 15 phút.
  - Quản lý hồ sơ cá nhân, đổi mật khẩu, thống kê chi tiêu và tra cứu lịch sử đặt vé.

### 2. Phân Hệ Quản Trị (Admin Area)
- **Bảng điều khiển (Dashboard)**: Thống kê tổng quan doanh thu, số lượng vé bán ra, tỷ lệ lấp đầy phòng chiếu và biểu đồ trực quan.
- **Quản lý danh mục phim và thể loại**: Thêm mới, chỉnh sửa, cập nhật trạng thái phát hành (Đang chiếu, Sắp chiếu, Ngừng chiếu).
- **Quản lý rạp, phòng chiếu và lịch chiếu**: Thiết lập sơ đồ ghế, phân bổ suất chiếu tự động kiểm tra xung đột thời gian giữa các phòng chiếu.
- **Quản lý người dùng và phân quyền**: Quản lý trạng thái tài khoản, phân quyền truy cập theo vai trò (`Admin`, `Staff`, `Customer`) kết hợp cơ chế `SecurityStamp` thu hồi phiên đăng nhập tức thời.
- **Báo cáo doanh thu**: Thống kê chi tiết doanh thu theo khoảng thời gian, theo phim và theo rạp chiếu.

### 3. Bảo Mật Và Vận Hành
- **Bảo vệ biên mạng và chống DDoS**: Triển khai qua **OneShield DNS Proxy / WAF** kết hợp **Nginx Reverse Proxy** (`ngx_http_realip_module`), khóa truy cập trực tiếp vào IP máy chủ gốc.
- **Giới hạn tần suất (Rate Limiting) 2 tầng**:
  - Tầng Nginx: Giới hạn tốc độ yêu cầu (`limit_req_zone`) và số kết nối đồng thời (`limit_conn_zone`) trên mỗi địa chỉ IP thật.
  - Tầng ứng dụng (.NET 8 RateLimiter): Chính sách giới hạn riêng biệt cho Toàn cục (`Global`), Xác thực (`AuthLimit`), Đặt vé (`BookingLimit`) và Chatbot (`ChatBotLimit`).
- **Chống tấn công dò mật khẩu (Brute-Force Protection)**: Tự động khóa tạm thời tài khoản/IP khi đăng nhập sai quá ngưỡng quy định và cân bằng thời gian xử lý BCrypt chống Timing Attack.
- **Cảnh báo an ninh thời gian thực**: Dịch vụ nền gửi cảnh báo tức thời qua **Telegram Bot** khi phát hiện hành vi vượt ngưỡng Rate Limit, dò mật khẩu hoặc sai lệch chữ ký thanh toán VNPAY.

---

## Công Nghệ Sử Dụng

| Thành phần | Công nghệ / Thư viện |
| :--- | :--- |
| **Backend Framework** | ASP.NET Core MVC 8.0, C# 12 |
| **Cơ sở dữ liệu** | PostgreSQL 16, Entity Framework Core 8.0 (`Npgsql`) |
| **Xác thực & Bảo mật** | Cookie Authentication, Google OAuth 2.0, BCrypt.Net-Next, ASP.NET Core Data Protection |
| **Reverse Proxy & WAF** | Nginx (Alpine), OneShield Proxy, Certbot (Let's Encrypt TLS 1.2/1.3) |
| **Cổng thanh toán** | VNPAY Payment Gateway (HMAC-SHA512) |
| **Trí tuệ nhân tạo** | Google Gemini API (`gemini-2.5-flash`) |
| **Giám sát & Ghi log** | Serilog, ASP.NET Core HealthChecks, Telegram Bot Alert |
| **Frontend** | Razor Views, Bootstrap 5, JavaScript (ES6), CSS3 |
| **Tài liệu API** | Swagger / OpenAPI 3.0 |
| **Hạ tầng triển khai** | Docker, Docker Compose, GitHub Actions CI/CD |

---

## Cấu Trúc Thư Mục

```text
Cinema_Management/
├── Areas/Admin/                  # Phân hệ quản trị (Dashboard, Movies, Showtimes, Users, Revenue)
├── Controllers/                  # Các Controller phục vụ khách hàng và API
│   ├── AccountController.cs      # Xác thực, Google OAuth, Quên mật khẩu, Hồ sơ cá nhân
│   ├── BookingController.cs      # Chọn ghế và xử lý đặt vé
│   ├── ChatBotController.cs      # API tích hợp trợ lý ảo Gemini
│   ├── HomeController.cs         # Trang chủ, rạp chiếu, tin tức, giới thiệu
│   ├── MoviesController.cs       # Danh sách và chi tiết phim
│   ├── PaymentController.cs      # Xử lý thanh toán và callback VNPAY
│   └── ShowtimesController.cs    # Tra cứu lịch chiếu
├── Data/                         # Ngữ cảnh cơ sở dữ liệu (CinemaDbContext) và khởi tạo dữ liệu (DbInitializer)
├── DB/                           # Kịch bản khởi tạo cơ sở dữ liệu PostgreSQL
├── Helpers/                      # Các lớp tiện ích (Chuẩn hóa múi giờ UTC/UTC+7, VnPayLibrary)
├── Models/                       # Các thực thể (Entities), ViewModels và hằng số trạng thái
├── Services/                     # Tầng xử lý nghiệp vụ và dịch vụ chạy nền (Hosted Services)
│   ├── EmailService.cs           # Gửi email giao dịch và khôi phục mật khẩu qua SMTP
│   ├── LoginAttemptService.cs    # Kiểm soát và khóa tạm thời khi đăng nhập sai nhiều lần
│   ├── MovieService.cs           # Nghiệp vụ quản lý phim
│   ├── PendingBookingCleanupService.cs # Dịch vụ nền tự động hủy vé chờ thanh toán quá hạn
│   ├── ShowtimeService.cs        # Nghiệp vụ lịch chiếu và kiểm tra trạng thái ghế
│   ├── TelegramSecurityAlertService.cs # Dịch vụ gửi cảnh báo bảo mật qua Telegram
│   ├── TicketService.cs          # Nghiệp vụ đặt vé và giao dịch cơ sở dữ liệu
│   └── VnPayService.cs           # Tạo URL thanh toán và xác thực chữ ký VNPAY
├── Views/                        # Giao diện người dùng (Razor Views)
├── wwwroot/                      # Tài nguyên tĩnh (CSS, JavaScript, hình ảnh)
├── nginx/                        # Cấu hình Nginx Reverse Proxy, Rate Limit và SSL
├── Dockerfile                    # Cấu hình đóng gói container ứng dụng .NET 8
├── docker-compose.yml            # Cấu hình Docker Compose cho môi trường phát triển
└── docker-compose.prod.yml       # Cấu hình Docker Compose tối ưu cho môi trường Production
```

---

## Hướng Dẫn Cài Đặt Và Triển Khai

### 1. Yêu Cầu Môi Trường
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 16](https://www.postgresql.org/download/) hoặc [Docker Engine](https://docs.docker.com/engine/install/)

### 2. Chạy Ở Môi Trường Phát Triển (Local Development)

**Bước 1: Sao chép mã nguồn**
```bash
git clone https://github.com/ducmanh103/Cinema_Management.git
cd Cinema_Management
```

**Bước 2: Cấu hình kết nối và biến môi trường**
Cập nhật chuỗi kết nối PostgreSQL và các khóa tích hợp trong `appsettings.Development.json` hoặc `User Secrets`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=CinemaManagement;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  },
  "VnPay": {
    "TmnCode": "YOUR_VNPAY_TMNCODE",
    "HashSecret": "YOUR_VNPAY_HASHSECRET",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash"
  }
}
```

**Bước 3: Khởi chạy ứng dụng**
```bash
dotnet restore
dotnet run --project CINEMA_MANAGEMENT.csproj
```
Hệ thống tự động kiểm tra, khởi tạo cấu trúc bảng và dữ liệu mẫu khi khởi động.

### 3. Triển Khai Môi Trường Production Với Docker Compose

**Bước 1: Tạo tệp cấu hình `.env.prod`**
Sao chép từ tệp mẫu `.env.prod.example` và điền đầy đủ các thông số bảo mật:
```bash
cp .env.prod.example .env.prod
```

**Bước 2: Khởi chạy toàn bộ cụm dịch vụ (PostgreSQL, Web App, Nginx, Certbot)**
```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
```

**Bước 3: Kiểm tra trạng thái hoạt động**
- Kiểm tra sức khỏe ứng dụng: `GET /health` và `GET /ready`
- Kiểm tra cấu hình Nginx: `docker exec cinemahub_nginx nginx -t`

---

## Thông Tin Thử Nghiệm Thanh Toán VNPAY (Sandbox)

Để kiểm thử quy trình thanh toán trực tuyến trên môi trường Sandbox của VNPAY, sử dụng thông tin thẻ mẫu sau:
- **Ngân hàng**: NCB
- **Số thẻ**: `9704198526191432198`
- **Tên chủ thẻ**: `NGUYEN VAN A`
- **Ngày phát hành**: `07/15`
- **Mật khẩu OTP**: `123456`

---

## Tài Khoản Quản Trị Mặc Định

| Thông tin | Giá trị |
| :--- | :--- |
| **Đường dẫn đăng nhập quản trị** | `/Admin/Account/Login` |
| **Tên đăng nhập** | `admin` |
| **Mật khẩu khởi tạo** | Thiết lập qua biến môi trường `ADMIN_INITIAL_PASSWORD` trong `.env.prod` |

---

## Thông Tin Liên Hệ

- **Đơn vị vận hành**: CinemaHub
- **Địa chỉ**: 235 Hoàng Quốc Việt, Phường Nghĩa Đô, Quận Bắc Từ Liêm, Hà Nội
- **Điện thoại**: 0344 596 643
- **Email**: ducmanh2005vt@gmail.com

---

## Giấy Phép

Dự án được phát triển phục vụ mục đích nghiên cứu, học tập và triển khai thực nghiệm hệ thống thương mại điện tử ngành dịch vụ điện ảnh.
