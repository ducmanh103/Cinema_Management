using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class AdminController : Controller
    {
        private readonly CinemaDbContext _context;

        public AdminController(CinemaDbContext context) => _context = context;

        // GET: /Admin
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalMovies = await _context.Movies.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalTickets = await _context.Tickets.CountAsync();
            ViewBag.TotalRevenue = await _context.Payments
                .Where(p => p.Status == "Completed")
                .SumAsync(p => p.Amount);

            var recentTickets = await _context.Tickets
                .Include(t => t.Showtime).ThenInclude(s => s.Movie)
                .Include(t => t.Seat)
                .Include(t => t.User)
                .Include(t => t.Payment)
                .OrderByDescending(t => t.BookingTime)
                .Take(10)
                .Select(t => new TicketDto
                {
                    TicketId = t.TicketId,
                    MovieTitle = t.Showtime.Movie.Title,
                    RoomName = t.Showtime.Room.RoomName,
                    StartTime = t.Showtime.StartTime,
                    SeatNumber = t.Seat.SeatNumber,
                    Price = t.Payment != null ? t.Payment.Amount : (t.Showtime.Price + (t.Seat.SeatType == "VIP" ? 15000m : 0m)),
                    Status = t.Status,
                    BookingTime = t.BookingTime,
                    PaymentStatus = t.Payment != null ? t.Payment.Status : "N/A"
                })
                .ToListAsync();

            ViewBag.RecentTickets = recentTickets;
            return View();
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Select(u => new UserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    Status = u.Status,
                    RoleName = u.Role.RoleName,
                    RoleId = u.RoleId
                })
                .ToListAsync();

            ViewBag.Roles = await _context.Roles.ToListAsync();
            return View(users);
        }

        // POST: /Admin/ToggleUserStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.Status = user.Status == "Active" ? "Inactive" : "Active";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã {(user.Status == "Active" ? "mở khoá" : "khoá")} người dùng thành công.";
            }
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(int userId, int roleId)
        {
            var user = await _context.Users.FindAsync(userId);
            var role = await _context.Roles.FindAsync(roleId);
            if (user != null && role != null)
            {
                user.RoleId = roleId;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã đổi vai trò của '{user.Username}' thành '{role.RoleName}'.";
            }
            return RedirectToAction(nameof(Users));
        }

        // GET: /Admin/Showtimes
        public async Task<IActionResult> Showtimes()
        {
            var showtimes = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .OrderBy(s => s.StartTime)
                .ToListAsync();
            return View(showtimes);
        }

        // GET: /Admin/Movies
        public async Task<IActionResult> Movies()
        {
            var movies = await _context.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .OrderByDescending(m => m.MovieId)
                .Select(m => new MovieDto
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Duration = m.Duration,
                    ReleaseDate = m.ReleaseDate,
                    PosterUrl = m.PosterUrl,
                    Status = m.Status,
                    Genres = m.MovieGenres.Select(mg => mg.Genre.GenreName).ToList()
                })
                .ToListAsync();
            return View(movies);
        }
    }
}
