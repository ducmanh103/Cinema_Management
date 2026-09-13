namespace CinemaManagement.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Ticket
    {
        [Key]
        public int TicketId { get; set; }

        public DateTime BookingTime { get; set; } = DateTime.Now;
        public DateTime? HeldUntil { get; set; }
        public string Status { get; set; } = "Booked"; // Pending, Booked, Cancelled, Expired

        [ForeignKey("Showtime")]
        public int ShowtimeId { get; set; }
        public virtual Showtime Showtime { get; set; } = null!;

        [ForeignKey("Seat")]
        public int SeatId { get; set; }
        public virtual Seat Seat { get; set; } = null!;

        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        // Navigation ngược từ Ticket → Payment (1-1)
        public virtual Payment? Payment { get; set; }
    }
}
