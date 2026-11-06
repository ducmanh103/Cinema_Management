using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RevenueController : Controller
    {
        private readonly CinemaDbContext _context;

        public RevenueController(CinemaDbContext context) => _context = context;

        // GET: /Admin/Revenue
        public async Task<IActionResult> Index(int? year)
        {
            int currentYear = CinemaManagement.Helpers.VietnamTime.Now.Year;
            int selectedYear = year ?? currentYear;

            var yearStart = new DateTime(selectedYear, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var yearEnd = yearStart.AddYears(1);

            var yearPayments = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed" && p.PaidAt >= yearStart && p.PaidAt < yearEnd)
                .Select(p => new { p.PaidAt, p.Amount })
                .ToListAsync();

            var monthlyRevenue = yearPayments
                .GroupBy(p => p.PaidAt.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
                .ToList();

            ViewBag.TotalRevenue = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            ViewBag.YearRevenue = monthlyRevenue.Sum(m => m.Total);

            ViewBag.YearTickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.Status == "Booked" && t.BookingTime >= yearStart && t.BookingTime < yearEnd)
                .CountAsync();

            ViewBag.TotalPaidTickets = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .CountAsync();

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

            var byMethod = await _context.Payments
                .AsNoTracking()
                .Where(p => p.Status == "Completed")
                .GroupBy(p => p.Method)
                .Select(g => new { Method = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
                .ToListAsync();
            ViewBag.PaymentMethods = byMethod;

            var minPaidAt = await _context.Payments
                .AsNoTracking()
                .MinAsync(p => (DateTime?)p.PaidAt);
            var maxPaidAt = await _context.Payments
                .AsNoTracking()
                .MaxAsync(p => (DateTime?)p.PaidAt);

            var availableYears = new List<int> { currentYear };
            if (minPaidAt.HasValue && maxPaidAt.HasValue)
            {
                for (int y = maxPaidAt.Value.Year; y >= minPaidAt.Value.Year; y--)
                {
                    if (!availableYears.Contains(y))
                        availableYears.Add(y);
                }
            }
            availableYears = availableYears.OrderByDescending(y => y).ToList();
            ViewBag.AvailableYears = availableYears;
            ViewBag.SelectedYear = selectedYear;

            return View();
        }
    }
}
