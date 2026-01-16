namespace CinemaManagement.Models

{
    using System;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Ticket
    {
        public int TicketId { get; set; }
        public string SeatNumber { get; set; }
        public DateTime BookingTime { get; set; }
        public string Status { get; set; }

        [ForeignKey("Showtime")]
        public int ShowtimeId { get; set; }
        public virtual Showtime Showtime { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }
    }

}
