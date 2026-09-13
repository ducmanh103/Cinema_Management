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
            var now = DateTime.Now;
            return await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId && s.StartTime >= now)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId   = s.ShowtimeId,
                    StartTime    = s.StartTime,
                    Price        = s.Price,
                    MovieId      = s.MovieId,
                    MovieTitle   = s.Movie.Title,
                    MoviePosterUrl = s.Movie.PosterUrl,
                    MovieDuration = s.Movie.Duration,
                    RoomId       = s.RoomId,
                    RoomName     = s.Room.RoomName,
                    TheaterName  = s.Room.Theater.TheaterName,
                    AvailableSeats = s.Room.SeatCount
                                   - s.Tickets.Count(t => t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now)))
                })
                .ToListAsync();
        }

        public async Task<List<ShowtimeDto>> GetShowtimesByMovieAndFiltersAsync(int movieId, DateTime? date, int? theaterId)
        {
            var now = DateTime.Now;
            var query = _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId && s.StartTime >= now);

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(s => s.StartTime >= start && s.StartTime < end);
            }

            if (theaterId.HasValue)
                query = query.Where(s => s.Room.TheaterId == theaterId.Value);

            return await query
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId   = s.ShowtimeId,
                    StartTime    = s.StartTime,
                    Price        = s.Price,
                    MovieId      = s.MovieId,
                    MovieTitle   = s.Movie.Title,
                    MoviePosterUrl = s.Movie.PosterUrl,
                    MovieDuration = s.Movie.Duration,
                    RoomId       = s.RoomId,
                    RoomName     = s.Room.RoomName,
                    TheaterName  = s.Room.Theater.TheaterName,
                    AvailableSeats = s.Room.SeatCount
                                   - s.Tickets.Count(t => t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now)))
                })
                .ToListAsync();
        }

        public async Task<List<ShowtimeDto>> GetShowtimesByDateAsync(DateTime date)
        {
            var now = DateTime.Now;
            var start = date.Date;
            var end = start.AddDays(1);
            return await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.StartTime >= start && s.StartTime < end)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId   = s.ShowtimeId,
                    StartTime    = s.StartTime,
                    Price        = s.Price,
                    MovieId      = s.MovieId,
                    MovieTitle   = s.Movie.Title,
                    MoviePosterUrl = s.Movie.PosterUrl,
                    MovieDuration = s.Movie.Duration,
                    RoomId       = s.RoomId,
                    RoomName     = s.Room.RoomName,
                    TheaterName  = s.Room.Theater.TheaterName,
                    AvailableSeats = s.Room.SeatCount
                                   - s.Tickets.Count(t => t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now)))
                })
                .ToListAsync();
        }

        public async Task<List<ShowtimeDto>> GetShowtimesByTheaterAsync(int theaterId)
        {
            var now = DateTime.Now;
            return await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.Room.TheaterId == theaterId && s.StartTime >= now)
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId   = s.ShowtimeId,
                    StartTime    = s.StartTime,
                    Price        = s.Price,
                    MovieId      = s.MovieId,
                    MovieTitle   = s.Movie.Title,
                    MoviePosterUrl = s.Movie.PosterUrl,
                    MovieDuration = s.Movie.Duration,
                    RoomId       = s.RoomId,
                    RoomName     = s.Room.RoomName,
                    TheaterName  = s.Room.Theater.TheaterName,
                    AvailableSeats = s.Room.SeatCount
                                   - s.Tickets.Count(t => t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now)))
                })
                .ToListAsync();
        }

        public async Task<List<ShowtimeDto>> GetShowtimesByDateAndTheaterAsync(DateTime date, int? theaterId)
        {
            var now = DateTime.Now;
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            var query = _context.Showtimes
                .AsNoTracking()
                .Where(s => s.StartTime >= startOfDay && s.StartTime < endOfDay);

            if (theaterId.HasValue)
                query = query.Where(s => s.Room.TheaterId == theaterId.Value);

            return await query
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId   = s.ShowtimeId,
                    StartTime    = s.StartTime,
                    Price        = s.Price,
                    MovieId      = s.MovieId,
                    MovieTitle   = s.Movie.Title,
                    MoviePosterUrl = s.Movie.PosterUrl,
                    MovieDuration = s.Movie.Duration,
                    RoomId       = s.RoomId,
                    RoomName     = s.Room.RoomName,
                    TheaterName  = s.Room.Theater.TheaterName,
                    AvailableSeats = s.Room.SeatCount
                                   - s.Tickets.Count(t => t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now)))
                })
                .ToListAsync();
        }

        public async Task<ShowtimeDto?> GetShowtimeByIdAsync(int id)
        {
            var now = DateTime.Now;
            return await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.ShowtimeId == id)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId   = s.ShowtimeId,
                    StartTime    = s.StartTime,
                    Price        = s.Price,
                    MovieId      = s.MovieId,
                    MovieTitle   = s.Movie.Title,
                    MoviePosterUrl = s.Movie.PosterUrl,
                    MovieDuration = s.Movie.Duration,
                    RoomId       = s.RoomId,
                    RoomName     = s.Room.RoomName,
                    TheaterName  = s.Room.Theater.TheaterName,
                    AvailableSeats = s.Room.SeatCount
                                   - s.Tickets.Count(t => t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now)))
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<SeatStatusDto>> GetSeatStatusAsync(int showtimeId)
        {
            // Lấy RoomId và danh sách SeatId đã đặt hoặc đang giữ chỗ qua truy vấn
            var roomId = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.ShowtimeId == showtimeId)
                .Select(s => s.RoomId)
                .FirstOrDefaultAsync();

            if (roomId == 0) return new List<SeatStatusDto>();

            var now = DateTime.Now;
            var bookedSeatIds = (await _context.Tickets
                .AsNoTracking()
                .Where(t => t.ShowtimeId == showtimeId && (t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now))))
                .Select(t => t.SeatId)
                .ToListAsync()).ToHashSet();

            return await _context.Seats
                .AsNoTracking()
                .Where(s => s.RoomId == roomId)
                .Select(s => new SeatStatusDto
                {
                    SeatId     = s.SeatId,
                    SeatNumber = s.SeatNumber,
                    SeatType   = s.SeatType,
                    IsBooked   = bookedSeatIds.Contains(s.SeatId)
                })
                .ToListAsync();
        }

        public async Task<ShowtimeDto> CreateShowtimeAsync(CreateShowtimeDto dto)
        {
            var showtime = new Showtime
            {
                StartTime = dto.StartTime,
                Price     = dto.Price,
                MovieId   = dto.MovieId,
                RoomId    = dto.RoomId
            };

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();
            return (await GetShowtimeByIdAsync(showtime.ShowtimeId))!;
        }

        public async Task<bool> DeleteShowtimeAsync(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null) return false;

            // Kiểm tra có vé đã đặt hoặc đang thanh toán trước khi xóa
            var now = DateTime.Now;
            bool hasTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeId == id && (t.Status == "Booked" || (t.Status == "Pending" && (t.HeldUntil == null || t.HeldUntil > now))));
            if (hasTickets)
                throw new InvalidOperationException("Không thể xoá suất chiếu có vé đã đặt.");

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
