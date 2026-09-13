-- =======================================================
-- CINEMA MANAGEMENT — FULL DATABASE SCRIPT (POSTGRESQL)
-- =======================================================

-- 1. DROP EXISTING TABLES
DROP TABLE IF EXISTS "Payments" CASCADE;
DROP TABLE IF EXISTS "Tickets" CASCADE;
DROP TABLE IF EXISTS "Showtimes" CASCADE;
DROP TABLE IF EXISTS "Seats" CASCADE;
DROP TABLE IF EXISTS "Rooms" CASCADE;
DROP TABLE IF EXISTS "Theaters" CASCADE;
DROP TABLE IF EXISTS "MovieGenres" CASCADE;
DROP TABLE IF EXISTS "Movies" CASCADE;
DROP TABLE IF EXISTS "Genres" CASCADE;
DROP TABLE IF EXISTS "Users" CASCADE;
DROP TABLE IF EXISTS "Roles" CASCADE;

-- 2. CREATE TABLES

-- Roles
CREATE TABLE "Roles" (
    "RoleId"   SERIAL PRIMARY KEY,
    "RoleName" VARCHAR(50) NOT NULL UNIQUE
);

-- Users
CREATE TABLE "Users" (
    "UserId"       SERIAL PRIMARY KEY,
    "Username"     VARCHAR(50)  NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "FullName"     VARCHAR(100),
    "Email"        VARCHAR(100),
    "RoleId"       INT          NOT NULL,
    "Status"       VARCHAR(20)  DEFAULT 'Active',
    CONSTRAINT "FK_Users_Roles" FOREIGN KEY ("RoleId") REFERENCES "Roles"("RoleId")
);

-- Genres
CREATE TABLE "Genres" (
    "GenreId"   SERIAL PRIMARY KEY,
    "GenreName" VARCHAR(100) NOT NULL UNIQUE
);

-- Movies
CREATE TABLE "Movies" (
    "MovieId"     SERIAL PRIMARY KEY,
    "Title"       VARCHAR(200) NOT NULL,
    "Duration"    INT          NOT NULL,
    "Description" TEXT,
    "ReleaseDate" DATE,
    "PosterUrl"   VARCHAR(500),
    "BannerUrl"   VARCHAR(500),
    "Status"      VARCHAR(50)  DEFAULT 'Now Showing'
);

-- MovieGenres
CREATE TABLE "MovieGenres" (
    "MovieId" INT NOT NULL,
    "GenreId" INT NOT NULL,
    CONSTRAINT "PK_MovieGenres" PRIMARY KEY ("MovieId", "GenreId"),
    CONSTRAINT "FK_MovieGenres_Movies" FOREIGN KEY ("MovieId") REFERENCES "Movies"("MovieId") ON DELETE CASCADE,
    CONSTRAINT "FK_MovieGenres_Genres" FOREIGN KEY ("GenreId") REFERENCES "Genres"("GenreId") ON DELETE CASCADE
);

-- Theaters
CREATE TABLE "Theaters" (
    "TheaterId" SERIAL PRIMARY KEY,
    "Name"      VARCHAR(150) NOT NULL,
    "Address"   VARCHAR(255)
);

-- Rooms
CREATE TABLE "Rooms" (
    "RoomId"    SERIAL PRIMARY KEY,
    "TheaterId" INT         NOT NULL,
    "RoomName"  VARCHAR(50),
    "SeatCount" INT         NOT NULL,
    CONSTRAINT "FK_Rooms_Theaters" FOREIGN KEY ("TheaterId") REFERENCES "Theaters"("TheaterId") ON DELETE CASCADE
);

-- Seats
CREATE TABLE "Seats" (
    "SeatId"     SERIAL PRIMARY KEY,
    "SeatNumber" VARCHAR(10) NOT NULL,
    "SeatType"   VARCHAR(20) NOT NULL DEFAULT 'Standard',
    "RoomId"     INT         NOT NULL,
    CONSTRAINT "FK_Seats_Rooms" FOREIGN KEY ("RoomId") REFERENCES "Rooms"("RoomId") ON DELETE CASCADE
);

