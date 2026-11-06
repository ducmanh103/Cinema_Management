namespace CinemaManagement.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        /// <summary>
        /// Mã băm SHA-256 (Hex 64 ký tự) của token khôi phục mật khẩu. Không lưu token thô.
        /// </summary>
        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(45)]
        public string? RequestIp { get; set; }
    }
}

