# 🎬 Cinema Management System (Hệ Thống Quản Lý Đặt Vé Xem Phim)

Chào mừng bạn đến với **Cinema Management System** – Hệ thống quản lý và đặt vé xem phim trực tuyến hiện đại được phát triển trên nền tảng **ASP.NET Core MVC 8.0** và **SQL Server**. Hệ thống cung cấp trải nghiệm toàn diện cho cả khách hàng đặt vé lẫn người quản trị rạp chiếu phim, tích hợp thanh toán trực tuyến qua cổng **VNPAY**.

---

## 🚀 Tính Năng Chính (Key Features)

### 👥 Dành Cho Khách Hàng (Customer Area)
* **Trang Chủ Hiện Đại**: Hiển thị danh sách phim đang chiếu, phim sắp chiếu với banner và trailer bắt mắt.
* **Tìm Kiếm & Lọc Phim**: Tìm kiếm phim theo tên, thể loại, quốc gia, thời lượng.
* **Chi Tiết Phim**: Xem thông tin chi tiết về phim (đạo diễn, diễn viên, nội dung, trailer, đánh giá).
* **Xem Lịch Chiếu & Rạp**: Tra cứu lịch chiếu theo rạp, theo ngày và giờ chiếu.
* **Đặt Vé & Chọn Ghế**: Giao diện chọn ghế ngồi trực quan (ghế thường, ghế VIP, ghế đôi) theo thời gian thực.
* **Thanh Toán VNPAY**: Tích hợp cổng thanh toán trực tuyến VNPAY (môi trường Sandbox) an toàn, nhanh chóng và hiển thị trạng thái giao dịch ngay lập tức.
* **Quản Lý Tài Khoản**: Đăng ký, đăng nhập và xem lịch sử đặt vé cá nhân.

### 🛡️ Dành Cho Quản Trị Viên (Admin Dashboard)
* **Tổng Quan & Thống Kê**: Biểu đồ báo cáo doanh thu theo phim, theo rạp, số lượng vé bán ra trong tháng.
* **Quản Lý Phim**: Thêm, sửa, xóa thông tin phim, cập nhật trạng thái phim (Đang chiếu / Sắp chiếu / Ngừng chiếu).
* **Quản Lý Lịch Chiếu**: Phân bổ suất chiếu, chọn phòng chiếu và thiết lập giá vé.
* **Quản Lý Thành Viên**: Quản lý thông tin tài khoản người dùng, phân quyền (User / Admin).
* **Quản Lý Vé & Hóa Đơn**: Tra cứu lịch sử thanh toán, kiểm tra trạng thái vé và doanh thu từ cổng VNPAY.

### 🔌 Hệ Thống API & Swagger Docs
* **RESTful APIs**: Cung cấp các endpoint quản lý Phim, Lịch chiếu, Vé và Người dùng.
* **Swagger UI Integration**: Tài liệu API tương tác trực quan cho phép lập trình viên dễ dàng tích hợp hoặc mở rộng ứng dụng trên thiết bị di động (Mobile App).

---

## 🛠️ Công Nghệ Sử Dụng (Technology Stack)

* **Backend**: ASP.NET Core MVC 8.0, C#
* **Cơ sở dữ liệu**: SQL Server, Entity Framework Core (Code First & Database First)
* **Authentication**: Cookie-based Authentication
* **Cổng thanh toán**: VNPAY Payment Gateway SDK
* **Frontend**: HTML5, CSS3, Javascript (Vanilla ES6), Bootstrap 5, Razor Views
* **Tài liệu API**: Swagger / OpenAPI 3.0

---

## ⚙️ Hướng Dẫn Cài Đặt & Chạy Dự Án (Setup Guide)

### 📋 Yêu Cầu Hệ Thống (Prerequisites)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (hoặc SQL Server Express)
* [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Khuyên dùng) hoặc VS Code

### 💻 Các Bước Cài Đặt

#### Bước 1: Clone dự án hoặc tải mã nguồn về máy
```bash
git clone https://github.com/ducmanh103/Cinema_Management.git
cd Cinema_Management
```

