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

-- =======================================================
-- INDEXES CHO TỐI ƯU HÓA TRUY VẤN
-- =======================================================
CREATE INDEX IX_Showtimes_MovieId ON Showtimes(MovieId);
CREATE INDEX IX_Showtimes_RoomId ON Showtimes(RoomId);
CREATE INDEX IX_Showtimes_StartTime ON Showtimes(StartTime);
CREATE INDEX IX_Rooms_TheaterId ON Rooms(TheaterId);
CREATE INDEX IX_Seats_RoomId ON Seats(RoomId);
CREATE INDEX IX_Tickets_ShowtimeId ON Tickets(ShowtimeId);
CREATE INDEX IX_Tickets_UserId ON Tickets(UserId);
CREATE INDEX IX_Tickets_SeatId ON Tickets(SeatId);
CREATE INDEX IX_Tickets_BookingTime ON Tickets(BookingTime);
CREATE INDEX IX_Payments_TicketId ON Payments(TicketId);
CREATE INDEX IX_Payments_PaidAt ON Payments(PaidAt);
CREATE INDEX IX_Users_RoleId ON Users(RoleId);
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
(N'Kung Fu Panda 4', 94, N'Sau khi được chọn làm Thủ Lĩnh Tinh Thần của Thung Lũng Bình Yên, Po cần tìm và huấn luyện một chiến binh Rồng mới, trong khi một kẻ thù độc ác mới là Tắc Kè Bông Chameleon đang nhăm nhe triệu hồi tất cả những kẻ phản diện từ quá khứ.', '2024-03-08', '/img/movies/kungfupanda4.jpg', '/img/movies/kungfupanda4-banner.jpg', 'Now Showing'),
(N'Dune: Phần 2', 166, N'Paul Atreides hội ngộ cùng Chani và người Fremen khi anh tìm cách trả thù những kẻ đã hủy diệt gia đình mình. Đối mặt với sự lựa chọn giữa tình yêu của đời mình và số phận của vũ trụ, Paul cố gắng ngăn chặn một tương lai khủng khiếp mà chỉ anh mới có thể thấy trước.', '2024-03-01', '/img/movies/dune2.jpg', '/img/movies/dune2-banner.jpg', 'Now Showing'),
(N'Mai', 131, N'Mai là câu chuyện tình cảm nhẹ nhàng nhưng chứa đựng nhiều góc khuất tâm lý sâu sắc xoay quanh cuộc đời của một người phụ nữ tên Mai, người luôn nỗ lực vượt lên số phận và định kiến xã hội để tìm kiếm hạnh phúc đích thực.', '2024-02-10', '/img/movies/mai.jpg', '/img/movies/mai-banner.jpg', 'Now Showing'),
(N'Quật Mộ Trùng Ma', 134, N'Hai pháp sư, một thầy phong thủy và một người chôn cất cùng nhau hợp tác để khai quật một ngôi mộ bí ẩn của một gia đình giàu có ở Mỹ, vô tình giải phóng một thế lực tà ác đáng sợ ẩn giấu bên dưới.', '2024-03-15', '/img/movies/quatmotrungma.jpg', '/img/movies/quatmotrungma-banner.jpg', 'Now Showing'),
(N'Godzilla x Kong', 115, N'Godzilla và Kong phải gạt bỏ những bất hòa xưa cũ để cùng nhau hợp tác chống lại một mối đe dọa khổng lồ mới từ bên trong Trái Đất Rỗng, đe dọa sự tồn vong của cả hai loài và toàn nhân loại.', '2024-03-29', '/img/movies/godzillaxkong.jpg', '/img/movies/godzillaxkong-banner.jpg', 'Coming Soon'),
(N'Avatar', 162, N'Một cựu thủy quân lục chiến bị liệt được phái đến hành tinh Pandora để thực hiện một nhiệm vụ đặc biệt, nhưng anh đã yêu một cô gái bản địa người Na''vi và phải chiến đấu để bảo vệ hành tinh quê hương của cô.', '2009-12-18', '/img/movies/avatar.jpg', '/img/movies/avatar-banner.jpg', 'Now Showing'),
(N'Deadpool 2', 119, N'Deadpool phải thành lập một nhóm dị nhân mang tên X-Force để bảo vệ một cậu bé dị nhân trẻ tuổi khỏi Cable, một người lính đi xuyên thời gian.', '2018-05-18', '/img/movies/deadpool2.jpg', '/img/movies/deadpool2-banner.jpg', 'Now Showing'),
(N'Fast & Furious 10', 141, N'Dom Toretto và gia đình của mình phải đối mặt với đối thủ nguy hiểm nhất từ trước đến nay: một kẻ thù đầy thù hận từ quá khứ muốn phá hủy tất cả những gì Dom yêu quý.', '2023-05-19', '/img/movies/fastx.jpg', '/img/movies/fastx-banner.jpg', 'Now Showing'),
(N'Biệt Đội Săn Ma: Kỷ Nguyên Băng Giá', 115, N'Gia đình Spengler quay trở lại nơi mọi thứ bắt đầu - trạm cứu hỏa thành phố New York mang tính biểu tượng - để lập nhóm với các Ghostbusters ban đầu, những người đã phát triển một phòng thí nghiệm nghiên cứu tối mật.', '2024-03-22', '/img/movies/ghostbusters.jpg', '/img/movies/ghostbusters-banner.jpg', 'Now Showing'),
(N'Võ Sĩ Giác Đấu 2', 148, N'Nhiều năm sau khi chứng kiến cái chết của Maximus dưới tay người chú của mình, Lucius buộc phải bước vào Đấu trường La Mã sau khi quê hương anh bị chinh phục bởi những vị hoàng đế tàn bạo.', '2024-11-22', '/img/movies/gladiator2.jpg', '/img/movies/gladiator2-banner.jpg', 'Now Showing'),
(N'Sát Thủ John Wick: Phần 4', 169, N'John Wick tìm ra con đường đánh bại High Table. Nhưng trước khi có thể kiếm được tự do, Wick phải đối mặt với một kẻ thù mới có liên minh hùng mạnh trên toàn cầu và những thế lực biến bạn cũ thành kẻ thù.', '2023-03-24', '/img/movies/johnwick4.jpg', '/img/movies/johnwick4-banner.jpg', 'Now Showing'),
(N'Mufasa: Vua Sư Tử', 120, N'Rafiki kể câu chuyện về huyền thoại Mufasa cho cô sư tử con Kiara, con gái của Simba và Nala, với Timon và Pumbaa đóng vai trò kể chuyện hài hước.', '2024-12-20', '/img/movies/mufasa.jpg', '/img/movies/mufasa-banner.jpg', 'Now Showing'),
(N'Vùng Đất Câm Lặng: Ngày Một', 100, N'Trải nghiệm ngày thế giới rơi vào tĩnh lặng trong phần tiền truyện này của loạt phim kinh dị sinh tồn ăn khách.', '2024-06-28', '/img/movies/quietplace.jpg', '/img/movies/quietplace-banner.jpg', 'Now Showing'),
(N'Sonic the Hedgehog 3', 110, N'Sonic, Knuckles và Tails tái hợp chống lại một đối thủ mới mạnh mẽ, Shadow, kẻ thù bí ẩn với sức mạnh vượt trội.', '2024-12-20', '/img/movies/sonic3.jpg', '/img/movies/sonic3-banner.jpg', 'Now Showing'),
(N'Người Nhện: Du Hành Vũ Trụ Nhện', 140, N'Miles Morales tái hợp với Gwen Stacy để thực hiện một cuộc phiêu lưu xuyên qua Đa vũ trụ, nơi anh gặp một nhóm Người Nhện chịu trách nhiệm bảo vệ sự tồn tại của nó.', '2023-06-02', '/img/movies/spiderman.jpg', '/img/movies/spiderman-banner.jpg', 'Now Showing'),
(N'Titanic', 194, N'Câu chuyện tình yêu đầy bi kịch giữa chàng họa sĩ nghèo Jack Dawson và tiểu thư quý tộc Rose DeWitt Bukater trên con tàu Titanic định mệnh.', '1997-12-19', '/img/movies/titanic.jpg', '/img/movies/titanic-banner.jpg', 'Now Showing'),
(N'Chiến Binh Báo Đen: Wakanda Bất Diệt', 161, N'Nữ hoàng Ramonda, Shuri, M''Baku, Okoye và Dora Milaje chiến đấu để bảo vệ quốc gia của họ khỏi sự can thiệp của các cường quốc thế giới sau cái chết của Vua T''Challa.', '2022-11-11', '/img/movies/wakanda.jpg', '/img/movies/wakanda-banner.jpg', 'Now Showing');

