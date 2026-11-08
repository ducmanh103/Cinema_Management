using CinemaManagement.Data;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class ShowtimesController : Controller
    {
        private readonly CinemaDbContext _context;

        public ShowtimesController(CinemaDbContext context) => _context = context;

        // GET: /Admin/Showtimes
        public async Task<IActionResult> Index(int? movieId, int? theaterId, string? dateFilter, string? search)
        {
            var nowVn = VietnamTime.Now;
            var query = _context.Showtimes
                .AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Room)
                    .ThenInclude(r => r.Theater)
                .AsQueryable();

            if (movieId.HasValue && movieId.Value > 0)
            {
                query = query.Where(s => s.MovieId == movieId.Value);
            }

            if (theaterId.HasValue && theaterId.Value > 0)
            {
                query = query.Where(s => s.Room.TheaterId == theaterId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s => s.Movie.Title.ToLower().Contains(term)
                                      || s.Room.RoomName.ToLower().Contains(term)
                                      || s.Room.Theater.TheaterName.ToLower().Contains(term));
            }

            var todayStart = nowVn.Date;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);

            switch (dateFilter?.ToLowerInvariant())
            {
                case "today":
                    query = query.Where(s => s.StartTime >= todayStart && s.StartTime <= todayEnd);
                    break;
                case "tomorrow":
                    var tomorrowStart = todayStart.AddDays(1);
                    var tomorrowEnd = tomorrowStart.AddDays(1).AddTicks(-1);
                    query = query.Where(s => s.StartTime >= tomorrowStart && s.StartTime <= tomorrowEnd);
                    break;
                case "next7days":
                    var next7End = todayStart.AddDays(7).AddTicks(-1);
                    query = query.Where(s => s.StartTime >= todayStart && s.StartTime <= next7End);
                    break;
                case "upcoming":
                    query = query.Where(s => s.StartTime >= nowVn);
                    break;
                case "past":
                    query = query.Where(s => s.StartTime < nowVn);
                    break;
                case "all":
                default:
                    break;
            }

            // Sắp xếp tối ưu: nếu xem sắp chiếu hoặc hôm nay thì suất chiếu gần nhất ở trên
            if (dateFilter == "today" || dateFilter == "tomorrow" || dateFilter == "next7days" || dateFilter == "upcoming")
            {
                query = query.OrderBy(s => s.StartTime);
            }
            else
            {
                query = query.OrderByDescending(s => s.StartTime);
            }

            var showtimes = await query
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    StartTime = s.StartTime,
                    Price = s.Price,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie.Title,
                    MovieDuration = s.Movie.Duration,
                    RoomId = s.RoomId,
                    RoomName = s.Room.RoomName,
                    TheaterName = s.Room.Theater.TheaterName
                })
                .ToListAsync();

            ViewBag.Movies = await _context.Movies
                .AsNoTracking()
                .Where(m => m.Status != "Ended")
                .OrderBy(m => m.Title)
                .ToListAsync();

            ViewBag.Theaters = await _context.Theaters
                .AsNoTracking()
                .OrderBy(t => t.TheaterName)
                .ToListAsync();

            ViewBag.Rooms = await _context.Rooms
                .AsNoTracking()
                .Include(r => r.Theater)
                .OrderBy(r => r.Theater.TheaterName).ThenBy(r => r.RoomName)
                .ToListAsync();

            ViewBag.SelectedMovieId = movieId;
            ViewBag.SelectedTheaterId = theaterId;
            ViewBag.SelectedDateFilter = dateFilter ?? "all";
            ViewBag.SearchTerm = search;

            // Thống kê nhanh thanh công cụ
            ViewBag.TotalUpcoming = await _context.Showtimes.AsNoTracking().CountAsync(s => s.StartTime >= nowVn);
            ViewBag.TotalToday = await _context.Showtimes.AsNoTracking().CountAsync(s => s.StartTime >= todayStart && s.StartTime <= todayEnd);

            return View(showtimes);
        }

        // POST: /Admin/Showtimes/CreateShowtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShowtime(int movieId, int roomId,
            DateTime startTime, decimal price)
        {
            if (movieId <= 0 || roomId <= 0 || price <= 0)
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin suất chiếu.";
                return RedirectToAction(nameof(Index));
            }

            var movieForDuration = await _context.Movies.FindAsync(movieId);
            if (movieForDuration != null)
            {
                var endTime = startTime.AddMinutes(movieForDuration.Duration);
                var windowStart = startTime.AddHours(-6);
                var candidates = await _context.Showtimes
                    .AsNoTracking()
                    .Where(s => s.RoomId == roomId && s.StartTime >= windowStart && s.StartTime < endTime)
                    .Select(s => new { s.StartTime, Duration = s.Movie.Duration })
                    .ToListAsync();

                bool overlapping = candidates.Any(s =>
                    s.StartTime < endTime && startTime < s.StartTime.AddMinutes(s.Duration));
                if (overlapping)
                {
                    TempData["Error"] = "Phòng chiếu đã có suất chiếu trùng thời gian.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var showtime = new Showtime
            {
                MovieId = movieId,
                RoomId = roomId,
                StartTime = startTime,
                Price = price
            };

            _context.Showtimes.Add(showtime);

            // Ghi Audit Log vào cơ sở dữ liệu
            _context.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUsername = User.Identity?.Name,
                ActorRole = User.FindFirst(ClaimTypes.Role)?.Value,
                Action = "CREATE_SHOWTIME",
                EntityName = "Showtime",
                Details = $"Tạo suất chiếu cho phim ID={movieId}, phòng ID={roomId}, giờ chiếu={startTime:dd/MM/yyyy HH:mm}, giá={price:N0}đ",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Path = "/Admin/Showtimes/CreateShowtime"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm suất chiếu mới thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Showtimes/GetShowtime/5 (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetShowtime(int id)
        {
            var st = await _context.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);
            if (st == null) return NotFound();
            return Json(new
            {
                st.ShowtimeId,
                st.MovieId,
                st.RoomId,
                StartTime = st.StartTime.ToString("yyyy-MM-ddTHH:mm"),
                st.Price
            });
        }

        // POST: /Admin/Showtimes/EditShowtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShowtime(int showtimeId, int movieId,
            int roomId, DateTime startTime, decimal price)
        {
            var showtime = await _context.Showtimes.FindAsync(showtimeId);
            if (showtime == null)
            {
                TempData["Error"] = "Không tìm thấy suất chiếu.";
                return RedirectToAction(nameof(Index));
            }

            var movieForDuration = await _context.Movies.FindAsync(movieId);
            if (movieForDuration != null)
            {
                var endTime = startTime.AddMinutes(movieForDuration.Duration);
                var windowStart = startTime.AddHours(-6);
                var candidates = await _context.Showtimes
                    .AsNoTracking()
                    .Where(s => s.RoomId == roomId
                        && s.ShowtimeId != showtimeId
                        && s.StartTime >= windowStart
                        && s.StartTime < endTime)
                    .Select(s => new { s.StartTime, Duration = s.Movie.Duration })
                    .ToListAsync();

                bool overlapping = candidates.Any(s =>
                    s.StartTime < endTime && startTime < s.StartTime.AddMinutes(s.Duration));
                if (overlapping)
                {
                    TempData["Error"] = "Phòng chiếu đã có suất chiếu trùng thời gian.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var oldTime = showtime.StartTime;
            showtime.MovieId = movieId;
            showtime.RoomId = roomId;
            showtime.StartTime = startTime;
            showtime.Price = price;

            _context.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUsername = User.Identity?.Name,
                ActorRole = User.FindFirst(ClaimTypes.Role)?.Value,
                Action = "EDIT_SHOWTIME",
                EntityName = "Showtime",
                EntityId = showtimeId.ToString(),
                Details = $"Cập nhật suất chiếu #{showtimeId}: Giờ cũ={oldTime:dd/MM/yyyy HH:mm} -> Giờ mới={startTime:dd/MM/yyyy HH:mm}, giá={price:N0}đ",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Path = "/Admin/Showtimes/EditShowtime"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật suất chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Showtimes/DeleteShowtime/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteShowtime(int id)
        {
            var showtime = await _context.Showtimes.FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null)
            {
                TempData["Error"] = "Không tìm thấy suất chiếu.";
                return RedirectToAction(nameof(Index));
            }

            var hasBookedTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeId == id && t.Status == "Booked");
            if (hasBookedTickets)
            {
                TempData["Error"] = "Không thể xoá suất chiếu có vé đã đặt.";
                return RedirectToAction(nameof(Index));
            }

            _context.Showtimes.Remove(showtime);

            _context.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                ActorUsername = User.Identity?.Name,
                ActorRole = User.FindFirst(ClaimTypes.Role)?.Value,
                Action = "DELETE_SHOWTIME",
                EntityName = "Showtime",
                EntityId = id.ToString(),
                Details = $"Xoá suất chiếu #{id} (Phim ID={showtime.MovieId}, Bắt đầu={showtime.StartTime:dd/MM/yyyy HH:mm})",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Path = "/Admin/Showtimes/DeleteShowtime"
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xoá suất chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
