using CinemaManagement.Models;

namespace CinemaManagement.Data
{
    public static class DbInitializer
    {
        public static void Seed(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

            // =========================
            // SEED ROLES
            // =========================
            if (!context.Roles.Any())
            {
                context.Roles.AddRange(
                    new Role { RoleName = "Admin" },
                    new Role { RoleName = "Staff" },
                    new Role { RoleName = "Customer" }
                );
                context.SaveChanges();
            }

            // =========================
            // SEED ADMIN USER
            // =========================
            if (!context.Users.Any())
            {
                var adminRole = context.Roles.First(r => r.RoleName == "Admin");

                context.Users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = "1", // demo, sau này mã hóa
                    FullName = "Nguyễn Đức Mạnh",
                    Email = "necma2005@gmail.com",
                    Status = "Active",
                    RoleId = adminRole.RoleId
                });

                context.SaveChanges();
            }

            // =========================
            // SEED GENRES
            // =========================
            if (!context.Genres.Any())
            {
                context.Genres.AddRange(
                    new Genre { GenreName = "Action" },
                    new Genre { GenreName = "Comedy" },
                    new Genre { GenreName = "Drama" },
                    new Genre { GenreName = "Horror" },
                    new Genre { GenreName = "Sci-Fi" }
                );

                context.SaveChanges();
            }
        }
    }
}
