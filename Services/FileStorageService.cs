using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CinemaManagement.Services
{
    public class FileStorageService : IFileStorageService
    {
        // Loại bỏ hoàn toàn .svg (nguy cơ Stored XSS) và .jfif
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private static readonly HashSet<string> BlockedHostnames = new(StringComparer.OrdinalIgnoreCase)
        {
            "localhost", "127.0.0.1", "::1", "postgres_db", "cinemahub_postgres",
            "web_app", "cinemahub_web", "nginx", "cinemahub_nginx"
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

            // Kiểm tra Magic Bytes thực tế của tệp để chống giả mạo định dạng (Polyglot / Extension spoofing)
            await using (var readStream = file.OpenReadStream())
            {
                if (!ValidateImageSignature(readStream, extension))
                {
                    _logger.LogWarning("Tệp tải lên không khớp Magic Bytes chữ ký ảnh: {FileName}", file.FileName);
                    throw new InvalidOperationException("Nội dung tệp không hợp lệ hoặc bị giả mạo định dạng ảnh.");
                }
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

            await using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
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

            // 1. Chống SSRF: Chỉ cho phép cổng chuẩn 80 hoặc 443
            if (uri.Port != 80 && uri.Port != 443)
            {
                _logger.LogWarning("SSRF Blocked: URL sử dụng cổng không chuẩn ({Port}): {Url}", uri.Port, imageUrl);
                return null;
            }

            // 2. Chống SSRF: Chặn các hostname nội bộ và resolve DNS kiểm tra IP riêng tư
            if (BlockedHostnames.Contains(uri.DnsSafeHost))
            {
                _logger.LogWarning("SSRF Blocked: Hostname nội bộ bị chặn: {Host}", uri.DnsSafeHost);
                return null;
            }

            try
            {
                var hostAddresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
                if (hostAddresses.Length == 0)
                {
                    _logger.LogWarning("Không thể phân giải tên miền: {Host}", uri.DnsSafeHost);
                    return null;
                }

                foreach (var ip in hostAddresses)
                {
                    if (IsPrivateOrLocalIp(ip))
                    {
                        _logger.LogWarning("SSRF Blocked: Tên miền {Host} phân giải về địa chỉ IP nội bộ {Ip}", uri.DnsSafeHost, ip);
                        return null;
                    }
                }
            }
            catch (Exception dnsEx)
            {
                _logger.LogWarning(dnsEx, "Lỗi phân giải DNS cho URL: {Url}", imageUrl);
                return null;
            }

            string? tempPhysicalPath = null;
            try
            {
                // Sử dụng HttpClientHandler riêng với AllowAutoRedirect = false để chặn SSRF qua Redirect 302 về nội bộ
                using var handler = new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectTimeout = TimeSpan.FromSeconds(10)
                };
                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(20)
                };
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
                    _logger.LogWarning("Nội dung tải về không phải định dạng ảnh hợp lệ ({ContentType}) từ URL {Url}", contentType, imageUrl);
                    return null;
                }

                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var targetDir = Path.Combine(webRoot, "uploads", subFolder);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var safeFileName = $"remote_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}{extension}";
                tempPhysicalPath = Path.Combine(targetDir, safeFileName);

                // Giới hạn luồng đọc trực tiếp để chống DoS nếu server từ xa gửi stream dữ liệu vô tận (không có Content-Length)
                await using (var fileStream = new FileStream(tempPhysicalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                await using (var remoteStream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[81920];
                    long totalBytesRead = 0;
                    int read;

                    while ((read = await remoteStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytesRead += read;
                        if (totalBytesRead > MaxFileSizeBytes)
                        {
                            throw new InvalidOperationException($"Tệp từ xa vượt quá dung lượng tối đa ({MaxFileSizeBytes / (1024 * 1024)}MB).");
                        }
                        await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    }

                    // Kiểm tra Magic Bytes của tệp sau khi tải xong
                    if (!ValidateImageSignature(fileStream, extension))
                    {
                        throw new InvalidOperationException("Tệp tải về không khớp chữ ký Magic Bytes của ảnh.");
                    }
                }

                _logger.LogInformation("Đã tải ảnh từ xa và lưu thành công: {Url} -> {Path}", imageUrl, tempPhysicalPath);
                return $"/uploads/{subFolder}/{safeFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh từ xa từ URL: {Url}", imageUrl);
                if (tempPhysicalPath != null && File.Exists(tempPhysicalPath))
                {
                    try { File.Delete(tempPhysicalPath); } catch { }
                }
                return null;
            }
        }

        public bool DeleteFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return false;

            // Chỉ cho phép xoá trong thư mục uploads để tránh xoá nhầm asset gốc của hệ thống (Path Traversal defense)
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

        private static bool IsPrivateOrLocalIp(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                return ip.Equals(IPAddress.IPv6Loopback)
                       || ip.IsIPv6LinkLocal
                       || ip.IsIPv6SiteLocal
                       || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC; // ULA fc00::/7
            }

            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 10) return true;                                // 10.0.0.0/8
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true; // 172.16.0.0/12
            if (bytes[0] == 192 && bytes[1] == 168) return true;             // 192.168.0.0/16
            if (bytes[0] == 127) return true;                               // 127.0.0.0/8
            if (bytes[0] == 169 && bytes[1] == 254) return true;             // 169.254.0.0/16 Link-Local
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true; // 100.64.0.0/10 CGNAT
            if (bytes[0] == 0) return true;                                 // 0.0.0.0/8

            return false;
        }

        private static bool ValidateImageSignature(Stream stream, string extension)
        {
            if (stream == null || !stream.CanRead || stream.Length < 12)
                return false;

            var currentPosition = stream.Position;
            try
            {
                stream.Position = 0;
                var header = new byte[16];
                int bytesRead = stream.Read(header, 0, header.Length);
                if (bytesRead < 12) return false;

                switch (extension.ToLowerInvariant())
                {
                    case ".jpg":
                    case ".jpeg":
                        // JPEG SOI marker: FF D8 FF
                        return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

                    case ".png":
                        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
                        return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

                    case ".gif":
                        // GIF87a or GIF89a: 47 49 46 38 (37|39) 61
                        return header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38
                            && (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61;

                    case ".webp":
                        // RIFF .... WEBP: 52 49 46 46 [4 bytes size] 57 45 42 50
                        return header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

                    default:
                        return false;
                }
            }
            finally
            {
                stream.Position = currentPosition;
            }
        }

        private static string GetExtensionFromContentTypeOrUrl(string contentType, string absolutePath)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("jpeg") || contentType.Contains("jpg")) return ".jpg";
                if (contentType.Contains("png")) return ".png";
                if (contentType.Contains("webp")) return ".webp";
                if (contentType.Contains("gif")) return ".gif";
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
