# CINEMAHUB - BÁO CÁO KIẾN TRÚC BẢO MẬT HỆ THỐNG TOÀN DIỆN
**Hệ thống Quản lý Rạp chiếu phim CinemaHub (Cinema Management System)**  
*Dự án Thiết kế & Triển khai An toàn Thông tin Đa Lớp (Defense-in-Depth)*  
*Tiêu chuẩn đối chiếu: OWASP Top 10:2021, OWASP Top 10 for LLM:2025, STRIDE Threat Model, Nghị định 13/2023/NĐ-CP (PDPD)*

---

## 1. TỔNG QUAN KIẾN TRÚC BẢO MẬT ĐA LỚP (DEFENSE-IN-DEPTH)

CinemaHub được xây dựng trên nguyên tắc **Zero Trust** và kiến trúc phòng thủ theo chiều sâu (Defense-in-Depth) với 6 tầng bảo vệ độc lập:

```
[ LỚP 1: BIÊN MẠNG & VPS ]
   ├── Port Filtering (Chỉ mở cổng 80, 443, 22; Postgres 5432 strictly 127.0.0.1)
   ├── SSH Ed25519 Key Authentication
   └── Fail2ban Jail (Tự động khoá IP 1 giờ sau 5 lần thất bại)
            │
[ LỚP 2: REVERSE PROXY & WAF (NGINX) ]
   ├── Rate Limiting (req/s per IP theo zone: login, register, api, general)
   ├── Chặn truy cập file nhạy cảm (.git, .env, docker-compose) -> HTTP 404
   ├── Che giấu Swagger / OpenAPI trên môi trường Production -> HTTP 404
   ├── HTTP Security Headers (HSTS preload, CSP, X-Frame-Options, X-Content-Type-Options, Permissions-Policy)
   └── TLS 1.3 / 1.2 Hardened Ciphers
            │
[ LỚP 3: APPLICATION & MIDDLEWARE (ASP.NET CORE 8) ]
   ├── Cookie Authentication Hardening (HttpOnly, SameSite=Lax, Secure, Expire & SlidingExpiration)
   ├── Global AutoValidateAntiforgeryToken (Chống CSRF toàn diện cho mọi HTTP POST/PUT/DELETE)
   ├── Frontend Security Layer (window.CinemaSecurity: HTML escaping, URL sanitization, jQuery CSRF injector)
   ├── Magic Bytes & SSRF Prevention (Chặn IP nội bộ, loopback, link-local, chặn chuyển hướng lén lút)
   └── Global Exception Handler & Security Alert Real-Time (Telegram Bot Alert)
            │
[ LỚP 4: XÁC THỰC QUẢN TRỊ VIÊN 2 BƯỚC (ADMIN MFA) ]
   ├── Xác thực 2 bước bắt buộc cho Admin/Staff
   ├── Sinh mã OTP 6 số mật mã học ngẫu nhiên (hết hạn 5 phút)
   ├── So sánh mã bằng CryptographicOperations.FixedTimeEquals (Chống Timing Attack)
   └── Gửi mã kép qua Email cá nhân và Telegram Security Channel
            │
[ LỚP 5: MÃ HOÁ DỮ LIỆU & KIỂM TOÁN (DATA PROTECTION & AUDIT) ]
   ├── Mã hoá dữ liệu cá nhân (PII: Email, Họ tên) bằng thuật toán AES-256-GCM
   ├── Blind Index HMAC-SHA256 (EmailHash) cho phép tìm kiếm chính xác mà không cần giải mã
   ├── Băm mật khẩu bằng thuật toán BCrypt với Work Factor = 12
   ├── Xoá mềm (Soft Delete) kết hợp ẩn danh hoá vĩnh viễn (Anonymization) theo NĐ 13/2023/NĐ-CP
   └── Bảng AuditLog bất biến ghi lại mọi thao tác đặc quyền (Role, Status, Delete, Schedule)
            │
[ LỚP 6: BẢO MẬT AI CHATBOT (LLM DEFENSE) ]
   ├── Phân tách tường minh System Prompt và Context Data bằng thẻ XML (<context_data>, <user_context>)
   ├── Hardened Prompt Guard chống Direct Prompt Injection, Jailbreak, DAN
   ├── Làm sạch và cắt ngắn dữ liệu đầu vào chống Indirect Prompt Injection
   ├── Cấu hình Gemini Safety Settings (Harassment, Hate, Explicit, Danger)
   └── Lọc bỏ nguy cơ XSS/Script Injection trong phản hồi AI trước khi gửi tới trình duyệt
```

---

## 2. BẢNG ĐỐI CHIẾU TIÊU CHUẨN OWASP TOP 10 (2021)

