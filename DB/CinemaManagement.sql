/* ===============================
   CINEMA MANAGEMENT DATABASE
================================ */

-- XÓA DB NẾU ĐÃ TỒN TẠI
IF DB_ID('CinemaManagement') IS NOT NULL
    DROP DATABASE CinemaManagement;
GO

-- TẠO DATABASE
CREATE DATABASE CinemaManagement;
GO

USE CinemaManagement;
GO

/* ===============================
   1. ROLES
================================ */
CREATE TABLE Roles (
    RoleId INT IDENTITY PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);

INSERT INTO Roles (RoleName)
VALUES (N'Admin'), (N'Staff'), (N'Customer');

/* ===============================
   2. USERS
================================ */
CREATE TABLE Users (
    UserId INT IDENTITY PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100),
    Email NVARCHAR(100),
    RoleId INT NOT NULL,
    Status NVARCHAR(20) DEFAULT N'Active',
    CONSTRAINT FK_Users_Roles
        FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

/* ===============================
   3. MOVIES
================================ */
CREATE TABLE Movies (
    MovieId INT IDENTITY PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    Duration INT NOT NULL,
    Description NVARCHAR(MAX),
    ReleaseDate DATE,
    Status NVARCHAR(50) DEFAULT N'Now Showing'
);

/* ===============================
   4. GENRES
================================ */
CREATE TABLE Genres (
    GenreId INT IDENTITY PRIMARY KEY,
    GenreName NVARCHAR(100) NOT NULL UNIQUE
);

/* ===============================
   5. MOVIE_GENRES (N-N)
================================ */
CREATE TABLE MovieGenres (
    MovieId INT NOT NULL,
    GenreId INT NOT NULL,
    CONSTRAINT PK_MovieGenres PRIMARY KEY (MovieId, GenreId),
    CONSTRAINT FK_MovieGenres_Movies
        FOREIGN KEY (MovieId) REFERENCES Movies(MovieId),
    CONSTRAINT FK_MovieGenres_Genres
        FOREIGN KEY (GenreId) REFERENCES Genres(GenreId)
);

/* ===============================
   6. THEATERS
================================ */
CREATE TABLE Theaters (
    TheaterId INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Address NVARCHAR(255)
);

/* ===============================
   7. ROOMS
================================ */
CREATE TABLE Rooms (
    RoomId INT IDENTITY PRIMARY KEY,
    TheaterId INT NOT NULL,
    RoomName NVARCHAR(50),
    SeatCount INT NOT NULL,
    CONSTRAINT FK_Rooms_Theaters
        FOREIGN KEY (TheaterId) REFERENCES Theaters(TheaterId)
);

/* ===============================
   8. SHOWTIMES
================================ */
CREATE TABLE Showtimes (
    ShowtimeId INT IDENTITY PRIMARY KEY,
    MovieId INT NOT NULL,
    RoomId INT NOT NULL,
    StartTime DATETIME NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    CONSTRAINT FK_Showtimes_Movies
        FOREIGN KEY (MovieId) REFERENCES Movies(MovieId),
    CONSTRAINT FK_Showtimes_Rooms
        FOREIGN KEY (RoomId) REFERENCES Rooms(RoomId)
);

/* ===============================
   9. TICKETS
================================ */
CREATE TABLE Tickets (
    TicketId INT IDENTITY PRIMARY KEY,
    ShowtimeId INT NOT NULL,
    UserId INT NOT NULL,
    SeatNumber NVARCHAR(10) NOT NULL,
    BookingTime DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(50) DEFAULT N'Booked',
    CONSTRAINT FK_Tickets_Showtimes
        FOREIGN KEY (ShowtimeId) REFERENCES Showtimes(ShowtimeId),
    CONSTRAINT FK_Tickets_Users
        FOREIGN KEY (UserId) REFERENCES Users(UserId)
);

