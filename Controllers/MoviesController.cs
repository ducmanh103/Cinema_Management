using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Data;
using CinemaManagement.Services;

namespace CinemaManagement.Controllers
{
    public class MoviesController : Controller
    {
        private readonly CinemaDbContext _context;
        private readonly IMovieService _movieService;

        public MoviesController(CinemaDbContext context, IMovieService movieService)
        {
            _context = context;
            _movieService = movieService;
        }

        // GET: Movies (public listing)
        public async Task<IActionResult> Index()
        {
            var movies = await _movieService.GetAllMoviesAsync();
            return View(movies);
        }

        // GET: Movies/Details/5 (public details)
        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies
                .AsNoTracking()
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes)
                    .ThenInclude(st => st.Room).ThenInclude(r => r.Theater)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            ViewBag.Genres = movie.MovieGenres.Select(mg => mg.Genre.GenreName).ToList();

            return View(movie);
        }
    }
}