-- Showtimes
CREATE TABLE "Showtimes" (
    "ShowtimeId" SERIAL PRIMARY KEY,
    "MovieId"    INT           NOT NULL,
    "RoomId"     INT           NOT NULL,
    "StartTime"  TIMESTAMP     NOT NULL,
    "Price"      DECIMAL(18,2) NOT NULL,
    CONSTRAINT "FK_Showtimes_Movies" FOREIGN KEY ("MovieId") REFERENCES "Movies"("MovieId") ON DELETE CASCADE,
    CONSTRAINT "FK_Showtimes_Rooms"  FOREIGN KEY ("RoomId")  REFERENCES "Rooms"("RoomId") ON DELETE CASCADE
);

-- Tickets
CREATE TABLE "Tickets" (
    "TicketId"    SERIAL PRIMARY KEY,
    "ShowtimeId"  INT         NOT NULL,
    "SeatId"      INT         NULL,
    "UserId"      INT         NOT NULL,
    "BookingTime" TIMESTAMP   DEFAULT CURRENT_TIMESTAMP,
    "Status"      VARCHAR(50) DEFAULT 'Booked',
    CONSTRAINT "FK_Tickets_Showtimes" FOREIGN KEY ("ShowtimeId") REFERENCES "Showtimes"("ShowtimeId"),
    CONSTRAINT "FK_Tickets_Seats"     FOREIGN KEY ("SeatId")     REFERENCES "Seats"("SeatId"),
    CONSTRAINT "FK_Tickets_Users"     FOREIGN KEY ("UserId")     REFERENCES "Users"("UserId")
);

-- Filtered Unique Index
CREATE UNIQUE INDEX "UQ_Ticket_Showtime_Seat" ON "Tickets"("ShowtimeId", "SeatId") WHERE "Status" = 'Booked';

-- Payments
CREATE TABLE "Payments" (
    "PaymentId" SERIAL PRIMARY KEY,
    "Amount"    DECIMAL(18,2) NOT NULL DEFAULT 0,
    "Method"    VARCHAR(50)   NOT NULL DEFAULT 'Cash',
    "Status"    VARCHAR(50)   NOT NULL DEFAULT 'Pending',
    "PaidAt"    TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "TicketId"  INT           NOT NULL UNIQUE,
    CONSTRAINT "FK_Payments_Tickets" FOREIGN KEY ("TicketId") REFERENCES "Tickets"("TicketId") ON DELETE CASCADE
);

-- Indexes
CREATE INDEX "IX_Showtimes_MovieId" ON "Showtimes"("MovieId");
CREATE INDEX "IX_Showtimes_RoomId" ON "Showtimes"("RoomId");
CREATE INDEX "IX_Showtimes_StartTime" ON "Showtimes"("StartTime");
CREATE INDEX "IX_Rooms_TheaterId" ON "Rooms"("TheaterId");
CREATE INDEX "IX_Seats_RoomId" ON "Seats"("RoomId");
CREATE INDEX "IX_Tickets_ShowtimeId" ON "Tickets"("ShowtimeId");
CREATE INDEX "IX_Tickets_UserId" ON "Tickets"("UserId");
CREATE INDEX "IX_Tickets_SeatId" ON "Tickets"("SeatId");
CREATE INDEX "IX_Tickets_BookingTime" ON "Tickets"("BookingTime");
CREATE INDEX "IX_Payments_TicketId" ON "Payments"("TicketId");
CREATE INDEX "IX_Payments_PaidAt" ON "Payments"("PaidAt");
CREATE INDEX "IX_Users_RoleId" ON "Users"("RoleId");
CREATE UNIQUE INDEX "IX_User_Username" ON "Users"("Username");
CREATE INDEX "IX_User_Email" ON "Users"("Email");
CREATE INDEX "IX_Showtime_MovieId_StartTime" ON "Showtimes"("MovieId", "StartTime");
CREATE INDEX "IX_Ticket_UserId_BookingTime" ON "Tickets"("UserId", "BookingTime");

-- =======================================================
-- 3. SEED DATA
-- =======================================================

-- Roles
INSERT INTO "Roles" ("RoleName") VALUES ('Admin'), ('Staff'), ('Customer');

-- Admin User (Pass: Admin@123)
INSERT INTO "Users" ("Username", "PasswordHash", "FullName", "Email", "RoleId", "Status")
VALUES ('admin', '$2a$11$V.Co51b1VTU8zRBPdCcMUeYFNuCBgiTGoyngz5kPfWe3iyFC1AcKq', 'Nguyễn Đức Mạnh', 'necma2005@gmail.com', 1, 'Active');

-- Genres
INSERT INTO "Genres" ("GenreName")
VALUES ('Hành động'), ('Hài hước'), ('Tâm lý'), ('Kinh dị'), ('Khoa học viễn tưởng'), ('Hoạt hình');

