namespace CinemaManagement.Models
{
    /// <summary>
    /// Các hằng số trạng thái dùng chung trong hệ thống.
    /// Tránh hardcode magic strings rải rác trong code.
    /// </summary>
    public static class StatusConstants
    {
        // Ticket statuses
        public static class Ticket
        {
            public const string Booked = "Booked";
            public const string Cancelled = "Cancelled";
        }

        // Payment statuses
        public static class Payment
        {
            public const string Pending = "Pending";
            public const string Completed = "Completed";
            public const string Failed = "Failed";
            public const string Refunded = "Refunded";
        }

        // Movie statuses
        public static class Movie
        {
            public const string NowShowing = "Now Showing";
            public const string ComingSoon = "Coming Soon";
            public const string Ended = "Ended";
        }

        // User statuses
        public static class User
        {
            public const string Active = "Active";
            public const string Inactive = "Inactive";
        }

        // Payment methods
        public static class PaymentMethod
        {
            public const string Cash = "Cash";
            public const string VnPay = "VnPay";
        }

        // Seat types
        public static class SeatType
        {
            public const string Standard = "Standard";
            public const string VIP = "VIP";
        }

        // Roles
        public static class Role
        {
            public const string Admin = "Admin";
            public const string Staff = "Staff";
            public const string Customer = "Customer";
        }
    }
}
