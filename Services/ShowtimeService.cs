using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Services
{
    public class ShowtimeService : IShowtimeService
    {
        private readonly CinemaDbContext _context;

        public ShowtimeService(CinemaDbContext context) => _context = context;

        public async Task<List<ShowtimeDto>> GetShowtimesByMovieAsync(int movieId)
        {
            return await _context.Showtimes
                .Where(s => s.MovieId == movieId && s.StartTime >= DateTime.Now)
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .Include(s => s.Room).ThenInclude(r => r.Seats)
                .Include(s => s.Tickets)
                .Select(s => ToDto(s))
                .ToListAsync();
        }

        public async Task<List<ShowtimeDto>> GetShowtimesByDateAsync(DateTime date)
        {
            return await _context.Showtimes
                .Where(s => s.StartTime.Date >= date.Date)
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .Include(s => s.Room).ThenInclude(r => r.Seats)
                .Include(s => s.Tickets)
                .Select(s => ToDto(s))
                .ToListAsync();
        }

        public async Task<ShowtimeDto?> GetShowtimeByIdAsync(int id)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .Include(s => s.Room).ThenInclude(r => r.Seats)
                .Include(s => s.Tickets)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            return showtime == null ? null : ToDto(showtime);
        }

        public async Task<List<SeatStatusDto>> GetSeatStatusAsync(int showtimeId)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Room).ThenInclude(r => r.Seats)
                .Include(s => s.Tickets)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null) return new List<SeatStatusDto>();

            var bookedSeatIds = showtime.Tickets
                .Where(t => t.Status == "Booked")
                .Select(t => t.SeatId)
                .ToHashSet();

            return showtime.Room.Seats.Select(seat => new SeatStatusDto
            {
                SeatId = seat.SeatId,
                SeatNumber = seat.SeatNumber,
                SeatType = seat.SeatType,
                IsBooked = bookedSeatIds.Contains(seat.SeatId)
            }).ToList();
        }

        public async Task<ShowtimeDto> CreateShowtimeAsync(CreateShowtimeDto dto)
        {
            var showtime = new Showtime
            {
                StartTime = dto.StartTime,
                Price = dto.Price,
                MovieId = dto.MovieId,
                RoomId = dto.RoomId
            };

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();
            return (await GetShowtimeByIdAsync(showtime.ShowtimeId))!;
        }

        public async Task<bool> DeleteShowtimeAsync(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null) return false;
            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
            return true;
        }

        private static ShowtimeDto ToDto(Showtime s) => new()
        {
            ShowtimeId = s.ShowtimeId,
            StartTime = s.StartTime,
            Price = s.Price,
            MovieId = s.MovieId,
            MovieTitle = s.Movie?.Title ?? "",
            RoomId = s.RoomId,
            RoomName = s.Room?.RoomName ?? "",
            TheaterName = s.Room?.Theater?.TheaterName ?? "",
            AvailableSeats = s.Room != null
                ? s.Room.Seats.Count - s.Tickets.Count(t => t.Status == "Booked")
                : 0
        };
    }
}
