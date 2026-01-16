using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Data;
using CinemaManagement.Models;

namespace CinemaManagement.Controllers
{
    public class MoviesController : Controller
    {
        private readonly CinemaDbContext _context;

        public MoviesController(CinemaDbContext context)
        {
            _context = context;
        }

        // GET: Movies
        public async Task<IActionResult> Index()
        {
            var movies = await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .ToListAsync();

            return View(movies);
        }

        // GET: Movies/Create
        public IActionResult Create()
        {
            ViewBag.Genres = _context.Genres.ToList();
            return View();
        }

        // POST: Movies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Movie movie, int[] selectedGenres)
        {
            if (ModelState.IsValid)
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();

                if (selectedGenres != null)
                {
                    foreach (var genreId in selectedGenres)
                    {
                        _context.MovieGenres.Add(new MovieGenre
                        {
                            MovieId = movie.MovieId,
                            GenreId = genreId
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Genres = _context.Genres.ToList();
            return View(movie);
        }

        // GET: Movies/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            ViewBag.Genres = _context.Genres.ToList();
            ViewBag.SelectedGenres = movie.MovieGenres.Select(mg => mg.GenreId).ToList();

            return View(movie);
        }

        // POST: Movies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Movie movie, int[] selectedGenres)
        {
            if (id != movie.MovieId) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(movie);

                var oldGenres = _context.MovieGenres.Where(mg => mg.MovieId == id);
                _context.MovieGenres.RemoveRange(oldGenres);

                if (selectedGenres != null)
                {
                    foreach (var genreId in selectedGenres)
                    {
                        _context.MovieGenres.Add(new MovieGenre
                        {
                            MovieId = id,
                            GenreId = genreId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Genres = _context.Genres.ToList();
            return View(movie);
        }

        // GET: Movies/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // POST: Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie != null)
            {
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