-- MovieGenres
INSERT INTO MovieGenres (MovieId, GenreId) VALUES 
(1, 6), (1, 2), -- Kung Fu Panda 4: Hoạt hình, Hài hước
(2, 5), (2, 1), -- Dune: Phần 2: Khoa học viễn tưởng, Hành động
(3, 3),         -- Mai: Tâm lý
(4, 4),         -- Quật Mộ Trùng Ma: Kinh dị
(5, 1), (5, 5), -- Godzilla x Kong: Hành động, Khoa học viễn tưởng
(6, 1), (6, 5), -- Avatar: Hành động, Khoa học viễn tưởng
(7, 1), (7, 2), -- Deadpool 2: Hành động, Hài hước
(8, 1),         -- Fast & Furious 10: Hành động
(9, 1), (9, 2), (9, 5), -- Ghostbusters: Hành động, Hài hước, Khoa học viễn tưởng
(10, 1), (10, 3), -- Gladiator 2: Hành động, Tâm lý
(11, 1),        -- John Wick 4: Hành động
(12, 1), (12, 3), (12, 6), -- Mufasa: Hành động, Tâm lý, Hoạt hình
(13, 4), (13, 5), -- Quiet Place: Kinh dị, Khoa học viễn tưởng
(14, 1), (14, 6), -- Sonic 3: Hành động, Hoạt hình
(15, 1), (15, 6), (15, 5), -- Spider-Man: Hành động, Hoạt hình, Khoa học viễn tưởng
(16, 3),        -- Titanic: Tâm lý
(17, 1), (17, 5); -- Wakanda: Hành động, Khoa học viễn tưởng

