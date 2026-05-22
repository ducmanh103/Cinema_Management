using System.Diagnostics;
using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMovieService _movieService;
        private readonly CinemaDbContext _context;

        public HomeController(ILogger<HomeController> logger, IMovieService movieService, CinemaDbContext context)
        {
            _logger = logger;
            _movieService = movieService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var allMovies = await _movieService.GetAllMoviesAsync();
            var nowShowing = allMovies.Where(m => m.Status == "Now Showing").ToList();
            var comingSoon = allMovies.Where(m => m.Status == "Coming Soon").ToList();

            ViewBag.NowShowing = nowShowing;
            ViewBag.ComingSoon = comingSoon;
            
            return View();
        }



        public async Task<IActionResult> Theaters()
        {
            ViewData["Title"] = "Hệ thống Rạp";

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var theaters = await _context.Theaters
                .AsNoTracking()
                .Select(t => new
                {
                    t.TheaterId,
                    t.TheaterName,
                    t.Address,
                    RoomCount  = t.Rooms.Count(),
                    SeatCount  = t.Rooms.Sum(r => (int?)r.SeatCount) ?? 0,
                    // Suất chiếu hôm nay
                    ShowtimesToday = t.Rooms
                        .SelectMany(r => r.Showtimes)
                        .Count(s => s.StartTime >= today && s.StartTime < tomorrow),
                    // Số phim đang chiếu tại rạp này (7 ngày tới)
                    MoviesNow = t.Rooms
                        .SelectMany(r => r.Showtimes)
                        .Where(s => s.StartTime >= DateTime.Now && s.StartTime <= tomorrow.AddDays(6))
                        .Select(s => s.MovieId)
                        .Distinct()
                        .Count(),
                    // Suất chiếu sắp tới gần nhất (để hiện "Suất chiếu tiếp theo")
                    NextShowtime = t.Rooms
                        .SelectMany(r => r.Showtimes)
                        .Where(s => s.StartTime >= DateTime.Now)
                        .OrderBy(s => s.StartTime)
                        .Select(s => (DateTime?)s.StartTime)
                        .FirstOrDefault()
                })
                .OrderBy(t => t.TheaterId)
                .ToListAsync();

            // Tổng số suất chiếu & phim trên toàn hệ thống
            ViewBag.TotalShowtimesToday = theaters.Sum(t => t.ShowtimesToday);
            ViewBag.TotalMovies         = theaters.Sum(t => t.MoviesNow);
            ViewBag.TotalTheaters       = theaters.Count;
            ViewBag.TotalSeats          = theaters.Sum(t => t.SeatCount);

            return View(theaters);
        }

        public IActionResult News()
        {
            ViewData["Title"] = "Tin tức Điện ảnh";
            return View();
        }

        public IActionResult About()
        {
            ViewData["Title"] = "Giới thiệu CinemaHub";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
