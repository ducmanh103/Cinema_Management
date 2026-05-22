using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Controllers
{
    public class ShowtimesController : Controller
    {
        private readonly IShowtimeService _showtimeService;
        private readonly CinemaDbContext _context;

        public ShowtimesController(IShowtimeService showtimeService, CinemaDbContext context)
        {
            _showtimeService = showtimeService;
            _context = context;
        }

        // GET: /Showtimes?movieId=5  HOẶC  /Showtimes?date=2026-05-21&theaterId=2
        public async Task<IActionResult> Index(int? movieId, int? theaterId, DateTime? date)
        {
            // Tải danh sách rạp cho dropdown
            var theaters = await _context.Theaters
                .AsNoTracking()
                .OrderBy(t => t.TheaterName)
                .Select(t => new { t.TheaterId, t.TheaterName })
                .ToListAsync();

            ViewBag.Theaters = theaters;

            // Nếu filter theo phim (từ trang Movies)
            if (movieId.HasValue)
            {
                var selectedDate2     = date?.Date ?? DateTime.Today;
                var selectedTheaterId2 = theaterId;

                var showtimes = await _showtimeService.GetShowtimesByMovieAndFiltersAsync(
                    movieId.Value, selectedDate2, selectedTheaterId2);

                // Lấy tên phim (từ kết quả hoặc DB)
                string movieTitle = showtimes.Any()
                    ? showtimes.First().MovieTitle
                    : (await _context.Movies.AsNoTracking()
                          .Where(m => m.MovieId == movieId.Value)
                          .Select(m => m.Title)
                          .FirstOrDefaultAsync() ?? $"Phim #{movieId}");

                ViewBag.MovieTitle        = movieTitle;
                ViewBag.MovieId           = movieId.Value;
                ViewBag.SelectedDate      = selectedDate2;
                ViewBag.SelectedTheaterId = selectedTheaterId2;

                // Cung cấp dữ liệu phim chi tiết cho View
                ViewBag.ActiveMovie = await _context.Movies
                    .AsNoTracking()
                    .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                    .FirstOrDefaultAsync(m => m.MovieId == movieId.Value);

                return View(showtimes);
            }

            // Chế độ lịch chiếu theo ngày + rạp
            var selectedDate     = date?.Date ?? DateTime.Today;
            var selectedTheaterId = theaterId;

            ViewBag.SelectedDate      = selectedDate;
            ViewBag.SelectedTheaterId = selectedTheaterId;
            ViewBag.MovieTitle        = null;
            ViewBag.MovieId           = null;

            var result = await _showtimeService.GetShowtimesByDateAndTheaterAsync(selectedDate, selectedTheaterId);

            // Cung cấp dữ liệu phim cho View (tránh inject DbContext trong View)
            var distinctTitles = result.Select(s => s.MovieTitle).Distinct().ToList();
            ViewBag.MovieInfoDict = await _context.Movies
                .AsNoTracking()
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Where(m => distinctTitles.Contains(m.Title))
                .ToDictionaryAsync(m => m.Title, m => m);

            return View(result);
        }
    }
}
