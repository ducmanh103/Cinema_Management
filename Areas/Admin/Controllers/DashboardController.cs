using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class DashboardController : Controller
    {
        private readonly CinemaDbContext _context;

        public DashboardController(CinemaDbContext context) => _context = context;

        // GET: /Admin/Dashboard
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

        // POST: /Admin/Dashboard/DeleteTicket  (AJAX – Admin only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Payment)
                .FirstOrDefaultAsync(t => t.TicketId == id);

            if (ticket == null)
                return Json(new { success = false, message = "Không tìm thấy vé." });

            if (ticket.Payment != null)
                _context.Payments.Remove(ticket.Payment);
            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
