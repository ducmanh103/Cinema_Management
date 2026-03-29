namespace CinemaManagement.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Theater
    {
        [Key]
        public int TheaterId { get; set; }

        [Required]
        [Column("Name")]   // Maps to existing 'Name' column in Theaters table
        public string TheaterName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}

