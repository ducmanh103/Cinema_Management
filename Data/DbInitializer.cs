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
            var piiService = scope.ServiceProvider.GetRequiredService<CinemaManagement.Services.IPiiEncryptionService>();

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
                    @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""EmailHash"" VARCHAR(64) NULL;",
                    @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""IsDeleted"" BOOLEAN NOT NULL DEFAULT FALSE;",
                    @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""DeletedAt"" TIMESTAMPTZ NULL;",
                    @"ALTER TABLE ""Users"" ALTER COLUMN ""Email"" TYPE VARCHAR(500);",
                    @"ALTER TABLE ""Users"" ALTER COLUMN ""FullName"" TYPE VARCHAR(500);",
                    @"CREATE INDEX IF NOT EXISTS ""IX_User_EmailHash"" ON ""Users""(""EmailHash"");",
                    @"ALTER TABLE ""Payments"" ADD COLUMN IF NOT EXISTS ""TransactionId"" VARCHAR(100) NULL;",
                    @"ALTER TABLE ""Movies"" ADD COLUMN IF NOT EXISTS ""TrailerUrl"" VARCHAR(500) NULL;",

                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/_inKs4eeHiI' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Kung Fu Panda%' OR ""Title"" ILIKE '%Panda 4%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/Way9Dexny3w' WHERE ""TrailerUrl"" IS NULL AND ""Title"" ILIKE '%Dune%';",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/W8VekFsJKZc' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE 'Mai%' OR ""Title"" ILIKE '%phim mai%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/ag5d9Su53nA' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Quật Mộ%' OR ""Title"" ILIKE '%Exhuma%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/lV1OOlGwExM' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Godzilla%' OR ""Title"" ILIKE '%Kong%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/a8Gx8wiNbs8' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Avatar 3%' OR ""Title"" ILIKE '%Tro Tàn%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/d9MyW72ELq0' WHERE ""TrailerUrl"" IS NULL AND ""Title"" = 'Avatar';",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/73_1biulkYk' WHERE ""TrailerUrl"" IS NULL AND ""Title"" ILIKE '%Deadpool%';",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/32RAq6JzguY' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Fast%' OR ""Title"" ILIKE '%Furious%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/AbkYj4X_9g4' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Săn Ma%' OR ""Title"" ILIKE '%Ghostbusters%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/4rgYUipGJNo' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Giác Đấu%' OR ""Title"" ILIKE '%Gladiator%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/qEVUtrk8_B4' WHERE ""TrailerUrl"" IS NULL AND ""Title"" ILIKE '%John Wick%';",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/o17MF9vnabg' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Mufasa%' OR ""Title"" ILIKE '%Vua Sư Tử%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/YPY7J-flzE8' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Câm Lặng%' OR ""Title"" ILIKE '%Quiet Place%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/qSu6i2iFMO0' WHERE ""TrailerUrl"" IS NULL AND ""Title"" ILIKE '%Sonic%';",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/shW9i6k8cB0' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Nhện%' OR ""Title"" ILIKE '%Spider%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/I10q6fj4n3U' WHERE ""TrailerUrl"" IS NULL AND ""Title"" ILIKE '%Titanic%';",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/_Z3QKkl1WyM' WHERE ""TrailerUrl"" IS NULL AND (""Title"" ILIKE '%Wakanda%' OR ""Title"" ILIKE '%Báo Đen%');",
                    @"UPDATE ""Movies"" SET ""TrailerUrl"" = 'https://www.youtube.com/embed/jWM0ct-OLsM' WHERE ""TrailerUrl"" IS NULL AND ""Title"" ILIKE '%Zootopia%';",

                    @"CREATE TABLE IF NOT EXISTS ""AuditLogs"" (
                        ""Id"" BIGSERIAL PRIMARY KEY,
                        ""TimestampUtc"" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ""ActorUsername"" VARCHAR(100) NULL,
                        ""ActorUserId"" INT NULL,
                        ""ActorRole"" VARCHAR(50) NULL,
                        ""Action"" VARCHAR(100) NOT NULL,
                        ""EntityName"" VARCHAR(100) NULL,
                        ""EntityId"" VARCHAR(50) NULL,
                        ""Details"" TEXT NULL,
                        ""IpAddress"" VARCHAR(45) NULL,
                        ""Path"" VARCHAR(200) NULL
                    );",

                    @"CREATE INDEX IF NOT EXISTS ""IX_AuditLog_TimestampUtc"" ON ""AuditLogs""(""TimestampUtc"");",
                    @"CREATE INDEX IF NOT EXISTS ""IX_AuditLog_Action"" ON ""AuditLogs""(""Action"");",
                    @"CREATE INDEX IF NOT EXISTS ""IX_AuditLog_ActorUsername"" ON ""AuditLogs""(""ActorUsername"");",

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
                var targetAdminPassword = (!string.IsNullOrWhiteSpace(adminInitPassword) && adminInitPassword.Length >= 8)
                    ? adminInitPassword
                    : "Admin@123";

                var adminRole = context.Roles.FirstOrDefault(r => r.RoleName == "Admin");
                if (adminRole == null)
                {
                    adminRole = new CinemaManagement.Models.Role { RoleName = "Admin" };
                    context.Roles.Add(adminRole);
                    context.SaveChanges();
                }

                var adminUser = context.Users.FirstOrDefault(u => u.Username.ToLower() == "admin");
                if (adminUser == null)
                {
                    adminUser = new CinemaManagement.Models.User
                    {
                        Username = "admin",
                        FullName = "Quản Trị Viên CinemaHub",
                        Email = "admin@cinemahub.vn",
                        EmailHash = piiService.ComputeEmailHash("admin@cinemahub.vn"),
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(targetAdminPassword),
                        RoleId = adminRole.RoleId,
                        Status = "Active",
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString("N")
                    };
                    context.Users.Add(adminUser);
                    context.SaveChanges();
                    Log.Information("🔐 Đã khởi tạo tài khoản admin ban đầu (Username: admin).");
                }
                else if (string.IsNullOrEmpty(adminUser.PasswordHash))
                {
                    // Chỉ gán mật khẩu nếu tài khoản admin chưa từng có PasswordHash
                    adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(targetAdminPassword);
                    if (string.IsNullOrEmpty(adminUser.EmailHash)) adminUser.EmailHash = piiService.ComputeEmailHash(adminUser.Email);
                    adminUser.SecurityStamp = Guid.NewGuid().ToString("N");
                    context.SaveChanges();
                    Log.Information("🔐 Đã gán mật khẩu khởi tạo cho tài khoản admin vì chưa có mật khẩu.");
                }
                else
                {
                    Log.Information("ℹ️ Tài khoản admin đã tồn tại. Bảo lưu toàn vẹn mật khẩu và trạng thái quản trị viên.");
                }

                // Tự động tính toán EmailHash cho các tài khoản cũ trong DB
                var usersMissingHash = context.Users.Where(u => u.EmailHash == null && u.Email != null).ToList();
                if (usersMissingHash.Count > 0)
                {
                    foreach (var u in usersMissingHash)
                    {
                        u.EmailHash = piiService.ComputeEmailHash(u.Email);
                    }
                    context.SaveChanges();
                    Log.Information("🔐 Đã đồng bộ EmailHash mã hóa cho {Count} tài khoản người dùng.", usersMissingHash.Count);
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
