namespace CinemaManagement.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class Role
    {
        [Key]
        public int RoleId { get; set; }

        [Required]
        public string RoleName { get; set; }

        public virtual ICollection<User> Users { get; set; }
    }

}