-- Movies
INSERT INTO "Movies" ("Title", "Duration", "Description", "ReleaseDate", "PosterUrl", "BannerUrl", "Status")
VALUES 
('Kung Fu Panda 4', 94, 'Sau khi được chọn làm Thủ Lĩnh Tinh Thần của Thung Lũng Bình Yên, Po cần tìm và huấn luyện một chiến binh Rồng mới, trong khi một kẻ thù độc ác mới là Tắc Kè Bông Chameleon đang nhăm nhe triệu hồi tất cả những kẻ phản diện từ quá khứ.', '2024-03-08', '/img/movies/kungfupanda4.jpg', '/img/movies/kungfupanda4-banner.jpg', 'Now Showing'),
('Dune: Phần 2', 166, 'Paul Atreides hội ngộ cùng Chani và người Fremen khi anh tìm cách trả thù những kẻ đã hủy diệt gia đình mình. Đối mặt với sự lựa chọn giữa tình yêu của đời mình và số phận của vũ trụ, Paul cố gắng ngăn chặn một tương lai khủng khiếp mà chỉ anh mới có thể thấy trước.', '2024-03-01', '/img/movies/dune2.jpg', '/img/movies/dune2-banner.jpg', 'Now Showing'),
('Mai', 131, 'Mai là câu chuyện tình cảm nhẹ nhàng nhưng chứa đựng nhiều góc khuất tâm lý sâu sắc xoay quanh cuộc đời của một người phụ nữ tên Mai, người luôn nỗ lực vượt lên số phận và định kiến xã hội để tìm kiếm hạnh phúc đích thực.', '2024-02-10', '/img/movies/mai.jpg', '/img/movies/mai-banner.jpg', 'Now Showing'),
('Quật Mộ Trùng Ma', 134, 'Hai pháp sư, một thầy phong thủy và một người chôn cất cùng nhau hợp tác để khai quật một ngôi mộ bí ẩn của một gia đình giàu có ở Mỹ, vô tình giải phóng một thế lực tà ác đáng sợ ẩn giấu bên dưới.', '2024-03-15', '/img/movies/quatmotrungma.jpg', '/img/movies/quatmotrungma-banner.jpg', 'Now Showing'),
('Godzilla x Kong', 115, 'Godzilla và Kong phải gạt bỏ những bất hòa xưa cũ để cùng nhau hợp tác chống lại một mối đe dọa khổng lồ mới từ bên trong Trái Đất Rỗng, đe dọa sự tồn vong của cả hai loài và toàn nhân loại.', '2024-03-29', '/img/movies/godzillaxkong.jpg', '/img/movies/godzillaxkong-banner.jpg', 'Coming Soon'),
('Avatar', 162, 'Một cựu thủy quân lục chiến bị liệt được phái đến hành tinh Pandora để thực hiện một nhiệm vụ đặc biệt, nhưng anh đã yêu một cô gái bản địa người Na''vi và phải chiến đấu để bảo vệ hành tinh quê hương của cô.', '2009-12-18', '/img/movies/avatar.jpg', '/img/movies/avatar-banner.jpg', 'Now Showing'),
('Deadpool 2', 119, 'Deadpool phải thành lập một nhóm dị nhân mang tên X-Force để bảo vệ một cậu bé dị nhân trẻ tuổi khỏi Cable, một người lính đi xuyên thời gian.', '2018-05-18', '/img/movies/deadpool2.jpg', '/img/movies/deadpool2-banner.jpg', 'Now Showing'),
('Fast & Furious 10', 141, 'Dom Toretto và gia đình của mình phải đối mặt với đối thủ nguy hiểm nhất từ trước đến nay: một kẻ thù đầy thù hận từ quá khứ muốn phá hủy tất cả những gì Dom yêu quý.', '2023-05-19', '/img/movies/fastx.jpg', '/img/movies/fastx-banner.jpg', 'Now Showing'),
('Biệt Đội Săn Ma: Kỷ Nguyên Băng Giá', 115, 'Gia đình Spengler quay trở lại nơi mọi thứ bắt đầu - trạm cứu hỏa thành phố New York mang tính biểu tượng - để lập nhóm với các Ghostbusters ban đầu, những người đã phát triển một phòng thí nghiệm nghiên cứu tối mật.', '2024-03-22', '/img/movies/ghostbusters.jpg', '/img/movies/ghostbusters-banner.jpg', 'Now Showing'),
('Võ Sĩ Giác Đấu 2', 148, 'Nhiều năm sau khi chứng kiến cái chết của Maximus dưới tay người chú của mình, Lucius buộc phải bước vào Đấu trường La Mã sau khi quê hương anh bị chinh phục bởi những vị hoàng đế tàn bạo.', '2024-11-22', '/img/movies/gladiator2.jpg', '/img/movies/gladiator2-banner.jpg', 'Now Showing'),
('Sát Thủ John Wick: Phần 4', 169, 'John Wick tìm ra con đường đánh bại High Table. Nhưng trước khi có thể kiếm được tự do, Wick phải đối mặt với một kẻ thù mới có liên minh hùng mạnh trên toàn cầu và những thế lực biến bạn cũ thành kẻ thù.', '2023-03-24', '/img/movies/johnwick4.jpg', '/img/movies/johnwick4-banner.jpg', 'Now Showing'),
('Mufasa: Vua Sư Tử', 120, 'Rafiki kể câu chuyện về huyền thoại Mufasa cho cô sư tử con Kiara, con gái của Simba và Nala, với Timon và Pumbaa đóng vai trò kể chuyện hài hước.', '2024-12-20', '/img/movies/mufasa.jpg', '/img/movies/mufasa-banner.jpg', 'Now Showing'),
('Vùng Đất Câm Lặng: Ngày Một', 100, 'Trải nghiệm ngày thế giới rơi vào tĩnh lặng trong phần tiền truyện này của loạt phim kinh dị sinh tồn ăn khách.', '2024-06-28', '/img/movies/quietplace.jpg', '/img/movies/quietplace-banner.jpg', 'Now Showing'),
('Sonic the Hedgehog 3', 110, 'Sonic, Knuckles và Tails tái hợp chống lại một đối thủ mới mạnh mẽ, Shadow, kẻ thù bí ẩn với sức mạnh vượt trội.', '2024-12-20', '/img/movies/sonic3.jpg', '/img/movies/sonic3-banner.jpg', 'Now Showing'),
('Người Nhện: Du Hành Vũ Trụ Nhện', 140, 'Miles Morales tái hợp với Gwen Stacy để thực hiện một cuộc phiêu lưu xuyên qua Đa vũ trụ, nơi anh gặp một nhóm Người Nhện chịu trách nhiệm bảo vệ sự tồn tại của nó.', '2023-06-02', '/img/movies/spiderman.jpg', '/img/movies/spiderman-banner.jpg', 'Now Showing'),
('Titanic', 194, 'Câu chuyện tình yêu đầy bi kịch giữa chàng họa sĩ nghèo Jack Dawson và tiểu thư quý tộc Rose DeWitt Bukater trên con tàu Titanic định mệnh.', '1997-12-19', '/img/movies/titanic.jpg', '/img/movies/titanic-banner.jpg', 'Now Showing'),
('Chiến Binh Báo Đen: Wakanda Bất Diệt', 161, 'Nữ hoàng Ramonda, Shuri, M''Baku, Okoye và Dora Milaje chiến đấu để bảo vệ quốc gia của họ khỏi sự can thiệp của các cường quốc thế giới sau cái chết của Vua T''Challa.', '2022-11-11', '/img/movies/wakanda.jpg', '/img/movies/wakanda-banner.jpg', 'Now Showing');

