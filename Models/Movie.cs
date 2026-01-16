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
        public string Title { get; set; } = string.Empty;

        public int Duration { get; set; }

        public string? Description { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public string Status { get; set; } = "Now Showing";

        public virtual ICollection<MovieGenre> MovieGenres { get; set; }
            = new List<MovieGenre>();
    }


}
