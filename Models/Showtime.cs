namespace CinemaManagement.Models

{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Showtime
    {
        public int ShowtimeId { get; set; }
        public DateTime StartTime { get; set; }
        public decimal Price { get; set; }

        [ForeignKey("Movie")]
        public int MovieId { get; set; }
        public virtual Movie Movie { get; set; }

        [ForeignKey("Room")]
        public int RoomId { get; set; }
        public virtual Room Room { get; set; }

        public virtual ICollection<Ticket> Tickets { get; set; }
    }

}
