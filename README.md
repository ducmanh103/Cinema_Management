# Cinema Management System

## Giới thiệu
Cinema Management System là ứng dụng web được xây dựng bằng **ASP.NET Core Web App (Model–View–Controller)** trên nền **.NET 8** và **SQL Server**.  
Đề tài tập trung vào **xây dựng backend cho ứng dụng web quản lý rạp chiếu phim**, theo đúng kiến trúc MVC.

Hệ thống hỗ trợ quản lý thông tin phim, thể loại, người dùng và là nền tảng cho các chức năng nghiệp vụ của rạp chiếu phim.

---

## Công nghệ sử dụng
- ASP.NET Core Web App (MVC)
- .NET 8
- Entity Framework Core 8
- SQL Server
- Razor View
- Bootstrap

---

## Chức năng chính
- Quản lý phim (xem danh sách, thêm, sửa, xóa)
- Gán nhiều thể loại cho một phim
- Quản lý trạng thái phim:
  - Now Showing (Đang chiếu)
  - Coming Soon (Sắp chiếu)
  - Ended (Ngừng chiếu)
- Seed dữ liệu ban đầu (Role, Admin, Genre)

---

## Cơ sở dữ liệu
Hệ thống sử dụng cơ sở dữ liệu quan hệ với các bảng chính:
- Users
- Roles
- Movies
- Genres
- MovieGenres
- Theaters
- Rooms
- Showtimes
- Tickets

Dữ liệu được quản lý thông qua **Entity Framework Core**.

---

## Tài khoản mặc định
TK: admin
Mk: 1
