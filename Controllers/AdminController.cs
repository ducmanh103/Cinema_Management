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

        // ============================================================
        // DASHBOARD
        // ============================================================

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
                .AsNoTracking()
                .OrderByDescending(t => t.BookingTime)
                .Take(20)
                .Select(t => new TicketDto
                {
                    TicketId    = t.TicketId,
                    MovieTitle  = t.Showtime.Movie.Title,
                    RoomName    = t.Showtime.Room != null ? t.Showtime.Room.RoomName : "N/A",
                    StartTime   = t.Showtime.StartTime,
                    SeatNumber  = t.Seat != null ? t.Seat.SeatNumber : "N/A",
                    Price       = t.Payment != null ? t.Payment.Amount : t.Showtime.Price,
                    Status      = t.Status,
                    BookingTime = t.BookingTime,
                    PaymentStatus = t.Payment != null ? t.Payment.Status : "N/A",
                    CustomerName  = t.User != null ? (t.User.FullName ?? t.User.Username) : "N/A"
                })
                .ToListAsync();

            ViewBag.RecentTickets = recentTickets;
            return View();
        }

        // ============================================================
        // USERS
        // ============================================================

        // GET: /Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .AsNoTracking()
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

            ViewBag.Roles = await _context.Roles.AsNoTracking().ToListAsync();
            return View(users);
        }

        // POST: /Admin/ToggleUserStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var targetUser = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
            if (targetUser == null)
                return RedirectToAction(nameof(Users));

            // Staff không được khoá/mở khoá tài khoản Admin
            if (targetUser.Role.RoleName == "Admin" && !User.IsInRole("Admin"))
            {
                TempData["Error"] = "Bạn không có quyền khoá tài khoản Admin.";
                return RedirectToAction(nameof(Users));
            }

            targetUser.Status = targetUser.Status == "Active" ? "Inactive" : "Active";
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã {(targetUser.Status == "Active" ? "mở khoá" : "khoá")} người dùng '{targetUser.Username}' thành công.";
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/DeleteTicket  (AJAX – Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            var affected = await _context.Tickets
                .Where(t => t.TicketId == id)
                .ExecuteDeleteAsync();

            if (affected == 0)
                return Json(new { success = false, message = "Không tìm thấy vé." });

            return Json(new { success = true });
        }

        // POST: /Admin/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(int userId, int roleId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (user != null && role != null)
            {
                user.RoleId = roleId;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã đổi vai trò của '{user.Username}' thành '{role.RoleName}'.";
            }
            return RedirectToAction(nameof(Users));
        }

        // ============================================================
        // MOVIES
        // ============================================================

        // GET: /Admin/Movies
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Movies()
        {
            var movies = await _context.Movies
                .AsNoTracking()
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

        // ============================================================
        // SHOWTIMES
        // ============================================================

        // GET: /Admin/Showtimes
        public async Task<IActionResult> Showtimes()
        {
            var showtimes = await _context.Showtimes
                .AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            // Dữ liệu cho modal Create/Edit
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

        // POST: /Admin/CreateShowtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShowtime(int movieId, int roomId,
            DateTime startTime, decimal price)
        {
            if (movieId <= 0 || roomId <= 0 || price <= 0)
            {
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin suất chiếu.";
                return RedirectToAction(nameof(Showtimes));
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
            return RedirectToAction(nameof(Showtimes));
        }

        // GET: /Admin/EditShowtime/5  (AJAX – trả JSON để điền modal)
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

        // POST: /Admin/EditShowtime
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditShowtime(int showtimeId, int movieId,
            int roomId, DateTime startTime, decimal price)
        {
            var showtime = await _context.Showtimes.FindAsync(showtimeId);
            if (showtime == null)
            {
                TempData["Error"] = "Không tìm thấy suất chiếu.";
                return RedirectToAction(nameof(Showtimes));
            }

            showtime.MovieId = movieId;
            showtime.RoomId = roomId;
            showtime.StartTime = startTime;
            showtime.Price = price;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật suất chiếu thành công.";
            return RedirectToAction(nameof(Showtimes));
        }

        // POST: /Admin/DeleteShowtime/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteShowtime(int id)
        {
            var showtime = await _context.Showtimes.FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null)
            {
                TempData["Error"] = "Không tìm thấy suất chiếu.";
                return RedirectToAction(nameof(Showtimes));
            }

            var hasBookedTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeId == id && t.Status == "Booked");
            if (hasBookedTickets)
            {
                TempData["Error"] = "Không thể xoá suất chiếu có vé đã đặt.";
                return RedirectToAction(nameof(Showtimes));
            }

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xoá suất chiếu thành công.";
            return RedirectToAction(nameof(Showtimes));
        }

        // ============================================================
        // REVENUE – Thống kê doanh thu
        // ============================================================

        // GET: /Admin/Revenue
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Revenue(int? year)
        {
            int selectedYear = year ?? DateTime.Now.Year;

            // Doanh thu theo tháng trong năm được chọn
            var monthlyRevenue = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed" && p.PaidAt.Year == selectedYear)
                .GroupBy(p => p.PaidAt.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
                .ToListAsync();

            // Tổng doanh thu tất cả thời gian
            ViewBag.TotalRevenue = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            // Doanh thu năm được chọn
            ViewBag.YearRevenue = monthlyRevenue.Sum(m => m.Total);

            // Số vé trong năm
            ViewBag.YearTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.Status == "Booked" && t.BookingTime.Year == selectedYear)
                .CountAsync();

            // Tổng số vé tất cả thời gian (Completed payment)
            ViewBag.TotalPaidTickets = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .CountAsync();

            // Chart data: 12 tháng (0 nếu không có dữ liệu)
            var chartData = Enumerable.Range(1, 12)
                .Select(m => new
                {
                    Month = m,
                    Total = monthlyRevenue.FirstOrDefault(x => x.Month == m)?.Total ?? 0m,
                    Count = monthlyRevenue.FirstOrDefault(x => x.Month == m)?.Count ?? 0
                }).ToList();

            ViewBag.ChartLabels = chartData.Select(c => $"T{c.Month}").ToArray();
            ViewBag.ChartRevenue = chartData.Select(c => c.Total).ToArray();
            ViewBag.ChartTickets = chartData.Select(c => c.Count).ToArray();

            // Top phim doanh thu cao nhất
            var topMovies = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .GroupBy(p => new
                {
                    MovieId = p.Ticket.Showtime.Movie.MovieId,
                    Title = p.Ticket.Showtime.Movie.Title,
                    PosterUrl = p.Ticket.Showtime.Movie.PosterUrl
                })
                .Select(g => new MovieRevenueDto
                {
                    MovieId = g.Key.MovieId,
                    MovieTitle = g.Key.Title,
                    PosterUrl = g.Key.PosterUrl,
                    TotalRevenue = g.Sum(x => x.Amount),
                    TicketCount = g.Count()
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(10)
                .ToListAsync();

            ViewBag.TopMovies = topMovies;

            // Doanh thu theo phương thức thanh toán
            var byMethod = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .GroupBy(p => p.Method)
                .Select(g => new { Method = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
                .ToListAsync();
            ViewBag.PaymentMethods = byMethod;

            // Danh sách năm có dữ liệu
            var availableYears = await _context.Payments
                .AsNoTracking()
                .Select(p => p.PaidAt.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
            if (!availableYears.Contains(DateTime.Now.Year))
                availableYears.Insert(0, DateTime.Now.Year);
            ViewBag.AvailableYears = availableYears;
            ViewBag.SelectedYear = selectedYear;

            return View();
        }
    }
}
