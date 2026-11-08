# 🎬 CinemaHub - Enterprise Cinema Management & Online Booking Platform

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white" alt="C# 12" />
  <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL 16" />
  <img src="https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
  <img src="https://img.shields.io/badge/Nginx-009639?style=for-the-badge&logo=nginx&logoColor=white" alt="Nginx" />
  <img src="https://img.shields.io/badge/Security-OWASP%20Top%2010%20Compliant-red?style=for-the-badge&logo=shield" alt="OWASP Compliant" />
  <img src="https://img.shields.io/badge/Vulnerabilities-0%20(Clean)-brightgreen?style=for-the-badge&logo=security" alt="Zero Vulnerabilities" />
  <img src="https://img.shields.io/badge/Status-Live%20on%20Production-success?style=for-the-badge&logo=vercel" alt="Live Demo" />
</p>

---

## 🌐 Trải Nghiệm Trực Tiếp (Live Production Demo)
- **Production URL:** [https://cinemahubvn.io.vn](https://cinemahubvn.io.vn)
- **Chứng chỉ bảo mật:** SSL/TLS 1.3 Let's Encrypt, HSTS Preload, A+ Rating
- **Hạ tầng triển khai:** Ubuntu 24.04 LTS VPS, Docker Containerization, Nginx Reverse Proxy, Automated CI/CD

---

## 📌 Tổng Quan Dự Án (Project Overview)

**CinemaHub** là nền tảng quản lý rạp chiếu phim và thương mại điện tử đặt vé trực tuyến cấp doanh nghiệp (Enterprise-grade), được thiết kế và xây dựng theo chuẩn kiến trúc nguyên khối mô-đun hoá (Modular Monolith) trên nền tảng **ASP.NET Core 8 MVC** và **PostgreSQL 16**.

Hệ thống được phát triển với trọng tâm giải quyết hai bài toán kỹ thuật phức tạp trong kỹ thuật phần mềm thực tế:
1. **Xử lý đồng thời cao (High-Concurrency & Race Condition):** Giữ ghế thời gian thực, chống đặt trùng vé (Zero Overbooking) với cơ chế khoá ghế tạm thời và dịch vụ nền (Background Worker) tự động giải phóng tài nguyên.
2. **Kiến trúc An toàn Thông tin Đa lớp (Defense-in-Depth):** Bảo vệ toàn diện theo tiêu chuẩn **OWASP Top 10:2021**, bảo mật AI theo **OWASP Top 10 for LLM:2025**, mã hoá dữ liệu cá nhân (PII) bằng thuật toán **AES-256-GCM + Blind Index (HMAC-SHA256)** và tuân thủ nghiêm ngặt **Nghị định 13/2023/NĐ-CP (PDPD)** của Chính phủ Việt Nam.

---

## 🏛️ Sơ Đồ Kiến Trúc Hệ Thống (System Architecture)

```
[ INTERNET CLIENTS ] (Web Browsers, Mobile Devices)
         │
         ▼ (HTTPS / TLS 1.3)
┌────────────────────────────────────────────────────────────────────────┐
│ LỚP BIÊN MẠNG & WAF (REVERSE PROXY - NGINX & LINUX HARDENING)          │
│ • Fail2ban SSH & UFW Firewall Protection (Ban 1h sau 5 lần vi phạm)    │
│ • Nginx Layer 7 Rate Limiting (Zone: Login, Register, API, General)    │
│ • Chặn triệt để tệp nhạy cảm (.git, .env, docker-compose) -> HTTP 404  │
│ • Ẩn Swagger / OpenAPI trên môi trường Production                      │
│ • HTTP Security Headers (HSTS Preload, Strict CSP, X-Frame-Options)   │
└────────────────────────────────────┬───────────────────────────────────┘
                                     │ (Internal Docker Network)
                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG ỨNG DỤNG (ASP.NET CORE 8.0 RUNTIME)                               │
│ • Security Middlewares: Global Antiforgery, Cookie Hardening, CSP      │
│ • Concurrency Control: Seat Locking Manager & In-Memory Distributed Lock│
│ • Authentication & AuthZ: Cookie Auth + Admin 2-Factor Auth (MFA OTP)  │
│ • SSRF Protection & Image Magic Bytes binary validation                │
│ • Real-time Monitoring: Telegram Alert Bot & Serilog Structured Logs   │
└──────────────────┬──────────────────────────────────┬──────────────────┘
                   │                                  │
                   ▼ (Encrypted Connection)           ▼ (REST API)
┌────────────────────────────────────────┐  ┌────────────────────────────┐
│ CƠ SỞ DỮ LIỆU (POSTGRESQL 16)          │  │ CÁC DỊCH VỤ TÍCH HỢP NGOÀI │
│ • ValueConverter: AES-256-GCM PII Enc  │  │ • Cổng VNPAY (HMAC-SHA512) │
│ • Blind Index: HMAC-SHA256 (EmailHash) │  │ • Google Gemini AI 2.5     │
│ • Soft Delete & Permanent Anonymization│  │ • Google OAuth 2.0         │
│ • Bảng AuditLog bất biến ghi vết admin │  │ • SMTP Mail Service (TLS)  │
└────────────────────────────────────────┘  └────────────────────────────┘
```

---

## 🛡️ Điểm Nhấn An Toàn Thông Tin (Security & Compliance Highlights)

Hệ thống được thiết kế theo mô hình **Zero Trust** với 6 lớp bảo vệ độc lập, đã được kiểm thử và rà soát thực tế:

### 1. Mã hoá Dữ liệu Cá nhân (PII Encryption) & Blind Indexing
- **Thuật toán mã hóa:** Áp dụng **AES-256-GCM** (chuẩn quân đội) cho dữ liệu định danh người dùng (`Email`, `FullName`) trực tiếp ở tầng Entity Framework Core qua `ValueConverter`. Dữ liệu ghi xuống PostgreSQL luôn ở dạng mã hoá nhị phân Base64 (`PIIENC:...`), triệt tiêu rủi ro lộ lọt thông tin khi cơ sở dữ liệu bị dump.
- **Blind Index (HMAC-SHA256):** Khắc phục nhược điểm không thể tìm kiếm của dữ liệu mã hoá mà không cần giải mã bộ nhớ bằng trường `EmailHash` có khoá bí mật (Secret Salt). Cho phép tìm kiếm chính xác (`O(1)` qua B-Tree Index) cho luồng Đăng nhập, Google OAuth và Khôi phục mật khẩu.

### 2. Xác thực 2 bước (MFA) cho Quản trị viên
- Mọi tài khoản đặc quyền (`Admin`, `Staff`) sau khi nhập đúng mật khẩu bắt buộc phải hoàn thành bước xác thực OTP 6 số.
- Mã OTP sinh ngẫu nhiên bằng bộ sinh số mật mã học, lưu trong bộ nhớ đệm an toàn với thời gian sống 5 phút.
- Gửi mã tức thời qua **Email cá nhân** và kênh **Telegram Security Channel**.
- Cơ chế đối soát sử dụng hàm so sánh hằng số thời gian `CryptographicOperations.FixedTimeEquals` để triệt tiêu hoàn toàn tấn công kênh bên (Timing Attack).

### 3. Phòng chống tấn công tầng Web (OWASP Top 10:2021)
- **Chống CSRF:** Kích hoạt `AutoValidateAntiforgeryTokenAttribute` toàn cục trên toàn bộ ứng dụng ASP.NET Core; tự động nạp CSRF token vào mọi yêu cầu AJAX/Fetch từ frontend.
- **Chống XSS:** Thư viện `window.CinemaSecurity` khử khuẩn HTML (`escapeHtml`), làm sạch URL (`sanitizeUrl`); viết lại toàn bộ bộ phân tích cú pháp Markdown cho Chatbot, loại bỏ khả năng tiêm mã JavaScript độc hại.
- **Chống SSRF & Tải tệp độc hại:** Dịch vụ lưu trữ tệp kiểm tra Magic Bytes nhị phân (chỉ chấp nhận JPEG, PNG, GIF, WEBP; loại bỏ SVG XSS); phân giải DNS và chặn toàn bộ kết nối đến dải IP riêng tư (Private/Loopback/Docker subnet), chặn HTTP 302 chuyển hướng ngầm.
- **Không còn gói phụ thuộc dính lỗ hổng (Zero Vulnerabilities):** Đã override và cập nhật các gói transitively dính lỗ hổng High severity trong .NET (`Microsoft.Extensions.Caching.Memory`, `System.Security.Cryptography.Pkcs`, `System.Security.Cryptography.Xml`). Kết quả kiểm tra đạt: **0 Vulnerabilities**.

### 4. Bảo mật AI Chatbot (OWASP Top 10 for LLM)
- **Chống Prompt Injection (Direct & Indirect):** Phân tách tuyệt đối giữa System Instructions và dữ liệu rạp phim bằng các thẻ XML đóng gói `<context_data>` và `<user_context>`.
- **System Prompt Boundary:** Chỉ thị mô hình tuân thủ quy tắc bất biến, từ chối mọi yêu cầu Jailbreak, DAN, Roleplay, không bao giờ tiết lộ prompt hệ thống hay API keys.
- **Làm sạch ngữ cảnh & Phản hồi:** Cắt ngắn mô tả phim, loại bỏ ký tự điều khiển; tự động lọc các thẻ HTML độc hại trong câu trả lời từ mô hình AI.

### 5. Tuân thủ Pháp lý & Nghị định 13/2023/NĐ-CP (PDPD)
- **Quyền được xoá dữ liệu (Right to Erasure):** Triển khai cơ chế Xoá mềm (Soft Delete) kết hợp ẩn danh hoá vĩnh viễn (Anonymization) thông tin cá nhân thành `Deleted_User_XXX`, đồng thời bảo toàn vé và số liệu tài chính phục vụ kiểm toán kế toán.
- **Quy trình Ứng phó Sự cố (IRP):** Xây dựng quy trình chuẩn hóa theo **NIST SP 800-61 Rev. 2** và cam kết thông báo cơ quan quản lý (Cục A05 - Bộ Công an) trong vòng 72 giờ khi phát sinh sự cố lộ lọt PII.
- **Tài liệu tham chiếu:** Chi tiết xem tại [docs/SECURITY.md](docs/SECURITY.md) và [docs/INCIDENT_RESPONSE.md](docs/INCIDENT_RESPONSE.md).

---

## ⚡ Các Thách Thức Kỹ Thuật Đã Giải Quyết (Key Engineering Challenges)

### 1. Bài toán Race Condition trong Đặt vé & Giữ ghế đồng thời
- **Thách thức:** Trong các khung giờ cao điểm hoặc phim bom tấn, hàng trăm khách hàng có thể cùng chọn một vị trí ghế VIP tại cùng một tích tắc.
- **Giải pháp:**
  - Áp dụng cơ chế **Khóa bi quan/lạc quan (Pessimistic/Optimistic Concurrency)** ở tầng cơ sở dữ liệu kết hợp xác thực trạng thái ghế tức thời tại thời điểm submit.
  - Chuyển trạng thái ghế sang `Holding` với dấu thời gian hết hạn (15 phút).
  - Xây dựng **Background Hosted Service** (`PendingBookingCleanupService`) chạy ngầm theo chu kỳ, tự động quét và thu hồi các giao dịch quá hạn thanh toán, hoàn trả ghế trống về rạp mà không làm khóa bảng hay nghẽn luồng.

### 2. Tối ưu Hiệu năng trên Phần cứng Giới hạn (Resource Optimization)
- **Thách thức:** Vận hành toàn bộ hệ sinh thái (Nginx, Web App .NET 8, PostgreSQL 16, Certbot, Fail2ban) ổn định trên máy chủ ảo VPS chỉ có **2 vCPU và 2 GB RAM**.
- **Giải pháp:**
  - Tối ưu hóa Garbage Collector của .NET: Chuyển cấu hình từ Server GC sang **Workstation GC** (`<ServerGarbageCollection>false</ServerGarbageCollection>`), giảm mức tiêu thụ RAM tĩnh từ ~600MB xuống dưới ~180MB.
  - Phân vùng bộ nhớ đệm (Caching Strategy) 2 lớp: Cache ảnh tĩnh và metadata phim bằng `IMemoryCache` có thời gian trượt (Sliding Expiration).
  - Tối ưu truy vấn EF Core: Áp dụng `.AsNoTracking()` cho 100% các thao tác đọc dữ liệu và viết lại toàn bộ bộ lọc truy vấn suất chiếu admin theo phân trang và chỉ mục thời gian.

---

## 💻 Ngăn Xếp Công Nghệ (Technology Stack)

| Phân tầng | Công nghệ / Thư viện lựa chọn | Mục đích sử dụng |
| :--- | :--- | :--- |
| **Backend Core** | C# 12, .NET 8.0 SDK (ASP.NET Core MVC & Web API) | Ngôn ngữ hiện đại, hiệu năng cao, kiểu dữ liệu an toàn |
| **Cơ sở dữ liệu** | PostgreSQL 16 (Alpine Container) | RDBMS mã nguồn mở mạnh mẽ, hỗ trợ Indexing nâng cao |
| **Truy cập Dữ liệu** | Entity Framework Core 8.0, Npgsql | ORM chuẩn hóa, hỗ trợ ValueConverter mã hoá trong suốt |
| **Bảo mật & Mã hoá** | AES-256-GCM, HMAC-SHA256, BCrypt.Net-Next | Mã hoá PII, Blind Index, Hashing mật khẩu chuẩn an toàn |
| **Reverse Proxy & WAF** | Nginx (Alpine), Fail2ban, Let's Encrypt Certbot | TLS 1.3 Termination, Rate Limiting, Chống dò quét |
| **Thanh toán Điện tử** | VNPAY Payment Gateway | Xử lý thanh toán trực tuyến qua mã QR, ATM và Thẻ quốc tế |
| **Trí tuệ Nhân tạo (AI)**| Google Gemini API (`gemini-2.5-flash`) | Trợ lý ảo AI tư vấn phim, lịch chiếu và hỗ trợ đặt vé |
| **Giám sát & Ghi log** | Serilog, Telegram Bot Alert, HealthChecks | Giám sát trạng thái 24/7 và gửi cảnh báo an ninh tức thì |
| **Frontend UI/UX** | Razor Views, Bootstrap 5, Vanilla JS (ES6), CSS Glassmorphism | Giao diện tối ưu di động, hiển thị sơ đồ ghế động |
| **CI/CD & DevOps** | Docker, Docker Compose, GitHub Actions, GHCR | Đóng gói container và tự động hoá quy trình triển khai |

---

## 📂 Cấu Trúc Mã Nguồn (Directory Structure)

```text
Cinema_Management/
├── .github/workflows/          # Quy trình CI/CD tự động (Gitleaks, Nuget Audit, Build, Deploy)
├── Areas/Admin/                # Phân hệ Quản trị viên & Nhân viên (CMS)
│   ├── Controllers/            # Quản lý Phim, Suất chiếu, Doanh thu, Người dùng, Xác thực MFA
│   └── Views/                  # Giao diện quản trị phong cách Dark Mode chuyên nghiệp
├── Controllers/                # Phân hệ Khách hàng & Public API
│   ├── AccountController.cs    # Đăng nhập, Đăng ký, Google OAuth, Profile, Blind Index Search
│   ├── BookingController.cs    # Đặt vé, Giữ ghế thời gian thực, Phòng chống xung đột
│   ├── ChatBotController.cs    # Trợ lý ảo Gemini AI với Prompt Guard & Ngữ cảnh rạp phim
│   ├── HomeController.cs       # Trang chủ, Cụm rạp, Tin tức, Chính sách Quyền riêng tư
│   └── PaymentController.cs    # Xử lý Callback VNPAY & Xác thực chữ ký số HMAC-SHA512
├── Data/                       # Entity Framework Context & Value Converters mã hoá
│   ├── CinemaDbContext.cs      # Khai báo DbSet, Query Filters, Indexing & Relationship
│   ├── EncryptedStringConverter.cs # Converter tự động mã hoá/giải mã AES-256-GCM
│   └── DbInitializer.cs        # Tự động khởi tạo schema, DDL và dữ liệu ban đầu
├── docs/                       # Tài liệu an toàn thông tin chuyên sâu
│   ├── SECURITY.md             # Báo cáo kiến trúc bảo mật toàn diện & Bảng đối chiếu OWASP
│   └── INCIDENT_RESPONSE.md    # Kế hoạch ứng phó sự cố ATTT theo NIST & Nghị định 13
├── Models/                     # Data Entities, ViewModels, DTOs & AuditLog Entity
├── Services/                   # Tầng nghiệp vụ (Business Logic) & Dịch vụ chạy ngầm
│   ├── PiiEncryptionService.cs # Xử lý mật mã học AES-256-GCM và Blind Index HMAC-SHA256
│   ├── PendingBookingCleanupService.cs # Worker giải phóng ghế hết hạn thanh toán
│   ├── TelegramSecurityAlertService.cs # Gửi thông báo an ninh thời gian thực
│   └── VnPayLibrary.cs         # Thư viện kiểm tra chữ ký số chống Timing Attack
├── nginx/                      # Cấu hình Nginx Reverse Proxy, SSL & Rate Limit
├── docker-compose.prod.yml     # Khởi chạy cụm dịch vụ Production (Web, DB, Nginx, Certbot)
└── Dockerfile                  # Đóng gói Multi-stage build container .NET 8 siêu nhẹ
```

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy Cục Bộ (Local Quickstart)

### 1. Yêu cầu tiên quyết
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 16](https://www.postgresql.org/download/) hoặc Docker Engine

### 2. Các bước khởi chạy
```bash
# 1. Clone repository
git clone https://github.com/ducmanh103/Cinema_Management.git
cd Cinema_Management

# 2. Cấu hình chuỗi kết nối và Secret
# Cập nhật thông tin trong appsettings.Development.json hoặc sử dụng dotnet user-secrets:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=CinemaManagement;Username=postgres;Password=YourPassword"
dotnet user-secrets set "Security:PiiMasterKey" "YOUR_32_BYTE_BASE64_KEY"
dotnet user-secrets set "Security:BlindIndexKey" "YOUR_32_BYTE_BASE64_KEY"

# 3. Phục hồi dependencies và kiểm tra an toàn gói
dotnet restore
dotnet list package --vulnerable --include-transitive

# 4. Khởi chạy ứng dụng
dotnet run --project CINEMA_MANAGEMENT.csproj
```
Ứng dụng sẽ tự động áp dụng DDL khởi tạo schema PostgreSQL và chạy tại `https://localhost:7001` (hoặc `http://localhost:5000`).

---

## 💳 Thông Tin Kiểm Thử Thanh Toán VNPAY Sandbox

Dự án đã tích hợp sẵn môi trường kiểm thử giả lập của cổng VNPAY:
- **Ngân hàng chấp nhận:** NCB (Ngân hàng Quốc Dân)
- **Số thẻ thử nghiệm:** `9704198526191432198`
- **Tên chủ thẻ:** `NGUYEN VAN A`
- **Ngày phát hành:** `07/15`
- **Mã OTP xác thực:** `123456`

---

## 👨‍💻 Thông Tin Tác Giả & Liên Hệ (Author)

- **Họ và tên:** Nguyễn Đức Mạnh
- **Vị trí định hướng:** Backend Developer / Fullstack Developer 
- **Hotline / Zalo:** 0344 596 643
- **Email:** ducmanh2005vt@gmail.com
- **LinkedIn / Portfolio:** [Nguyen Duc Manh](https://www.facebook.com/people/Nguy%E1%BB%85n-M%E1%BA%A1nh/pfbid0vjQvuoG87KCXZBA8h7E3yDrpT33HQcdHZh4U66qAz2Q5GNsBxDYGAmAy3ugrMLWEl/)
- **Live Platform:** [https://cinemahubvn.io.vn](https://cinemahubvn.io.vn)

---

<p align="center">
  <i>Dự án được xây dựng với mục tiêu chuẩn hoá giải pháp thương mại điện tử dịch vụ điện ảnh có độ tin cậy và mức độ an toàn thông tin cao nhất.</i>
</p>
