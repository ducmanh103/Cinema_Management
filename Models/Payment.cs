namespace CinemaManagement.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash"; // Cash, Card, Momo
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed
        public DateTime PaidAt { get; set; } = DateTime.Now;

        [ForeignKey("Ticket")]
        public int TicketId { get; set; }
        public virtual Ticket Ticket { get; set; } = null!;
    }
}
