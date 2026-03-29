namespace CinemaManagement.Models.ViewModels
{
    using System;
    using System.Collections.Generic;

    // ─────────── Movie DTOs ───────────
    public class MovieDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string? Description { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? PosterUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new();
    }

    public class CreateMovieDto
    {
        public string Title { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string? Description { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? PosterUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string Status { get; set; } = "Now Showing";
        public List<int> GenreIds { get; set; } = new();
    }

    // ─────────── Showtime DTOs ───────────
    public class ShowtimeDto
    {
        public int ShowtimeId { get; set; }
        public DateTime StartTime { get; set; }
        public decimal Price { get; set; }
        public int MovieId { get; set; }
        public string MovieTitle { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string TheaterName { get; set; } = string.Empty;
        public int AvailableSeats { get; set; }
    }

    public class CreateShowtimeDto
    {
        public DateTime StartTime { get; set; }
        public decimal Price { get; set; }
        public int MovieId { get; set; }
        public int RoomId { get; set; }
    }

    // ─────────── Seat DTOs ───────────
    public class SeatStatusDto
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public string SeatType { get; set; } = string.Empty;
        public bool IsBooked { get; set; }
    }

    // ─────────── Ticket / Booking DTOs ───────────
    public class BookTicketDto
    {
        public int ShowtimeId { get; set; }
        public int SeatId { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
    }

    public class TicketDto
    {
        public int TicketId { get; set; }
        public string MovieTitle { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime BookingTime { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    // ─────────── User DTOs (Admin) ───────────
    public class UserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }
}
