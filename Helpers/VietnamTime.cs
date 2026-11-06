using System;

namespace CinemaManagement.Helpers
{
    /// <summary>
    /// Helper chuẩn hoá thời gian:
    /// - Lưu trữ trong PostgreSQL: TIMESTAMPTZ (UTC, DateTimeKind.Utc)
    /// - Hiển thị & nghiệp vụ rạp tại Việt Nam: UTC+7 (cố định +07:00, không có DST)
    /// </summary>
    public static class VietnamTime
    {
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        /// <summary>Giờ UTC hiện tại (DateTimeKind.Utc) để lưu DB.</summary>
        public static DateTime UtcNow => DateTime.UtcNow;

        /// <summary>Giờ Việt Nam hiện tại (UTC+7, Kind=Unspecified) để hiển thị/so sánh giao diện.</summary>
        public static DateTime Now => DateTime.SpecifyKind(DateTime.UtcNow.Add(VietnamOffset), DateTimeKind.Unspecified);

        /// <summary>Ngày hôm nay theo múi giờ Việt Nam (00:00:00 VN).</summary>
        public static DateTime Today => Now.Date;

        /// <summary>
        /// Đảm bảo DateTime có Kind=Utc trước khi ghi vào cột TIMESTAMPTZ của PostgreSQL.
        /// Nếu Kind=Unspecified, coi giá trị đó là giờ Việt Nam (UTC+7) và trừ 7 giờ để về UTC.
        /// </summary>
        public static DateTime EnsureUtc(DateTime value)
        {
            if (value == default)
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Subtract(VietnamOffset), DateTimeKind.Utc)
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
                DateTimeKind.Utc => DateTime.SpecifyKind(value.Add(VietnamOffset), DateTimeKind.Unspecified),
                DateTimeKind.Local => DateTime.SpecifyKind(value.ToUniversalTime().Add(VietnamOffset), DateTimeKind.Unspecified),
                _ => value
            };
        }
    }
}
