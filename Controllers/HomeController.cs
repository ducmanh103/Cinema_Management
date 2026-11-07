using System.Diagnostics;
using CinemaManagement.Data;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CinemaManagement.Controllers
{
    public class HomeController : Controller
    {
        private const string LandingSnapshotCacheKey = "cinema_home_landing_snapshot_v1";

        private readonly ILogger<HomeController> _logger;
        private readonly IMovieService _movieService;
        private readonly CinemaDbContext _context;
        private readonly IMemoryCache _cache;

        public HomeController(
            ILogger<HomeController> logger,
            IMovieService movieService,
            CinemaDbContext context,
            IMemoryCache cache)
        {
            _logger = logger;
            _movieService = movieService;
            _context = context;
            _cache = cache;
        }

        private sealed record LandingInfraSnapshot(
            List<TheaterCardDto> Theaters,
            List<ShowtimeDto> UpcomingShowtimes,
            int TotalRooms,
            int TotalSeats,
            int ShowtimesToday);

        public async Task<IActionResult> Index()
        {
            var allMovies = await _movieService.GetAllMoviesAsync();
            var nowShowing = allMovies.Where(m => m.Status == "Now Showing").ToList();
            var comingSoon = allMovies.Where(m => m.Status == "Coming Soon").ToList();

            var featuredMovies = nowShowing
                .Where(m => !string.IsNullOrWhiteSpace(m.BannerUrl))
                .OrderByDescending(m => m.HasShowtimes)
                .ThenByDescending(m => m.BookedTicketCount)
                .Take(5)
                .ToList();

            if (featuredMovies.Count < 2)
            {
                var existingIds = featuredMovies.Select(f => f.MovieId).ToHashSet();
                var extras = nowShowing
                    .Concat(comingSoon)
                    .Where(m => !existingIds.Contains(m.MovieId))
                    .Take(5 - featuredMovies.Count);
                featuredMovies.AddRange(extras);
            }

            var topBoxOffice = nowShowing
                .Concat(comingSoon.Take(2))
                .OrderByDescending(m => m.BookedTicketCount)
                .ThenByDescending(m => m.HasShowtimes)
                .ThenByDescending(m => MovieDisplayHelper.GetMovieRating(m.MovieId))
                .Take(6)
                .ToList();

            var genres = nowShowing
                .Concat(comingSoon)
                .SelectMany(m => m.Genres)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            if (!_cache.TryGetValue(LandingSnapshotCacheKey, out LandingInfraSnapshot? snapshot) || snapshot == null)
            {
                var today = VietnamTime.Today;
                var now = VietnamTime.Now;
                var tomorrow = today.AddDays(1);
                var weekAhead = tomorrow.AddDays(6);

                var theaters = await _context.Theaters
                    .AsNoTracking()
                    .Select(t => new TheaterCardDto
                    {
                        TheaterId = t.TheaterId,
                        TheaterName = t.TheaterName,
                        Address = t.Address,
                        RoomCount = t.Rooms.Count(),
                        SeatCount = t.Rooms.Sum(r => (int?)r.SeatCount) ?? 0,
                        ShowtimesToday = t.Rooms
                            .SelectMany(r => r.Showtimes)
                            .Count(s => s.StartTime >= today && s.StartTime < tomorrow),
                        MoviesNow = t.Rooms
                            .SelectMany(r => r.Showtimes)
                            .Where(s => s.StartTime >= now && s.StartTime <= weekAhead)
                            .Select(s => s.MovieId)
                            .Distinct()
                            .Count(),
                        NextShowtime = t.Rooms
                            .SelectMany(r => r.Showtimes)
                            .Where(s => s.StartTime >= now)
                            .OrderBy(s => s.StartTime)
                            .Select(s => (DateTime?)s.StartTime)
                            .FirstOrDefault()
                    })
                    .OrderByDescending(t => t.ShowtimesToday)
                    .ThenBy(t => t.TheaterId)
                    .ToListAsync();

                var upcomingRaw = await _context.Showtimes
                    .AsNoTracking()
                    .Where(s => s.StartTime >= now && s.StartTime <= now.AddDays(3))
                    .OrderBy(s => s.StartTime)
                    .Take(14)
                    .Select(s => new ShowtimeDto
                    {
                        ShowtimeId = s.ShowtimeId,
                        StartTime = s.StartTime,
                        Price = s.Price,
                        MovieId = s.MovieId,
                        MovieTitle = s.Movie.Title,
                        MoviePosterUrl = s.Movie.PosterUrl,
                        MovieDuration = s.Movie.Duration,
                        RoomId = s.RoomId,
                        RoomName = s.Room.RoomName,
                        TheaterId = s.Room.TheaterId,
                        TheaterName = s.Room.Theater.TheaterName,
                        AvailableSeats = s.Room.SeatCount
                            - s.Tickets.Count(tk => tk.Status == "Booked" || (tk.Status == "Pending" && (tk.HeldUntil == null || tk.HeldUntil > now)))
                    })
                    .ToListAsync();

                var upcoming = upcomingRaw
                    .Where(st => st.AvailableSeats > 0)
                    .Take(8)
                    .ToList();

                snapshot = new LandingInfraSnapshot(
                    Theaters: theaters,
                    UpcomingShowtimes: upcoming,
                    TotalRooms: theaters.Sum(t => t.RoomCount),
                    TotalSeats: theaters.Sum(t => t.SeatCount),
                    ShowtimesToday: theaters.Sum(t => t.ShowtimesToday));

                _cache.Set(LandingSnapshotCacheKey, snapshot, TimeSpan.FromSeconds(60));
            }

            var vm = new HomeLandingViewModel
            {
                FeaturedMovies = featuredMovies,
                NowShowing = nowShowing,
                ComingSoon = comingSoon,
                TopBoxOffice = topBoxOffice,
                Genres = genres,
                Theaters = snapshot.Theaters,
                UpcomingShowtimes = snapshot.UpcomingShowtimes,
                TotalTheaters = snapshot.Theaters.Count,
                TotalRooms = snapshot.TotalRooms,
                TotalSeats = snapshot.TotalSeats,
                ShowtimesToday = snapshot.ShowtimesToday,
                NowShowingCount = nowShowing.Count,
                ComingSoonCount = comingSoon.Count
            };

            ViewBag.NowShowing = nowShowing;
            ViewBag.ComingSoon = comingSoon;
            ViewBag.FullWidth = true;

            return View(vm);
        }



        public async Task<IActionResult> Theaters()
        {
            ViewData["Title"] = "Hệ thống Rạp";

            var today = CinemaManagement.Helpers.VietnamTime.Today;
            var now = CinemaManagement.Helpers.VietnamTime.Now;
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
                        .Where(s => s.StartTime >= now && s.StartTime <= tomorrow.AddDays(6))
                        .Select(s => s.MovieId)
                        .Distinct()
                        .Count(),
                    // Suất chiếu sắp tới gần nhất (để hiện "Suất chiếu tiếp theo")
                    NextShowtime = t.Rooms
                        .SelectMany(r => r.Showtimes)
                        .Where(s => s.StartTime >= now)
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
            var exFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            if (exFeature?.Error != null)
            {
                _logger.LogError(exFeature.Error, "Unhandled exception on path {Path}", exFeature.Path);
                HttpContext.RequestServices.GetService<ISecurityAlertService>()?.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.Critical,
                    "UNHANDLED_SERVER_EXCEPTION",
                    "Lỗi hệ thống 500 chưa xử lý (Unhandled Exception)",
                    $"{exFeature.Error.GetType().Name}: {exFeature.Error.Message}",
                    IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Username: User.Identity?.Name,
                    Path: exFeature.Path));
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
