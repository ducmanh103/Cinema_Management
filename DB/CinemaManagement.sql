/* =======================================================
   CINEMA MANAGEMENT — FULL DATABASE SCRIPT (ULTIMATE)
   Bao gồm: 
   - Xóa và Tạo lại Database CinemaManagement
   - Hệ thống Roles & Users (Admin mặc định)
   - Hệ thống Phim, Thể loại & Banner ngang
   - Hệ thống Rạp, Phòng & Ghế
   - Hệ thống Suất chiếu & Đặt vé
   ======================================================= */

-- 1. XÓA DB NẾU ĐÃ TỒN TẠI
USE master;
GO
IF DB_ID('CinemaManagement') IS NOT NULL
BEGIN
    ALTER DATABASE CinemaManagement SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE CinemaManagement;
END
GO

-- 2. TẠO DATABASE
CREATE DATABASE CinemaManagement;
GO

USE CinemaManagement;
GO

/* ===============================
   3. Cấu trúc Bảng
================================ */

-- Roles
CREATE TABLE Roles (
    RoleId   INT IDENTITY PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);

-- Users
CREATE TABLE Users (
    UserId       INT IDENTITY PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName     NVARCHAR(100),
    Email        NVARCHAR(100),
    RoleId       INT           NOT NULL,
    Status       NVARCHAR(20)  DEFAULT N'Active',
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

-- Genres
CREATE TABLE Genres (
    GenreId   INT IDENTITY PRIMARY KEY,
    GenreName NVARCHAR(100) NOT NULL UNIQUE
);

-- Movies
CREATE TABLE Movies (
    MovieId     INT IDENTITY PRIMARY KEY,
    Title       NVARCHAR(200) NOT NULL,
    Duration    INT           NOT NULL,
    Description NVARCHAR(MAX),
    ReleaseDate DATE,
    PosterUrl   NVARCHAR(500),
    BannerUrl   NVARCHAR(500),
    Status      NVARCHAR(50)  DEFAULT N'Now Showing'
);

-- MovieGenres (Junction table)
CREATE TABLE MovieGenres (
    MovieId INT NOT NULL,
    GenreId INT NOT NULL,
    CONSTRAINT PK_MovieGenres PRIMARY KEY (MovieId, GenreId),
    CONSTRAINT FK_MovieGenres_Movies FOREIGN KEY (MovieId) REFERENCES Movies(MovieId) ON DELETE CASCADE,
    CONSTRAINT FK_MovieGenres_Genres FOREIGN KEY (GenreId) REFERENCES Genres(GenreId) ON DELETE CASCADE
);

-- Theaters
CREATE TABLE Theaters (
    TheaterId   INT IDENTITY PRIMARY KEY,
    Name        NVARCHAR(150) NOT NULL,
    Address     NVARCHAR(255)
);

-- Rooms
CREATE TABLE Rooms (
    RoomId    INT IDENTITY PRIMARY KEY,
    TheaterId INT          NOT NULL,
    RoomName  NVARCHAR(50),
    SeatCount INT          NOT NULL,
    CONSTRAINT FK_Rooms_Theaters FOREIGN KEY (TheaterId) REFERENCES Theaters(TheaterId) ON DELETE CASCADE
);

-- Seats
CREATE TABLE Seats (
    SeatId     INT IDENTITY PRIMARY KEY,
    SeatNumber NVARCHAR(10)  NOT NULL,
    SeatType   NVARCHAR(20)  NOT NULL DEFAULT N'Standard',
    RoomId     INT           NOT NULL,
    CONSTRAINT FK_Seats_Rooms FOREIGN KEY (RoomId) REFERENCES Rooms(RoomId) ON DELETE CASCADE
);

-- Showtimes
CREATE TABLE Showtimes (
    ShowtimeId INT IDENTITY PRIMARY KEY,
    MovieId    INT            NOT NULL,
    RoomId     INT            NOT NULL,
    StartTime  DATETIME       NOT NULL,
    Price      DECIMAL(18,2)  NOT NULL,
    CONSTRAINT FK_Showtimes_Movies FOREIGN KEY (MovieId) REFERENCES Movies(MovieId) ON DELETE CASCADE,
    CONSTRAINT FK_Showtimes_Rooms  FOREIGN KEY (RoomId)  REFERENCES Rooms(RoomId) ON DELETE CASCADE
);

-- Tickets
CREATE TABLE Tickets (
    TicketId    INT IDENTITY PRIMARY KEY,
    ShowtimeId  INT           NOT NULL,
    SeatId      INT           NULL,
    UserId      INT           NOT NULL,
    BookingTime DATETIME      DEFAULT GETDATE(),
    Status      NVARCHAR(50)  DEFAULT N'Booked',
    CONSTRAINT FK_Tickets_Showtimes FOREIGN KEY (ShowtimeId) REFERENCES Showtimes(ShowtimeId),
    CONSTRAINT FK_Tickets_Seats     FOREIGN KEY (SeatId)     REFERENCES Seats(SeatId),
    CONSTRAINT FK_Tickets_Users     FOREIGN KEY (UserId)     REFERENCES Users(UserId)
);
GO

-- Lọc Unique Index cho phép đặt lại ghế đã huỷ
CREATE UNIQUE INDEX UQ_Ticket_Showtime_Seat ON Tickets(ShowtimeId, SeatId) WHERE Status = N'Booked';
GO

-- Payments
CREATE TABLE Payments (
    PaymentId INT IDENTITY PRIMARY KEY,
    Amount    DECIMAL(18,2)  NOT NULL DEFAULT 0,
    Method    NVARCHAR(50)   NOT NULL DEFAULT N'Cash',
    Status    NVARCHAR(50)   NOT NULL DEFAULT N'Pending',
    PaidAt    DATETIME       NOT NULL DEFAULT GETDATE(),
    TicketId  INT            NOT NULL UNIQUE,
    CONSTRAINT FK_Payments_Tickets FOREIGN KEY (TicketId) REFERENCES Tickets(TicketId) ON DELETE CASCADE
);
GO

/* ===============================
   4. Dữ liệu Mẫu (Seed Data)
================================ */

-- Roles
INSERT INTO Roles (RoleName) VALUES (N'Admin'), (N'Staff'), (N'Customer');

-- Admin User (Pass: Admin@123 - BCrypt Hash)
INSERT INTO Users (Username, PasswordHash, FullName, Email, RoleId, Status)
VALUES ('admin', '$2a$11$V.Co51b1VTU8zRBPdCcMUeYFNuCBgiTGoyngz5kPfWe3iyFC1AcKq', N'Nguyễn Đức Mạnh', 'necma2005@gmail.com', 1, 'Active');

-- Genres
INSERT INTO Genres (GenreName)
VALUES (N'Hành động'), (N'Hài hước'), (N'Tâm lý'), (N'Kinh dị'), (N'Khoa học viễn tưởng'), (N'Hoạt hình');

-- Movies (Local Images)
INSERT INTO Movies (Title, Duration, Description, ReleaseDate, PosterUrl, BannerUrl, Status)
VALUES 
(N'Kung Fu Panda 4', 94, N'Gấu trúc Po trở lại...', '2024-03-08', '/img/movies/kungfupanda4.jpg', '/img/movies/kungfupanda4-banner.jpg', 'Now Showing'),
(N'Dune: Phần 2', 166, N'Hành trình của Paul Atreides...', '2024-03-01', '/img/movies/Dune.jpg', '/img/movies/DUNE-banner.jpg', 'Now Showing'),
(N'Mai', 131, N'Phim của Trấn Thành...', '2024-02-10', '/img/movies/mai.jpg', '/img/movies/mai-banner.jpg', 'Now Showing'),
(N'Quật Mộ Trùng Ma', 134, N'Phim kinh dị Hàn Quốc...', '2024-03-15', '/img/movies/quatmotrungma.jpg', NULL, 'Now Showing'),
(N'Godzilla x Kong', 115, N'Đế chế mới...', '2024-03-29', '/img/movies/godzillaxkong.jpg', NULL, 'Coming Soon');

-- MovieGenres
INSERT INTO MovieGenres (MovieId, GenreId) VALUES (1, 6), (2, 5), (3, 3), (4, 4);

-- Theater
INSERT INTO Theaters (Name, Address) VALUES 
(N'Cinema Plaza', N'123 Nguyễn Huệ, Q1, TP.HCM'),
(N'Lotte Cinema Hà Đông', N'110 Trần Phú, Hà Đông, Hà Nội'),
(N'CGV Vincom Đà Nẵng', N'910A Ngô Quyền, Sơn Trà, Đà Nẵng'),
(N'Galaxy Cinema Cần Thơ', N'Đại lộ Hoà Bình, Ninh Kiều, Cần Thơ'),
(N'BHD Star Huế', N'Vincom Plaza, Phú Nhuận, Huế');

-- Rooms
INSERT INTO Rooms (TheaterId, RoomName, SeatCount) VALUES 
-- Cinema Plaza (2 phòng cũ)
(1, N'Phòng 1', 30), (1, N'Phòng 2', 20),
-- Lotte Hà Đông (5 phòng)
(2, N'Phòng 1', 40), (2, N'Phòng 2', 40), (2, N'Phòng 3', 30), (2, N'Phòng 4', 30), (2, N'Phòng 5', 50),
-- CGV Đà Nẵng (6 phòng)
(3, N'Phòng 1', 50), (3, N'Phòng 2', 40), (3, N'Phòng 3', 40), (3, N'Phòng 4', 30), (3, N'Phòng 5', 30), (3, N'Phòng 6', 30),
-- Galaxy Cần Thơ (5 phòng)
(4, N'Phòng 1', 40), (4, N'Phòng 2', 40), (4, N'Phòng 3', 30), (4, N'Phòng 4', 30), (4, N'Phòng 5', 20),
-- BHD Star Huế (7 phòng)
(5, N'Phòng 1', 50), (5, N'Phòng 2', 40), (5, N'Phòng 3', 40), (5, N'Phòng 4', 30), (5, N'Phòng 5', 30), (5, N'Phòng 6', 30), (5, N'Phòng 7', 20);

-- Tạo ghế tự động cho TẤT CẢ các phòng chiếu (từ RoomId = 1 đến 25)
DECLARE @roomId INT = 1;
DECLARE @seatCount INT;
DECLARE @maxRow CHAR(1);
DECLARE @maxCol INT;
DECLARE @row CHAR(1);
DECLARE @col INT;

WHILE @roomId <= 25
BEGIN
    SELECT @seatCount = SeatCount FROM Rooms WHERE RoomId = @roomId;
    
    -- Cấu trúc ghế theo SeatCount:
    IF @seatCount = 50 BEGIN SET @maxRow = 'E'; SET @maxCol = 10; END
    ELSE IF @seatCount = 40 BEGIN SET @maxRow = 'E'; SET @maxCol = 8; END
    ELSE IF @seatCount = 30 BEGIN SET @maxRow = 'E'; SET @maxCol = 6; END
    ELSE BEGIN SET @maxRow = 'D'; SET @maxCol = 5; END

    SET @row = 'A';
    WHILE ASCII(@row) <= ASCII(@maxRow)
    BEGIN
        SET @col = 1;
        WHILE @col <= @maxCol
        BEGIN
            INSERT INTO Seats (SeatNumber, SeatType, RoomId) 
            VALUES (
                @row + CAST(@col AS VARCHAR), 
                CASE WHEN ASCII(@row) >= ASCII(@maxRow) - 1 THEN 'VIP' ELSE 'Standard' END, 
                @roomId
            );
            SET @col = @col + 1;
        END
        SET @row = CHAR(ASCII(@row) + 1);
    END
    
    SET @roomId = @roomId + 1;
END

-- Showtimes mẫu
INSERT INTO Showtimes (MovieId, RoomId, StartTime, Price)
VALUES 
(1, 1, DATEADD(HOUR, 14, CAST(CAST(GETDATE() AS DATE) AS DATETIME)), 85000),
(2, 2, DATEADD(HOUR, 10, CAST(CAST(GETDATE() AS DATE) AS DATETIME)), 105000),
(2, 2, DATEADD(HOUR, 19, CAST(CAST(GETDATE() AS DATE) AS DATETIME)), 105000),
(3, 1, DATEADD(HOUR, 21, CAST(CAST(GETDATE() AS DATE) AS DATETIME)), 90000);

GO
PRINT N'=== DATABASE CINEMAMANAGEMENT KHỞI TẠO HOÀN TẤT ===';
