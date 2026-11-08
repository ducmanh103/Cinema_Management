namespace CinemaManagement.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? ActorUsername { get; set; }

        public int? ActorUserId { get; set; }

        [StringLength(50)]
        public string? ActorRole { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(100)]
        public string? EntityName { get; set; }

        [StringLength(50)]
        public string? EntityId { get; set; }

        public string? Details { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(200)]
        public string? Path { get; set; }
    }
}

