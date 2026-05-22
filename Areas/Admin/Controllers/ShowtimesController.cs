using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class ShowtimesController : Controller
    {
        private readonly CinemaDbContext _context;

        public ShowtimesController(CinemaDbContext context) => _context = context;

        // GET: /Admin/Showtimes
        public async Task<IActionResult> Index()
        {
            var showtimes = await _context.Showtimes
                .AsNoTracking()
                .OrderBy(s => s.ShowtimeId)
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

            ViewBag.Rooms = await _context.Rooms
                .AsNoTracking()
                .Include(r => r.Theater)
                .OrderBy(r => r.Theater.TheaterName).ThenBy(r => r.RoomName)
                .ToListAsync();

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
                bool overlapping = await _context.Showtimes
                    .Include(s => s.Movie)
                    .AnyAsync(s => s.RoomId == roomId
                        && s.StartTime < endTime
                        && startTime < s.StartTime.AddMinutes(s.Movie.Duration));
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
                bool overlapping = await _context.Showtimes
                    .Include(s => s.Movie)
                    .AnyAsync(s => s.RoomId == roomId
                        && s.ShowtimeId != showtimeId
                        && s.StartTime < endTime
                        && startTime < s.StartTime.AddMinutes(s.Movie.Duration));
                if (overlapping)
                {
                    TempData["Error"] = "Phòng chiếu đã có suất chiếu trùng thời gian.";
                    return RedirectToAction(nameof(Index));
                }
            }

            showtime.MovieId = movieId;
            showtime.RoomId = roomId;
            showtime.StartTime = startTime;
            showtime.Price = price;

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
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xoá suất chiếu thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
