namespace CinemaManagement.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class Movie
    {
        [Key]
        public int MovieId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public int Duration { get; set; } // minutes

        public string? Description { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public string? PosterUrl { get; set; }
        public string? BannerUrl { get; set; }

        public string Status { get; set; } = "Now Showing"; // Now Showing, Coming Soon, Ended

        public virtual ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
        public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }
}
