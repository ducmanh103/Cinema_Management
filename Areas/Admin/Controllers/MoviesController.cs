using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CinemaManagement.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MoviesController : Controller
    {
        private const string AllMoviesCacheKey = "cinema_all_movies_v1";
        private const string LandingSnapshotCacheKey = "cinema_home_landing_snapshot_v1";

        private readonly CinemaDbContext _context;
        private readonly IFileStorageService _fileStorage;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(
            CinemaDbContext context,
            IFileStorageService fileStorage,
            IMemoryCache cache,
            ILogger<MoviesController> logger)
        {
            _context = context;
            _fileStorage = fileStorage;
            _cache = cache;
            _logger = logger;
        }

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
                    BannerUrl = m.BannerUrl,
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
            ViewBag.Genres = await _context.Genres.AsNoTracking().ToListAsync();
            return View(new Movie { Status = "Now Showing" });
        }

        // POST: /Admin/Movies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Movie movie,
            int[] selectedGenres,
            IFormFile? posterFile,
            IFormFile? bannerFile,
            bool downloadRemotePoster = false,
            bool downloadRemoteBanner = false)
        {
            // Xử lý tải ảnh Poster
            if (posterFile != null && posterFile.Length > 0)
            {
                try
                {
                    var savedPoster = await _fileStorage.SaveFileAsync(posterFile, "movies");
                    if (!string.IsNullOrEmpty(savedPoster))
                    {
                        movie.PosterUrl = savedPoster;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("PosterUrl", ex.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(movie.PosterUrl) && downloadRemotePoster)
            {
                var savedPoster = await _fileStorage.DownloadAndSaveRemoteImageAsync(movie.PosterUrl.Trim(), "movies");
                if (!string.IsNullOrEmpty(savedPoster))
                {
                    movie.PosterUrl = savedPoster;
                }
            }

            // Xử lý tải ảnh Banner
            if (bannerFile != null && bannerFile.Length > 0)
            {
                try
                {
                    var savedBanner = await _fileStorage.SaveFileAsync(bannerFile, "movies");
                    if (!string.IsNullOrEmpty(savedBanner))
                    {
                        movie.BannerUrl = savedBanner;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("BannerUrl", ex.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(movie.BannerUrl) && downloadRemoteBanner)
            {
                var savedBanner = await _fileStorage.DownloadAndSaveRemoteImageAsync(movie.BannerUrl.Trim(), "movies");
                if (!string.IsNullOrEmpty(savedBanner))
                {
                    movie.BannerUrl = savedBanner;
                }
            }

            if (string.IsNullOrWhiteSpace(movie.Status))
            {
                movie.Status = "Now Showing";
            }

            if (ModelState.IsValid)
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();

                if (selectedGenres != null && selectedGenres.Length > 0)
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

                InvalidateMovieCaches();

                TempData["Success"] = $"Đã thêm phim '{movie.Title}' thành công vào hệ thống!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Genres = await _context.Genres.AsNoTracking().ToListAsync();
            return View(movie);
        }

        // GET: /Admin/Movies/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            ViewBag.Genres = await _context.Genres.AsNoTracking().ToListAsync();
            ViewBag.SelectedGenres = movie.MovieGenres.Select(mg => mg.GenreId).ToList();

            return View(movie);
        }

        // POST: /Admin/Movies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Movie movie,
            int[] selectedGenres,
            IFormFile? posterFile,
            IFormFile? bannerFile,
            bool downloadRemotePoster = false,
            bool downloadRemoteBanner = false)
        {
            if (id != movie.MovieId) return NotFound();

            var existingMovie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (existingMovie == null) return NotFound();

            // 1. Cập nhật ảnh Poster
            if (posterFile != null && posterFile.Length > 0)
            {
                try
                {
                    var newPoster = await _fileStorage.SaveFileAsync(posterFile, "movies");
                    if (!string.IsNullOrEmpty(newPoster))
                    {
                        if (!string.IsNullOrEmpty(existingMovie.PosterUrl) && existingMovie.PosterUrl != newPoster)
                        {
                            _fileStorage.DeleteFile(existingMovie.PosterUrl);
                        }
                        existingMovie.PosterUrl = newPoster;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("PosterUrl", ex.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(movie.PosterUrl) && movie.PosterUrl.Trim() != existingMovie.PosterUrl)
            {
                var trimmedPoster = movie.PosterUrl.Trim();
                if (downloadRemotePoster && (trimmedPoster.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmedPoster.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var savedPoster = await _fileStorage.DownloadAndSaveRemoteImageAsync(trimmedPoster, "movies");
                    existingMovie.PosterUrl = !string.IsNullOrEmpty(savedPoster) ? savedPoster : trimmedPoster;
                }
                else
                {
                    existingMovie.PosterUrl = trimmedPoster;
                }
            }
            else if (string.IsNullOrWhiteSpace(movie.PosterUrl) && posterFile == null)
            {
                // Cho phép xóa poster nếu người dùng chủ động xóa trắng
                existingMovie.PosterUrl = null;
            }

            // 2. Cập nhật ảnh Banner
            if (bannerFile != null && bannerFile.Length > 0)
            {
                try
                {
                    var newBanner = await _fileStorage.SaveFileAsync(bannerFile, "movies");
                    if (!string.IsNullOrEmpty(newBanner))
                    {
                        if (!string.IsNullOrEmpty(existingMovie.BannerUrl) && existingMovie.BannerUrl != newBanner)
                        {
                            _fileStorage.DeleteFile(existingMovie.BannerUrl);
                        }
                        existingMovie.BannerUrl = newBanner;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("BannerUrl", ex.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(movie.BannerUrl) && movie.BannerUrl.Trim() != existingMovie.BannerUrl)
            {
                var trimmedBanner = movie.BannerUrl.Trim();
                if (downloadRemoteBanner && (trimmedBanner.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmedBanner.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var savedBanner = await _fileStorage.DownloadAndSaveRemoteImageAsync(trimmedBanner, "movies");
                    existingMovie.BannerUrl = !string.IsNullOrEmpty(savedBanner) ? savedBanner : trimmedBanner;
                }
                else
                {
                    existingMovie.BannerUrl = trimmedBanner;
                }
            }
            else if (string.IsNullOrWhiteSpace(movie.BannerUrl) && bannerFile == null)
            {
                // Cho phép xóa banner nếu người dùng chủ động xóa trắng
                existingMovie.BannerUrl = null;
            }

            if (ModelState.IsValid)
            {
                existingMovie.Title = movie.Title.Trim();
                existingMovie.Duration = movie.Duration;
                existingMovie.Description = movie.Description;
                existingMovie.ReleaseDate = movie.ReleaseDate;
                existingMovie.Status = string.IsNullOrWhiteSpace(movie.Status) ? "Now Showing" : movie.Status;

                // Cập nhật thể loại
                _context.MovieGenres.RemoveRange(existingMovie.MovieGenres);
                if (selectedGenres != null && selectedGenres.Length > 0)
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
                InvalidateMovieCaches();

                TempData["Success"] = $"Đã cập nhật phim '{existingMovie.Title}' thành công.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Genres = await _context.Genres.AsNoTracking().ToListAsync();
            ViewBag.SelectedGenres = selectedGenres?.ToList() ?? new List<int>();
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
                _fileStorage.DeleteFile(movie.PosterUrl);
                _fileStorage.DeleteFile(movie.BannerUrl);

                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
                InvalidateMovieCaches();

                TempData["Success"] = $"Đã xoá phim '{movie.Title}' thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        private void InvalidateMovieCaches()
        {
            _cache.Remove(AllMoviesCacheKey);
            _cache.Remove(LandingSnapshotCacheKey);
        }
    }
}