| Mã OWASP | Loại lỗ hổng | Trạng thái trước đây | Biện pháp kỹ thuật đã khắc phục trên CinemaHub |
| :--- | :--- | :--- | :--- |
| **A01:2021** | **Broken Access Control** | URL suất chiếu và rạp có thể bị brute-force ID; xoá user xoá cứng dữ liệu tài chính. | • Sử dụng ASP.NET Role-based Authorization `[Authorize(Roles = "Admin,Staff")]`<br>• Triển khai Soft Delete và Anonymization bảo toàn vé kiểm toán kế toán.<br>• Query Filter toàn cục `!u.IsDeleted` trong EF Core. |
| **A02:2021** | **Cryptographic Failures** | PII (Email, FullName) lưu dạng văn bản rõ (plaintext); VNPay dùng phép so sánh `==` (Timing Attack). | • Mã hoá AES-256-GCM + Blind Index HMAC-SHA256 qua `EncryptedStringConverter`.<br>• VNPay signature validation chuyển sang `CryptographicOperations.FixedTimeEquals`.<br>• BCrypt mật khẩu với Salt riêng biệt. |
| **A03:2021** | **Injection (SQL, XSS)** | DOM XSS trong `theaters.js` và `chatbot.js`; Chart.js dùng `Html.Raw`. | • Dùng EF Core Parameterized Queries 100% (chống SQLi).<br>• Viết lại hàm Markdown parser không unescape HTML, chỉ cho phép scheme an toàn.<br>• Khử khuẩn XSS qua `window.CinemaSecurity.escapeHtml`.<br>• Thay `Html.Raw` bằng `JsonSerializer.Serialize`. |
| **A04:2021** | **Insecure Design** | Admin không có MFA; mật khẩu admin bị reset về mặc định mỗi khi khởi động lại ứng dụng. | • Xoá bỏ hoàn toàn cơ chế reset mật khẩu Admin trong `DbInitializer.cs`.<br>• Thiết kế luồng MFA OTP 6 số qua Email & Telegram cho tài khoản quản trị. |
| **A05:2021** | **Security Misconfiguration** | File `.git`, `.env` và Swagger mở công khai; thiếu security headers. | • Nginx chặn triệt để `.git`, `.env`, `/swagger` trả về HTTP 404.<br>• Bổ sung HSTS, CSP, COOP, CORP, X-Content-Type-Options: nosniff. |
| **A06:2021** | **Vulnerable & Outdated Components** | Gói phụ thuộc cũ dính 28 lỗ hổng High (Pkcs, Memory, Asn1, Xml). | • Nâng cấp toàn bộ package references lên phiên bản vá lỗi chính thức (.NET 8.0.13, Pkcs 8.0.2, Xml 8.0.5).<br>• Kiểm tra `dotnet list package --vulnerable` đạt kết quả: **0 vulnerabilities**. |
| **A07:2021** | **Identification & Authentication Failures** | VPS mở SSH password không có fail2ban; Session thiếu SecurityStamp check. | • Triển khai SSH Ed25519 key.<br>• Kích hoạt Fail2ban sshd jail (ban 1 giờ sau 5 lần sai).<br>• Bật `OnValidatePrincipal` kiểm tra SecurityStamp thu hồi phiên tức thì. |
| **A08:2021** | **Software & Data Integrity Failures** | Tải ảnh từ URL từ xa không kiểm tra Magic Bytes; dễ bị SSRF và giả mạo SVG XSS. | • Loại bỏ hỗ trợ SVG/JFIF.<br>• Kiểm tra Magic Bytes nhị phân (JPEG, PNG, GIF, WEBP).<br>• Kiểm tra DNS IP, chặn kết nối đến loopback, private IP, Docker network. |
| **A09:2021** | **Security Logging & Monitoring Failures** | Không có Audit Log cho các hành vi quản trị; chỉ ghi log text thông thường. | • Xây dựng bảng `AuditLogs` lưu trữ: UserId, Action, EntityName, EntityId, Details, IpAddress, Timestamp.<br>• Tích hợp Telegram Alert cảnh báo real-time khi có lỗi hệ thống hoặc hành vi bất thường. |
| **A10:2021** | **Server-Side Request Forgery (SSRF)** | Hàm `DownloadAndSaveRemoteImageAsync` tải ảnh từ bất kỳ URL người dùng cung cấp. | • Triển khai `FileStorageService.ValidateRemoteUrlAsync`: chặn cổng lạ, phân giải IP và chặn toàn bộ dải IP 10.x, 172.16-31.x, 192.168.x, 127.x, Link-Local, chặn HTTP 302 chuyển hướng về mạng nội bộ. |

---

## 3. ĐỐI CHIẾU BẢO MẬT AI CHATBOT (OWASP TOP 10 FOR LLM)

