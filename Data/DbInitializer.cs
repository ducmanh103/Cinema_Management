using System;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace CinemaManagement.Data
{
    public static class DbInitializer
    {
        /// <summary>
        /// Kiểm tra kết nối PostgreSQL và tự động nâng cấp schema idempotent:
        /// - Chuyển các cột TIMESTAMP cũ sang TIMESTAMPTZ (UTC, quy đổi từ giờ VN)
        /// - Bổ sung cột cho Google OAuth, SecurityStamp, bảng PasswordResetTokens & DataProtectionKeys
        /// - Tuỳ chọn cập nhật mật khẩu admin khởi tạo từ biến môi trường ADMIN_INITIAL_PASSWORD
        /// </summary>
        public static void Seed(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            try
            {
                if (!context.Database.CanConnect())
                {
                    Log.Warning(">>> Cảnh báo: Không thể kết nối PostgreSQL. Kiểm tra lại chuỗi kết nối hoặc container postgres_db.");
                    return;
                }

                // Tự động đồng bộ schema sang TIMESTAMPTZ + các bảng/cột bảo mật mới (Idempotent SQL)
                const string migrateSql = @"
DO $$
BEGIN
    -- 1. Mở rộng bảng Users cho Google Login & SecurityStamp
    ALTER TABLE ""Users"" ALTER COLUMN ""PasswordHash"" DROP NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'GoogleSubjectId') THEN
        ALTER TABLE ""Users"" ADD COLUMN ""GoogleSubjectId"" VARCHAR(100) NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'EmailConfirmed') THEN
        ALTER TABLE ""Users"" ADD COLUMN ""EmailConfirmed"" BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'SecurityStamp') THEN
        ALTER TABLE ""Users"" ADD COLUMN ""SecurityStamp"" VARCHAR(64) NOT NULL DEFAULT md5(random()::text || clock_timestamp()::text);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'TransactionId') THEN
        ALTER TABLE ""Payments"" ADD COLUMN ""TransactionId"" VARCHAR(100) NULL;
    END IF;

    -- 2. Chuyển đổi các cột TIMESTAMP sang TIMESTAMPTZ (nếu đang là timestamp without time zone)
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Movies' AND column_name = 'ReleaseDate' AND data_type = 'timestamp without time zone') THEN
        ALTER TABLE ""Movies"" ALTER COLUMN ""ReleaseDate"" TYPE TIMESTAMPTZ USING ""ReleaseDate"" AT TIME ZONE 'Asia/Ho_Chi_Minh';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Showtimes' AND column_name = 'StartTime' AND data_type = 'timestamp without time zone') THEN
        ALTER TABLE ""Showtimes"" ALTER COLUMN ""StartTime"" TYPE TIMESTAMPTZ USING ""StartTime"" AT TIME ZONE 'Asia/Ho_Chi_Minh';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Tickets' AND column_name = 'BookingTime' AND data_type = 'timestamp without time zone') THEN
        ALTER TABLE ""Tickets"" ALTER COLUMN ""BookingTime"" TYPE TIMESTAMPTZ USING ""BookingTime"" AT TIME ZONE 'Asia/Ho_Chi_Minh';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Tickets' AND column_name = 'HeldUntil' AND data_type = 'timestamp without time zone') THEN
        ALTER TABLE ""Tickets"" ALTER COLUMN ""HeldUntil"" TYPE TIMESTAMPTZ USING ""HeldUntil"" AT TIME ZONE 'Asia/Ho_Chi_Minh';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'PaidAt' AND data_type = 'timestamp without time zone') THEN
        ALTER TABLE ""Payments"" ALTER COLUMN ""PaidAt"" TYPE TIMESTAMPTZ USING ""PaidAt"" AT TIME ZONE 'Asia/Ho_Chi_Minh';
    END IF;
END $$;

-- 3. Bảng lưu DataProtectionKeys (giữ phiên đăng nhập & antiforgery qua các lần redeploy container)
CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""FriendlyName"" TEXT NULL,
    ""Xml"" TEXT NULL
);

-- 4. Bảng lưu token khôi phục mật khẩu (chỉ lưu SHA-256 hash)
CREATE TABLE IF NOT EXISTS ""PasswordResetTokens"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""UserId"" INT NOT NULL REFERENCES ""Users""(""UserId"") ON DELETE CASCADE,
    ""TokenHash"" VARCHAR(64) NOT NULL,
    ""ExpiresAt"" TIMESTAMPTZ NOT NULL,
    ""UsedAt"" TIMESTAMPTZ NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ""RequestIp"" VARCHAR(45) NULL
);

-- 5. Các Index bảo mật và hiệu năng bổ sung
CREATE INDEX IF NOT EXISTS ""IX_PasswordResetToken_User_Hash"" ON ""PasswordResetTokens""(""UserId"", ""TokenHash"");
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_User_GoogleSubjectId"" ON ""Users""(""GoogleSubjectId"") WHERE ""GoogleSubjectId"" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Users_Username_Lower"" ON ""Users""(LOWER(""Username""));
CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Users_Email_Lower"" ON ""Users""(LOWER(""Email"")) WHERE ""Email"" IS NOT NULL AND ""Email"" <> '';
CREATE INDEX IF NOT EXISTS ""IX_Payments_Status_Pending"" ON ""Payments""(""Status"") WHERE ""Status"" = 'Pending';
";
                context.Database.ExecuteSqlRaw(migrateSql);

                // 6. Nếu có cấu hình ADMIN_INITIAL_PASSWORD từ môi trường, cập nhật mật khẩu admin an toàn
                var adminInitPassword = config["Admin:InitialPassword"] ?? Environment.GetEnvironmentVariable("ADMIN_INITIAL_PASSWORD");
                if (!string.IsNullOrWhiteSpace(adminInitPassword) && adminInitPassword.Length >= 8)
                {
                    var adminUser = context.Users.FirstOrDefault(u => u.Username.ToLower() == "admin");
                    if (adminUser != null && (string.IsNullOrEmpty(adminUser.PasswordHash) || !BCrypt.Net.BCrypt.Verify(adminInitPassword, adminUser.PasswordHash)))
                    {
                        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminInitPassword);
                        adminUser.SecurityStamp = Guid.NewGuid().ToString("N");
                        context.SaveChanges();
                        Log.Information("🔐 Đã đồng bộ mật khẩu khởi tạo cho tài khoản admin từ biến môi trường.");
                    }
                }

                Log.Information("✅ PostgreSQL schema (TIMESTAMPTZ + Auth + DataProtection) đã sẵn sàng.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, ">>> Lỗi khi khởi tạo/nâng cấp PostgreSQL schema");
            }
        }
    }
}
