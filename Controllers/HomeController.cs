using System.Diagnostics;
using CinemaManagement.Models;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace CinemaManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMovieService _movieService;

        public HomeController(ILogger<HomeController> logger, IMovieService movieService)
        {
            _logger = logger;
            _movieService = movieService;
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

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Theaters()
        {
            ViewData["Title"] = "Hệ thống Rạp";
            return View();
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
