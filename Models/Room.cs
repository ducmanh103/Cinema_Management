namespace CinemaManagement.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Room
    {
        [Key]
        public int RoomId { get; set; }

        [Required]
        public string RoomName { get; set; } = string.Empty;
        public int SeatCount { get; set; }

        [ForeignKey("Theater")]
        public int TheaterId { get; set; }
        public virtual Theater Theater { get; set; } = null!;

        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }
}
