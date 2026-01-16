namespace CinemaManagement.Models

{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string? FullName { get; set; }
        public string? Email { get; set; }

        public string Status { get; set; } = "Active";

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }


}
