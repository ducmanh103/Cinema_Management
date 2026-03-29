using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace CinemaManagement.Controllers
{
    public class ShowtimesController : Controller
    {
        private readonly IShowtimeService _showtimeService;

        public ShowtimesController(IShowtimeService showtimeService) => _showtimeService = showtimeService;

        // GET: /Showtimes?movieId=5
        public async Task<IActionResult> Index(int? movieId)
        {
            List<ShowtimeDto> showtimes;

            if (movieId.HasValue)
            {
                showtimes = await _showtimeService.GetShowtimesByMovieAsync(movieId.Value);
                // Get movie title for display
                if (showtimes.Any())
                    ViewBag.MovieTitle = showtimes.First().MovieTitle;
                else
                    ViewBag.MovieTitle = $"Phim #{movieId}";
                ViewBag.MovieId = movieId.Value;
            }
            else
            {
                showtimes = await _showtimeService.GetShowtimesByDateAsync(DateTime.Today);
                ViewBag.MovieTitle = null;
            }

            return View(showtimes);
        }
    }
}
