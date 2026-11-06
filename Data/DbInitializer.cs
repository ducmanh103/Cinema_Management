using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CinemaManagement.Data
{
    public static class DbInitializer
    {
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

                // Tách từng bước DDL độc lập để đảm bảo bảng DataProtectionKeys và các cột luôn được tạo thành công
                string[] statements =
                {
                    @"CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""FriendlyName"" TEXT NULL,
                        ""Xml"" TEXT NULL
                    );",

                    @"ALTER TABLE ""Users"" ALTER COLUMN ""PasswordHash"" DROP NOT NULL;",

                    @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""GoogleSubjectId"" VARCHAR(100) NULL;",
                    @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""EmailConfirmed"" BOOLEAN NOT NULL DEFAULT FALSE;",
                    @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""SecurityStamp"" VARCHAR(64) NOT NULL DEFAULT md5(random()::text || clock_timestamp()::text);",
                    @"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""TransactionId"" VARCHAR(100) NULL;",

                    @"CREATE TABLE IF NOT EXISTS ""PasswordResetTokens"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""UserId"" INT NOT NULL REFERENCES ""Users""(""UserId"") ON DELETE CASCADE,
                        ""TokenHash"" VARCHAR(64) NOT NULL,
                        ""ExpiresAt"" TIMESTAMPTZ NOT NULL,
                        ""UsedAt"" TIMESTAMPTZ NULL,
                        ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ""RequestIp"" VARCHAR(45) NULL
                    );",

                    @"DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Movies' AND column_name = 'ReleaseDate' AND data_type IN ('date', 'timestamp without time zone')) THEN
                            ALTER TABLE ""Movies"" ALTER COLUMN ""ReleaseDate"" TYPE TIMESTAMPTZ USING (""ReleaseDate""::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh');
                        END IF;
                    END $$;",

                    @"DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Showtimes' AND column_name = 'StartTime' AND data_type = 'timestamp without time zone') THEN
                            ALTER TABLE ""Showtimes"" ALTER COLUMN ""StartTime"" TYPE TIMESTAMPTZ USING (""StartTime"" AT TIME ZONE 'Asia/Ho_Chi_Minh');
                        END IF;
                    END $$;",

                    @"DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Tickets' AND column_name = 'BookingTime' AND data_type = 'timestamp without time zone') THEN
                            ALTER TABLE ""Tickets"" ALTER COLUMN ""BookingTime"" TYPE TIMESTAMPTZ USING (""BookingTime"" AT TIME ZONE 'Asia/Ho_Chi_Minh');
                        END IF;
                    END $$;",

                    @"DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Tickets' AND column_name = 'HeldUntil' AND data_type = 'timestamp without time zone') THEN
                            ALTER TABLE ""Tickets"" ALTER COLUMN ""HeldUntil"" TYPE TIMESTAMPTZ USING (""HeldUntil"" AT TIME ZONE 'Asia/Ho_Chi_Minh');
                        END IF;
                    END $$;",

                    @"DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'PaidAt' AND data_type = 'timestamp without time zone') THEN
                            ALTER TABLE ""Payments"" ALTER COLUMN ""PaidAt"" TYPE TIMESTAMPTZ USING (""PaidAt"" AT TIME ZONE 'Asia/Ho_Chi_Minh');
                        END IF;
                    END $$;",

                    @"CREATE INDEX IF NOT EXISTS ""IX_PasswordResetToken_User_Hash"" ON ""PasswordResetTokens""(""UserId"", ""TokenHash"");",
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""UX_User_GoogleSubjectId"" ON ""Users""(""GoogleSubjectId"") WHERE ""GoogleSubjectId"" IS NOT NULL;",
                    @"CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Users_Username_Lower"" ON ""Users""(LOWER(""Username""));",
                    @"CREATE INDEX IF NOT EXISTS ""IX_Payments_Status_Pending"" ON ""Payments""(""Status"") WHERE ""Status"" = 'Pending';"
                };

                foreach (var sql in statements)
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw(sql);
                    }
                    catch (Exception stepEx)
                    {
                        Log.Warning(stepEx, "Bỏ qua bước DDL không tương thích: {SqlPreview}", sql.Substring(0, Math.Min(60, sql.Length)));
                    }
                }

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
