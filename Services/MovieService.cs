using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Services
{
    public class MovieService : IMovieService
    {
        private readonly CinemaDbContext _context;

        public MovieService(CinemaDbContext context) => _context = context;

        public async Task<List<MovieDto>> GetAllMoviesAsync()
        {
            return await _context.Movies
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
                    Status = m.Status,
                    Genres = m.MovieGenres.Select(mg => mg.Genre != null ? mg.Genre.GenreName : "").ToList()
                })
                .ToListAsync();
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
                Status = dto.Status
            };

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            foreach (var genreId in dto.GenreIds)
            {
                _context.MovieGenres.Add(new MovieGenre { MovieId = movie.MovieId, GenreId = genreId });
            }
            await _context.SaveChangesAsync();

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
            movie.Status = dto.Status;

            // Delete old genres directly in DB (optimized)
            await _context.MovieGenres.Where(mg => mg.MovieId == id).ExecuteDeleteAsync();

            foreach (var genreId in dto.GenreIds)
            {
                _context.MovieGenres.Add(new MovieGenre { MovieId = id, GenreId = genreId });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMovieAsync(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return false;
            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
