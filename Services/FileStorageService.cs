using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CinemaManagement.Services
{
    public class FileStorageService : IFileStorageService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".jfif", ".svg"
        };

        private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB

        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(
            IWebHostEnvironment env,
            IHttpClientFactory httpClientFactory,
            ILogger<FileStorageService> logger)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> SaveFileAsync(IFormFile? file, string subFolder = "movies")
        {
            if (file == null || file.Length == 0) return null;

            if (file.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException($"Kích thước tệp vượt quá giới hạn cho phép ({MaxFileSizeBytes / (1024 * 1024)}MB).");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"Định dạng tệp không được hỗ trợ ({extension}). Chỉ chấp nhận: {string.Join(", ", AllowedExtensions)}.");
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var targetDir = Path.Combine(webRoot, "uploads", subFolder);

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Tên file ngẫu nhiên an toàn, tránh đụng hàng và path traversal
            var safeFileName = $"{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}{extension}";
            var physicalPath = Path.Combine(targetDir, safeFileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Đã lưu file tải lên thành công: {Path}", physicalPath);
            return $"/uploads/{subFolder}/{safeFileName}";
        }

        public async Task<string?> DownloadAndSaveRemoteImageAsync(string imageUrl, string subFolder = "movies")
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            if (!Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogWarning("URL hình ảnh không hợp lệ: {Url}", imageUrl);
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CinemaHub-MediaDownloader/1.0 (Windows NT 10.0; Win64; x64)");

                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Không thể tải ảnh từ URL {Url}. HTTP StatusCode: {Code}", imageUrl, response.StatusCode);
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
                var extension = GetExtensionFromContentTypeOrUrl(contentType, uri.AbsolutePath);

                if (!AllowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Nội dung tải về không phải ảnh hợp lệ ({ContentType}) từ URL {Url}", contentType, imageUrl);
                    return null;
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue && contentLength.Value > MaxFileSizeBytes)
                {
                    _logger.LogWarning("Ảnh từ URL {Url} quá lớn ({Size} bytes)", imageUrl, contentLength.Value);
                    return null;
                }

                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var targetDir = Path.Combine(webRoot, "uploads", subFolder);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var safeFileName = $"remote_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}{extension}";
                var physicalPath = Path.Combine(targetDir, safeFileName);

                using (var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var remoteStream = await response.Content.ReadAsStreamAsync())
                {
                    await remoteStream.CopyToAsync(fileStream);
                }

                _logger.LogInformation("Đã tải ảnh từ xa và lưu thành công: {Url} -> {Path}", imageUrl, physicalPath);
                return $"/uploads/{subFolder}/{safeFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh từ xa từ URL: {Url}", imageUrl);
                return null;
            }
        }

        public bool DeleteFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            // Chỉ cho phép xoá trong thư mục uploads để tránh xoá nhầm asset gốc của hệ thống
            if (!relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var cleanPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var fullPath = Path.Combine(webRoot, cleanPath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Đã xóa tệp: {Path}", fullPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể xóa tệp: {Path}", relativePath);
            }

            return false;
        }

        public bool IsValidImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (url.StartsWith("/") || url.StartsWith("~/")) return true; // Local path
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string GetExtensionFromContentTypeOrUrl(string contentType, string absolutePath)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("jpeg") || contentType.Contains("jpg")) return ".jpg";
                if (contentType.Contains("png")) return ".png";
                if (contentType.Contains("webp")) return ".webp";
                if (contentType.Contains("gif")) return ".gif";
                if (contentType.Contains("svg")) return ".svg";
            }

            var ext = Path.GetExtension(absolutePath)?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext))
            {
                return ext;
            }

            return ".jpg";
        }
    }
}

