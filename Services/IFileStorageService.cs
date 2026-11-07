using Microsoft.AspNetCore.Http;

namespace CinemaManagement.Services
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Lưu file tải lên từ người dùng vào thư mục wwwroot/uploads/{subFolder}.
        /// Trả về đường dẫn web tương đối (ví dụ: /uploads/movies/filename.jpg).
        /// </summary>
        Task<string?> SaveFileAsync(IFormFile? file, string subFolder = "movies");

        /// <summary>
        /// Tải hình ảnh từ liên kết URL từ xa (remote CDN / internet) và lưu trữ cục bộ vào server.
        /// Trả về đường dẫn web tương đối (ví dụ: /uploads/movies/filename.jpg).
        /// </summary>
        Task<string?> DownloadAndSaveRemoteImageAsync(string imageUrl, string subFolder = "movies");

        /// <summary>
        /// Xóa file cục bộ nếu nằm trong thư mục uploads.
        /// </summary>
        bool DeleteFile(string? relativePath);

        /// <summary>
        /// Kiểm tra nhanh URL có phải định dạng hợp lệ không.
        /// </summary>
        bool IsValidImageUrl(string? url);
    }
}