-- MovieGenres
INSERT INTO "MovieGenres" ("MovieId", "GenreId") VALUES 
(1, 6), (1, 2),
(2, 5), (2, 1),
(3, 3),
(4, 4),
(5, 1), (5, 5),
(6, 1), (6, 5),
(7, 1), (7, 2),
(8, 1),
(9, 1), (9, 2), (9, 5),
(10, 1), (10, 3),
(11, 1),
(12, 1), (12, 3), (12, 6),
(13, 4), (13, 5),
(14, 1), (14, 6),
(15, 1), (15, 6), (15, 5),
(16, 3),
(17, 1), (17, 5);

-- Theaters
INSERT INTO "Theaters" ("Name", "Address") VALUES 
('CinemaHub Nguyễn Huệ', '123 Nguyễn Huệ, Q1, TP.HCM'),
('CinemaHub Hà Đông', '110 Trần Phú, Hà Đông, Hà Nội'),
('CinemaHub Đà Nẵng', '910A Ngô Quyền, Sơn Trà, Đà Nẵng'),
('CinemaHub Cần Thơ', 'Đại lộ Hoà Bình, Ninh Kiều, Cần Thơ'),
('CinemaHub Huế', 'Vincom Plaza, Phú Nhuận, Huế');

-- Rooms (25 phòng)
INSERT INTO "Rooms" ("TheaterId", "RoomName", "SeatCount") VALUES 
(1, 'Phòng 1', 30), (1, 'Phòng 2', 20),
(2, 'Phòng 1', 40), (2, 'Phòng 2', 40), (2, 'Phòng 3', 30), (2, 'Phòng 4', 30), (2, 'Phòng 5', 50),
(3, 'Phòng 1', 50), (3, 'Phòng 2', 40), (3, 'Phòng 3', 40), (3, 'Phòng 4', 30), (3, 'Phòng 5', 30), (3, 'Phòng 6', 30),
(4, 'Phòng 1', 40), (4, 'Phòng 2', 40), (4, 'Phòng 3', 30), (4, 'Phòng 4', 30), (4, 'Phòng 5', 20),
(5, 'Phòng 1', 50), (5, 'Phòng 2', 40), (5, 'Phòng 3', 40), (5, 'Phòng 4', 30), (5, 'Phòng 5', 30), (5, 'Phòng 6', 30), (5, 'Phòng 7', 20);

