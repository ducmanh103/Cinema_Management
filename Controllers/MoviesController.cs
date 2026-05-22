using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Data;

namespace CinemaManagement.Controllers
{
    public class MoviesController : Controller
    {
        private readonly CinemaDbContext _context;

        public MoviesController(CinemaDbContext context)
        {
            _context = context;
        }

        // GET: Movies (public listing)
        public async Task<IActionResult> Index()
        {
            var movies = await _context.Movies
                .AsNoTracking()
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .ToListAsync();

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
