namespace CinemaManagement.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class Genre
    {
        [Key]
        public int GenreId { get; set; }

        [Required]
        public string GenreName { get; set; } = string.Empty;

        public virtual ICollection<MovieGenre> MovieGenres { get; set; }
            = new List<MovieGenre>();
    }


}