-- Sinh ghế tự động (PL/pgSQL Block)
DO $$
DECLARE
    r_id INT;
    s_count INT;
    max_row CHAR(1);
    max_col INT;
    r_char CHAR(1);
    c_idx INT;
    s_type VARCHAR(20);
BEGIN
    FOR r_id IN 1..25 LOOP
        SELECT "SeatCount" INTO s_count FROM "Rooms" WHERE "RoomId" = r_id;
        
        IF s_count = 50 THEN max_row := 'E'; max_col := 10;
        ELSIF s_count = 40 THEN max_row := 'E'; max_col := 8;
        ELSIF s_count = 30 THEN max_row := 'E'; max_col := 6;
        ELSE max_row := 'D'; max_col := 5;
        END IF;

        FOR r_code IN ASCII('A')..ASCII(max_row) LOOP
            r_char := CHR(r_code);
            FOR c_idx IN 1..max_col LOOP
                IF r_code >= ASCII(max_row) - 1 THEN
                    s_type := 'VIP';
                ELSE
                    s_type := 'Standard';
                END IF;
                
                INSERT INTO "Seats" ("SeatNumber", "SeatType", "RoomId")
                VALUES (r_char || c_idx::TEXT, s_type, r_id);
            END LOOP;
        END LOOP;
    END LOOP;
END $$;

-- Sinh suất chiếu tự động
DO $$
DECLARE
    movie_rec RECORD;
    movie_ids INT[];
    movie_count INT;
    d DATE;
    r_id INT;
    slot_info RECORD;
    movie_idx INT;
    target_movie_id INT;
BEGIN
    SELECT array_agg("MovieId" ORDER BY "MovieId") INTO movie_ids FROM "Movies" WHERE "Status" = 'Now Showing';
    movie_count := array_length(movie_ids, 1);

    FOR d IN SELECT generate_series('2026-05-21'::DATE, '2026-05-31'::DATE, '1 day'::INTERVAL)::DATE LOOP
        FOR r_id IN 1..25 LOOP
            FOR slot_info IN (
                SELECT 1 as slot, 9 as h, 0 as mn, 75000.0 as price UNION ALL
                SELECT 2, 11, 30, 85000.0 UNION ALL
                SELECT 3, 14, 0, 90000.0 UNION ALL
                SELECT 4, 16, 30, 95000.0 UNION ALL
                SELECT 5, 19, 0, 110000.0 UNION ALL
                SELECT 6, 21, 30, 105000.0
            ) LOOP
                movie_idx := ((r_id + EXTRACT(DAY FROM d)::INT + slot_info.slot) % movie_count) + 1;
                target_movie_id := movie_ids[movie_idx];

                INSERT INTO "Showtimes" ("MovieId", "RoomId", "StartTime", "Price")
                VALUES (
                    target_movie_id,
                    r_id,
                    (d + (slot_info.h || ' hours')::INTERVAL + (slot_info.mn || ' minutes')::INTERVAL),
                    slot_info.price
                );
            END LOOP;
        END LOOP;
    END LOOP;
END $$;