#### Bước 2: Cấu hình Cơ sở dữ liệu (Database Setup)
1. Mở file [appsettings.json](file:///c:/Users/Admin/source/repos/Cinema_Management/appsettings.json) và cập nhật lại chuỗi kết nối SQL Server của bạn tại mục `ConnectionStrings`:
   ```json
   "ConnectionStrings": {
       "DefaultConnection": "Server=TÊN_SERVER_CỦA_BẠN;Database=CinemaManagement;Trusted_Connection=True;TrustServerCertificate=True"
   }
   ```
2. Bạn có thể sử dụng file SQL Backup tại thư mục `DB/CinemaManagement.sql` để import cấu trúc database có sẵn vào SQL Server.
3. Khi bạn khởi chạy ứng dụng lần đầu tiên, hệ thống sẽ tự động gọi `DbInitializer.Seed()` để khởi tạo các dữ liệu mẫu cần thiết (Tài khoản admin mặc định, danh sách phim mẫu, phòng chiếu, lịch chiếu...).

#### Bước 3: Chạy ứng dụng
* **Sử dụng Visual Studio**: 
  1. Mở file `CINEMA MANAGEMENT.sln`.
  2. Bấm phím `F5` hoặc nút **Start** để biên dịch và chạy.
* **Sử dụng Terminal / CLI**:
  ```bash
  dotnet restore
  dotnet run
  ```
Sau khi chạy thành công, ứng dụng sẽ khởi động tại: `https://localhost:7079` (hoặc cổng được cấu hình trên máy bạn).

---

## 💳 Tích Hợp & Thử Nghiệm Thanh Toán VNPAY

Cổng thanh toán VNPAY đã được tích hợp sẵn ở chế độ **Sandbox** (Thử nghiệm). Cấu hình chi tiết nằm trong `appsettings.json`:

```json
"VnPay": {
    "TmnCode": "FI1FU3SC",
    "HashSecret": "IXINUVQPXBC1H3SK9LXFPXY2QQR9DHZU",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "Version": "2.1.0",
    "Command": "pay",
    "CurrCode": "VND",
    "Locale": "vn"
}
```

### 💳 Hướng dẫn test thanh toán:
1. Khi đặt vé, chọn phương thức thanh toán **VNPAY**.
2. Hệ thống sẽ tự động điều hướng sang cổng thanh toán của VNPAY.
3. Sử dụng thông tin thẻ test dưới đây của VNPAY để thanh toán:
   * **Ngân hàng**: NCB
   * **Số thẻ**: `9704198526191432198`
   * **Tên chủ thẻ**: `NGUYEN VAN A`
   * **Ngày phát hành**: `07/15`
   * **Mã OTP**: `123456`
4. Sau khi thanh toán thành công, VNPAY sẽ trả kết quả về ứng dụng qua `ReturnUrl`, hệ thống sẽ ghi nhận hóa đơn và hiển thị vé đã mua.

---

## 📂 Cấu Trúc Thư Mục Dự Án

* `Controllers/`: Điều hướng các yêu cầu từ phía Client, xử lý logic nghiệp vụ và trả về View tương ứng.
* `Controllers/Api/`: Các API endpoint phục vụ cho việc tích hợp hoặc phát triển app di động.
* `Models/`: Định nghĩa các cấu trúc dữ liệu Entity Framework, ViewModels và Dto.
* `Services/`: Chứa mã nguồn thực thi nghiệp vụ (Movies, Showtimes, Tickets, VNPAY) nhằm tách biệt logic khỏi Controller.
* `Views/`: Các giao diện Razor tương tác với người dùng (Home, Booking, Movies, Account, Admin, Payment).
* `DB/`: File SQL backup dữ liệu dự án.
* `wwwroot/`: Tài nguyên tĩnh bao gồm CSS, JS cá nhân, hình ảnh và banner phim.

---

## 🔐 Tài Khoản Đăng Nhập Mẫu

* **Tài khoản Admin**:
  * **Email**: `admin@cinema.com`
  * **Mật khẩu**: `Admin@123`
* **Tài khoản Khách hàng**:
  * **Email**: `user@cinema.com`
  * **Mật khẩu**: `User@123`

---

## 🛠️ Hướng Dẫn Truy Cập Tài Liệu API (Swagger)
Ở chế độ **Development**, bạn có thể truy cập tài liệu API trực tiếp bằng cách thêm đường dẫn `/swagger` vào sau URL gốc:
👉 `https://localhost:7079/swagger`
