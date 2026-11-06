using System;

namespace CinemaManagement.Helpers
{
    /// <summary>
    /// Helper chuẩn hoá thời gian:
    /// - Lưu trữ trong PostgreSQL: TIMESTAMPTZ (UTC, DateTimeKind.Utc)
    /// - Hiển thị & nghiệp vụ rạp tại Việt Nam: UTC+7 (Asia/Ho_Chi_Minh)
    /// </summary>
    public static class VietnamTime
    {
        private static readonly TimeZoneInfo _tz = ResolveVietnamTimeZone();

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "VN", TimeSpan.FromHours(7), "Vietnam Time", "Vietnam Standard Time");
                }
            }
        }

        /// <summary>Giờ UTC hiện tại (DateTimeKind.Utc) để lưu DB.</summary>
        public static DateTime UtcNow => DateTime.UtcNow;

        /// <summary>Giờ Việt Nam hiện tại (UTC+7, Kind=Unspecified) để hiển thị/so sánh giao diện.</summary>
        public static DateTime Now => ToVietnamTime(DateTime.UtcNow);

        /// <summary>Ngày hôm nay theo múi giờ Việt Nam (00:00:00 VN).</summary>
        public static DateTime Today => Now.Date;

        /// <summary>
        /// Đảm bảo DateTime có Kind=Utc trước khi ghi vào cột TIMESTAMPTZ của PostgreSQL.
        /// Nếu Kind=Unspecified, coi giá trị đó là giờ Việt Nam (UTC+7) và đổi sang UTC.
        /// </summary>
        public static DateTime EnsureUtc(DateTime value)
        {
            if (value == default)
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), _tz)
            };
        }

        /// <summary>
        /// Chuyển DateTime (từ DB UTC hoặc Local) sang giờ Việt Nam (UTC+7, Kind=Unspecified).
        /// Nếu đã là Unspecified (đã ở giờ VN) thì giữ nguyên (idempotent).
        /// </summary>
        public static DateTime ToVietnamTime(this DateTime value)
        {
            if (value == default)
                return value;

            return value.Kind switch
            {
                DateTimeKind.Utc => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value, _tz), DateTimeKind.Unspecified),
                DateTimeKind.Local => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), _tz), DateTimeKind.Unspecified),
                _ => value
            };
        }
    }
}
