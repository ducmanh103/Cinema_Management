namespace CinemaManagement.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Seat
    {
        [Key]
        public int SeatId { get; set; }

        [Required]
        public string SeatNumber { get; set; } = string.Empty; // e.g. "A1", "B2"

        public string SeatType { get; set; } = "Standard"; // Standard, VIP

        [ForeignKey("Room")]
        public int RoomId { get; set; }
        public virtual Room Room { get; set; } = null!;

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