-- Theater
INSERT INTO Theaters (Name, Address) VALUES 
(N'CinemaHub Nguyễn Huệ', N'123 Nguyễn Huệ, Q1, TP.HCM'),
(N'CinemaHub Hà Đông', N'110 Trần Phú, Hà Đông, Hà Nội'),
(N'CinemaHub Đà Nẵng', N'910A Ngô Quyền, Sơn Trà, Đà Nẵng'),
(N'CinemaHub Cần Thơ', N'Đại lộ Hoà Bình, Ninh Kiều, Cần Thơ'),
(N'CinemaHub Huế', N'Vincom Plaza, Phú Nhuận, Huế');

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

/* ===============================
   Sinh suất chiếu cho TẤT CẢ phim "Now Showing" trên toàn bộ 25 phòng
   trực thuộc hệ thống rạp từ ngày 21/05/2026 → 31/05/2026
   6 khung giờ/ngày (09:00, 11:30, 14:00, 16:30, 19:00, 21:30)
   Xoay vòng suất chiếu linh động giữa tất cả các phim đang chiếu
================================ */
GO

-- Lấy danh sách ID các phim đang chiếu để làm danh sách xoay vòng
DECLARE @NowShowingMovies TABLE (Seq INT IDENTITY(1,1), MovieId INT);
INSERT INTO @NowShowingMovies (MovieId)
SELECT MovieId FROM Movies WHERE Status = 'Now Showing' ORDER BY MovieId;

DECLARE @MovieCount INT;
SELECT @MovieCount = COUNT(*) FROM @NowShowingMovies;

-- Sinh suất chiếu
;WITH DateRange AS (
    SELECT CAST('2026-05-21' AS DATE) AS d
    UNION ALL
    SELECT DATEADD(DAY, 1, d) FROM DateRange WHERE d < '2026-05-31'
),
Slots AS (
    SELECT 1 AS slot, 9  AS h, 0  AS mn,  75000 AS price UNION ALL  -- Sáng sớm
    SELECT 2,        11,      30,         85000           UNION ALL  -- Trưa
    SELECT 3,        14,      0,          90000           UNION ALL  -- Đầu giờ chiều
    SELECT 4,        16,      30,         95000           UNION ALL  -- Cuối giờ chiều
    SELECT 5,        19,      0,         110000           UNION ALL  -- Tối (Prime time)
    SELECT 6,        21,      30,        105000                       -- Đêm
),
GeneratedShowtimes AS (
    SELECT
        -- Đổi số xoay vòng từ phép chia lấy dư dựa trên phòng, ngày, slot sang ID phim thực tế
        (((r.RoomId + DAY(d.d) + s.slot) % @MovieCount) + 1) AS MovieSeq,
        r.RoomId,
        DATEADD(MINUTE, s.mn, DATEADD(HOUR, s.h, CAST(d.d AS DATETIME))) AS StartTime,
        s.price
    FROM DateRange d
    CROSS JOIN (SELECT RoomId FROM Rooms) r
    CROSS JOIN Slots s
)
INSERT INTO Showtimes (MovieId, RoomId, StartTime, Price)
SELECT 
    m.MovieId,
    g.RoomId,
    g.StartTime,
    g.Price
FROM GeneratedShowtimes g
INNER JOIN @NowShowingMovies m ON g.MovieSeq = m.Seq
OPTION (MAXRECURSION 100);

GO
PRINT N'=== DATABASE CINEMAMANAGEMENT KHỞI TẠO HOÀN TẤT ===';