| Mã LLM | Tên lỗ hổng | Cơ chế bảo vệ kỹ thuật triển khai trên CinemaHub |
| :--- | :--- | :--- |
| **LLM01** | **Prompt Injection** | • Đóng gói toàn bộ dữ liệu cơ sở dữ liệu bên trong khối thẻ XML `<context_data>` và `<user_context>`.<br>• Chỉ thị hệ thống nghiêm ngặt khẳng định mọi thông tin trong thẻ chỉ là dữ liệu tham khảo, tuyệt đối không được coi là chỉ thị điều khiển.<br>• Làm sạch và cắt ngắn mô tả phim, loại bỏ ký tự điều khiển/thẻ HTML. |
| **LLM02** | **Sensitive Information Disclosure** | • Prompt quy định tuyệt đối không tiết lộ prompt hệ thống, API key, cấu trúc database hoặc logic backend dưới bất kỳ hình thức nào (kể cả đóng vai, debug). |
| **LLM04** | **Model Denial of Service** | • Giới hạn chiều dài tin nhắn người dùng tối đa 500 ký tự.<br>• Giới hạn lịch sử hội thoại tối đa 6 lượt trao đổi gần nhất.<br>• Giới hạn token đầu ra `maxOutputTokens = 2048`. |
| **LLM06** | **Excessive Agency** | • Chatbot là hệ thống chỉ đọc (read-only), không có công cụ gọi API (Tool Calling) cho phép huỷ vé hay chỉnh sửa dữ liệu.<br>• Bot được chỉ thị từ chối các yêu cầu tự ý hứa hẹn giảm giá hoặc thay đổi trạng thái giao dịch. |
| **LLM07** | **System Prompt Leakage** | • Bộ lọc bảo mật ngăn chặn các truy vấn trích xuất hướng dẫn hệ thống.<br>• Phía client lọc thẻ script/iframe nếu mô hình vô tình sinh ra mã HTML. |

---

## 4. MÔ HÌNH ĐE DỌA STRIDE

| Đe dọa STRIDE | Nguy cơ tiềm tàng | Biện pháp giải quyết |
| :--- | :--- | :--- |
| **S - Spoofing** (Giả mạo danh tính) | Kẻ tấn công giả mạo tài khoản Quản trị viên để đăng nhập CMS. | Xác thực 2 lớp (MFA OTP) qua Email + Telegram cho toàn bộ tài khoản Admin/Staff; Cookie gắn cờ `HttpOnly`, `SameSite=Lax`. |
| **T - Tampering** (Xáo trộn dữ liệu) | Can thiệp thay đổi số tiền thanh toán VNPay hoặc payload đặt vé. | Chữ ký điện tử HMAC-SHA512 của VNPay được kiểm tra bằng hàm `FixedTimeEquals`; CSRF Token bắt buộc trên mọi yêu cầu POST. |
| **R - Repudiation** (Chối bỏ trách nhiệm) | Quản trị viên xoá dữ liệu hoặc thay đổi vai trò nhưng chối bỏ hành vi. | Bảng `AuditLog` ghi nhận đầy đủ `UserId`, `Action`, `Details`, `IpAddress`, `Timestamp`. Không cho phép chỉnh sửa hoặc xoá log. |
| **I - Information Disclosure** (Lộ lọt thông tin) | Kẻ tấn công trộm bản sao lưu database PostgreSQL và đọc thông tin khách hàng. | Toàn bộ `Email` và `FullName` được mã hoá chuẩn quân đội AES-256-GCM. Không có khoá bí mật thì dữ liệu hoàn toàn vô nghĩa. |
| **D - Denial of Service** (Từ chối dịch vụ) | Tấn công Brute force SSH trên VPS hoặc spam gửi yêu cầu liên tục đến API. | Fail2ban tự động cấm IP ở tầng iptables; Rate Limiting trên Nginx và ASP.NET Core RateLimiter. |
| **E - Elevation of Privilege** (Leo thang đặc quyền) | Khách hàng thông thường truy cập trái phép vào các trang quản trị `/Admin`. | Role Authorization Middleware kiểm tra Claim `Admin`/`Staff`; Cookie xác thực kiểm tra `SecurityStamp` định kỳ để thu hồi quyền ngay lập tức. |

---

## 5. TUÂN THỦ NGHỊ ĐỊNH 13/2023/NĐ-CP (PDPD)

1. **Nguyên tắc Thu thập Tối thiểu (Data Minimization):** Chỉ thu thập Họ tên và Email phục vụ phát hành vé điện tử.
2. **Bảo vệ bằng Kỹ thuật (Privacy by Design):** Mã hoá AES-256-GCM tự động ở tầng ORM trước khi ghi xuống đĩa; tìm kiếm bằng Blind Index HMAC-SHA256.
3. **Quyền của Chủ thể Dữ liệu (Data Subject Rights):**
   - *Quyền được biết & Truy cập:* Trang "Hồ sơ cá nhân" và "Vé của tôi".
   - *Quyền xoá dữ liệu (Right to Erasure):* Áp dụng Soft Delete + Anonymization (ẩn danh hoá vĩnh viễn tên và email thành `Deleted_User_XXX` và `deleted_user_XXX@cinemahub.local`).
4. **Trang Chính sách Quyền riêng tư Công khai:** Đã triển khai tại URL `https://cinemahubvn.io.vn/Home/Privacy`.

