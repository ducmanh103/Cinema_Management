using System;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CinemaManagement.Data
{
    public class CinemaDbContext : DbContext, IDataProtectionKeyContext
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
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // Mọi cột DateTime lưu xuống PostgreSQL đều ở dạng TIMESTAMPTZ (UTC, Kind=Utc)
            // và tự động quy đổi về giờ Việt Nam (UTC+7) khi đọc lên ứng dụng.
            configurationBuilder
                .Properties<DateTime>()
                .HaveColumnType(" timestamp with time zone")
                .HaveConversion<VietnamUtcDateTimeConverter>();

            configurationBuilder
                .Properties<DateTime?>()
                .HaveColumnType("timestamp with time zone")
                .HaveConversion<NullableVietnamUtcDateTimeConverter>();
        }

        private sealed class VietnamUtcDateTimeConverter : ValueConverter<DateTime, DateTime>
        {
            public VietnamUtcDateTimeConverter()
                : base(
                    v => VietnamTime.EnsureUtc(v),
                    v => VietnamTime.ToVietnamTime(v))
            {
            }
        }

        private sealed class NullableVietnamUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
        {
            public NullableVietnamUtcDateTimeConverter()
                : base(
                    v => v.HasValue ? VietnamTime.EnsureUtc(v.Value) : null,
                    v => v.HasValue ? VietnamTime.ToVietnamTime(v.Value) : null)
            {
            }
        }

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

            // ==================== PasswordResetToken → User (N-1) ====================
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => new { t.UserId, t.TokenHash })
                .HasDatabaseName("IX_PasswordResetToken_User_Hash");

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
                .HasFilter("\"Status\" IN ('Booked', 'Pending')")
                .HasDatabaseName("UQ_Ticket_Showtime_Seat");

            // ==================== Payment → Ticket (1-1) ====================
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Ticket)
                .WithOne(t => t.Payment)
                .HasForeignKey<Payment>(p => p.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==================== Performance & Security Indexes ====================
            var encryptedConverter = new EncryptedStringConverter();

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasQueryFilter(u => !u.IsDeleted);

                entity.Property(u => u.Email)
                    .HasMaxLength(500)
                    .HasConversion(encryptedConverter);

                entity.Property(u => u.FullName)
                    .HasMaxLength(500)
                    .HasConversion(encryptedConverter);

                entity.HasIndex(u => u.Username)
                    .IsUnique()
                    .HasDatabaseName("IX_User_Username");

                entity.HasIndex(u => u.EmailHash)
                    .HasDatabaseName("IX_User_EmailHash");

                entity.HasIndex(u => u.GoogleSubjectId)
                    .IsUnique()
                    .HasFilter("\"GoogleSubjectId\" IS NOT NULL")
                    .HasDatabaseName("UX_User_GoogleSubjectId");
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(a => a.TimestampUtc).HasDatabaseName("IX_AuditLog_TimestampUtc");
                entity.HasIndex(a => a.Action).HasDatabaseName("IX_AuditLog_Action");
                entity.HasIndex(a => a.ActorUsername).HasDatabaseName("IX_AuditLog_ActorUsername");
            });

            modelBuilder.Entity<Showtime>()
                .HasIndex(s => new { s.MovieId, s.StartTime })
                .HasDatabaseName("IX_Showtime_MovieId_StartTime");

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => new { t.UserId, t.BookingTime })
                .HasDatabaseName("IX_Ticket_UserId_BookingTime");

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
