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
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Select(m => ToDto(m))
                .ToListAsync();
        }

        public async Task<MovieDto?> GetMovieByIdAsync(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            return movie == null ? null : ToDto(movie);
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

            // Replace genres
            var oldGenres = _context.MovieGenres.Where(mg => mg.MovieId == id);
            _context.MovieGenres.RemoveRange(oldGenres);
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

        private static MovieDto ToDto(Movie m) => new()
        {
            MovieId = m.MovieId,
            Title = m.Title,
            Duration = m.Duration,
            Description = m.Description,
            ReleaseDate = m.ReleaseDate,
            PosterUrl = m.PosterUrl,
            BannerUrl = m.BannerUrl,
            Status = m.Status,
            Genres = m.MovieGenres.Select(mg => mg.Genre?.GenreName ?? "").ToList()
        };
    }
}
