namespace CinemaManagement.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        public string? PasswordHash { get; set; } = string.Empty;

        public string? FullName { get; set; }

        [StringLength(500)]
        public string? Email { get; set; }

        [StringLength(64)]
        public string? EmailHash { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        [StringLength(100)]
        public string? GoogleSubjectId { get; set; }

        public bool EmailConfirmed { get; set; } = false;

        [StringLength(64)]
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

        public string Status { get; set; } = "Active"; // Active, Inactive

        public int RoleId { get; set; }
        public virtual Role Role { get; set; } = null!;

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    }
}
