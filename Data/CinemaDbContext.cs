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

        // ==================== DbSets ====================
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Theater> Theaters { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<MovieGenre> MovieGenres { get; set; }
        public DbSet<Showtime> Showtimes { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==================== MovieGenre (N-N) ====================
            modelBuilder.Entity<MovieGenre>()
                .HasKey(mg => new { mg.MovieId, mg.GenreId });

            modelBuilder.Entity<MovieGenre>()
                .HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieGenres)
                .HasForeignKey(mg => mg.MovieId);

            modelBuilder.Entity<MovieGenre>()
                .HasOne(mg => mg.Genre)
                .WithMany(g => g.MovieGenres)
                .HasForeignKey(mg => mg.GenreId);

            // ==================== User → Role (N-1) ====================
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== Room → Theater (N-1) ====================
            modelBuilder.Entity<Room>()
                .HasOne(r => r.Theater)
                .WithMany(t => t.Rooms)
                .HasForeignKey(r => r.TheaterId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==================== Seat → Room (N-1) ====================
            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Room)
                .WithMany(r => r.Seats)
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==================== Showtime → Movie (N-1) ====================
            modelBuilder.Entity<Showtime>()
                .HasOne(st => st.Movie)
                .WithMany(m => m.Showtimes)
                .HasForeignKey(st => st.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== Showtime → Room (N-1) ====================
            modelBuilder.Entity<Showtime>()
                .HasOne(st => st.Room)
                .WithMany(r => r.Showtimes)
                .HasForeignKey(st => st.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== Ticket → Showtime (N-1) ====================
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Showtime)
                .WithMany(st => st.Tickets)
                .HasForeignKey(t => t.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== Ticket → Seat (N-1) ====================
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Seat)
                .WithMany(s => s.Tickets)
                .HasForeignKey(t => t.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== Ticket → User (N-1) ====================
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.User)
                .WithMany(u => u.Tickets)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==================== Unique: ShowtimeId + SeatId ====================
            modelBuilder.Entity<Ticket>()
                .HasIndex(t => new { t.ShowtimeId, t.SeatId })
                .IsUnique()
                .HasFilter("[Status] = N'Booked'")
                .HasDatabaseName("UQ_Ticket_Showtime_Seat");

            // ==================== Payment → Ticket (1-1) ====================
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Ticket)
                .WithOne(t => t.Payment)
                .HasForeignKey<Payment>(p => p.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==================== Decimal precision ====================
            modelBuilder.Entity<Showtime>()
                .Property(s => s.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");
        }
    }
}
