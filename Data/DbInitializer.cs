using CinemaManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Data
{
    public static class DbInitializer
    {
        /// <summary>
        /// DbInitializer hiện tại đã được tối ưu. 
        /// Mọi cấu trúc bảng và dữ liệu mẫu (Seed Data) đã được chuyển sang file SQL: 
        /// /DB/CinemaManagement.sql để quản lý tập trung và hiệu quả hơn.
        /// </summary>
        public static void Seed(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

            // Chỉ đảm bảo DB tồn tại, không thực hiện Migration hay Seed bằng Code nữa.
            // (Người dùng sẽ chạy file SQL thủ công trong SSMS).
            try 
            {
                if (!context.Database.CanConnect())
                {
                    Console.WriteLine(">>> Cảnh báo: Không thể kết nối Database. Hãy đảm bảo bạn đã chạy file /DB/CinemaManagement.sql trong SSMS.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> Lỗi kết nối DB: {ex.Message}");
            }
        }
    }
}
