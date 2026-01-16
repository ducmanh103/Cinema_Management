namespace CinemaManagement.Models

{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Room
    {
        [Key]
        public int RoomId { get; set; }

        public string RoomName { get; set; }
        public int SeatCount { get; set; }

        [ForeignKey("Theater")]
        public int TheaterId { get; set; }
        public virtual Theater Theater { get; set; }

        public virtual ICollection<Showtime> Showtimes { get; set; }
    }

}
