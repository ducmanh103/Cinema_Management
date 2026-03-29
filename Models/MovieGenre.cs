namespace CinemaManagement.Models

{
    using System.ComponentModel.DataAnnotations.Schema;

    public class MovieGenre
    {
        [ForeignKey("Movie")]
        public int MovieId { get; set; }
        public virtual Movie Movie { get; set; } = null!;

        [ForeignKey("Genre")]
        public int GenreId { get; set; }
        public virtual Genre Genre { get; set; } = null!;
    }

}
