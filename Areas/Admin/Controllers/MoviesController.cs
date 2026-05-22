using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MoviesController : Controller
    {
        private readonly CinemaDbContext _context;

        public MoviesController(CinemaDbContext context) => _context = context;

        // GET: /Admin/Movies
        public async Task<IActionResult> Index()
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

        // GET: /Admin/Movies/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies
                .AsNoTracking()
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes)
                    .ThenInclude(st => st.Room).ThenInclude(r => r.Theater)
                .Include(m => m.Showtimes)
                    .ThenInclude(st => st.Tickets).ThenInclude(t => t.Payment)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            ViewBag.Genres = movie.MovieGenres.Select(mg => mg.Genre.GenreName).ToList();

            var bookedTickets = movie.Showtimes
                .SelectMany(st => st.Tickets)
                .Where(t => t.Status == "Booked")
                .ToList();

            ViewBag.TotalTickets = bookedTickets.Count;
            ViewBag.TotalRevenue = bookedTickets
                .Where(t => t.Payment?.Status == "Completed")
                .Sum(t => t.Payment?.Amount ?? 0m);

            return View(movie);
        }

        // GET: /Admin/Movies/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Genres = await _context.Genres.ToListAsync();
            return View();
        }

        // POST: /Admin/Movies/Create
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

                TempData["Success"] = $"Đã thêm phim '{movie.Title}' thành công.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Genres = await _context.Genres.ToListAsync();
            return View(movie);
        }

        // GET: /Admin/Movies/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            ViewBag.Genres = await _context.Genres.ToListAsync();
            ViewBag.SelectedGenres = movie.MovieGenres.Select(mg => mg.GenreId).ToList();

            return View(movie);
        }

        // POST: /Admin/Movies/Edit/5
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
                TempData["Success"] = $"Đã cập nhật phim '{movie.Title}'.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Genres = await _context.Genres.ToListAsync();
            return View(movie);
        }

        // GET: /Admin/Movies/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // POST: /Admin/Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie != null)
            {
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xoá phim thành công.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
