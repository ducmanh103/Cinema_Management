using Microsoft.EntityFrameworkCore;
using CinemaManagement.Models;

namespace CinemaManagement.Data
{
    public class CinemaDbContext : DbContext
    {
        public CinemaDbContext(DbContextOptions<CinemaDbContext> options)
            : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<MovieGenre> MovieGenres { get; set; }
        public DbSet<Theater> Theaters { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Showtime> Showtimes { get; set; }
        public DbSet<Ticket> Tickets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MovieGenre>()
                .HasKey(mg => new { mg.MovieId, mg.GenreId });

            base.OnModelCreating(modelBuilder);
        }
    }
}
