namespace CinemaManagement.Models

{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class Theater
    {
        [Key]
        public int TheaterId { get; set; }

        public string Name { get; set; }
        public string Address { get; set; }

        public virtual ICollection<Room> Rooms { get; set; }
    }

}
