using System;
using CinemaManagement.Data;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CinemaManagement.Services
{
    public class MovieService : IMovieService
    {
        private const string AllMoviesCacheKey = "cinema_all_movies_v1";
        private readonly CinemaDbContext _context;
        private readonly IMemoryCache _cache;

        public MovieService(CinemaDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<MovieDto>> GetAllMoviesAsync()
        {
            if (_cache.TryGetValue(AllMoviesCacheKey, out List<MovieDto>? cached) && cached != null)
                return cached;

            var now = VietnamTime.Now;
            var movies = await _context.Movies
                .AsNoTracking()
                .Select(m => new MovieDto
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Duration = m.Duration,
                    Description = m.Description,
                    ReleaseDate = m.ReleaseDate,
                    PosterUrl = m.PosterUrl,
                    BannerUrl = m.BannerUrl,
                    TrailerUrl = m.TrailerUrl,
                    Status = m.Status,
                    Genres = m.MovieGenres.Select(mg => mg.Genre != null ? mg.Genre.GenreName : "").ToList(),
                    HasShowtimes = m.Showtimes.Any(s => s.StartTime >= now),
                    BookedTicketCount = m.Showtimes.SelectMany(s => s.Tickets).Count(t => t.Status == "Booked")
                })
                .ToListAsync();

            var sorted = movies
                .OrderBy(m => m.Status == "Now Showing" ? 0 : (m.Status == "Coming Soon" ? 1 : 2))
                .ThenByDescending(m => m.HasShowtimes)
                .ThenByDescending(m => m.BookedTicketCount)
                .ThenByDescending(m => m.MovieId)
                .ToList();

            _cache.Set(AllMoviesCacheKey, sorted, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                Size = 1
            });

            return sorted;
        }

        public async Task<MovieDto?> GetMovieByIdAsync(int id)
        {
            return await _context.Movies
                .AsNoTracking()
                .Where(m => m.MovieId == id)
                .Select(m => new MovieDto
                {
                    MovieId = m.MovieId,
                    Title = m.Title,
                    Duration = m.Duration,
                    Description = m.Description,
                    ReleaseDate = m.ReleaseDate,
                    PosterUrl = m.PosterUrl,
                    BannerUrl = m.BannerUrl,
                    TrailerUrl = m.TrailerUrl,
                    Status = m.Status,
                    Genres = m.MovieGenres.Select(mg => mg.Genre != null ? mg.Genre.GenreName : "").ToList()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<MovieDto> CreateMovieAsync(CreateMovieDto dto)
        {
            var movie = new Movie
            {
                Title = dto.Title,
                Duration = dto.Duration,
                Description = dto.Description,
                ReleaseDate = dto.ReleaseDate,
                PosterUrl = dto.PosterUrl,
                BannerUrl = dto.BannerUrl,
                TrailerUrl = dto.TrailerUrl,
                Status = dto.Status
            };

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            foreach (var genreId in dto.GenreIds)
            {
                _context.MovieGenres.Add(new MovieGenre { MovieId = movie.MovieId, GenreId = genreId });
            }
            await _context.SaveChangesAsync();
            _cache.Remove(AllMoviesCacheKey);

            return (await GetMovieByIdAsync(movie.MovieId))!;
        }

        public async Task<bool> UpdateMovieAsync(int id, CreateMovieDto dto)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return false;

            movie.Title = dto.Title;
            movie.Duration = dto.Duration;
            movie.Description = dto.Description;
            movie.ReleaseDate = dto.ReleaseDate;
            movie.PosterUrl = dto.PosterUrl;
            movie.BannerUrl = dto.BannerUrl;
            movie.TrailerUrl = dto.TrailerUrl;
            movie.Status = dto.Status;

            await _context.MovieGenres.Where(mg => mg.MovieId == id).ExecuteDeleteAsync();

            foreach (var genreId in dto.GenreIds)
            {
                _context.MovieGenres.Add(new MovieGenre { MovieId = id, GenreId = genreId });
            }

            await _context.SaveChangesAsync();
            _cache.Remove(AllMoviesCacheKey);
            return true;
        }

        public async Task<bool> DeleteMovieAsync(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return false;
            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();
            _cache.Remove(AllMoviesCacheKey);
            return true;
        }
    }
}
