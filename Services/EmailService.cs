using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CinemaManagement.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string? GetConfigValue(string key, string envName)
        {
            var val = _config[key];
            if (string.IsNullOrWhiteSpace(val))
                val = Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetUrl)
        {
            var smtpHost = GetConfigValue("Smtp:Host", "SMTP_HOST");
            var smtpPortStr = GetConfigValue("Smtp:Port", "SMTP_PORT") ?? "587";
            var smtpUser = GetConfigValue("Smtp:Username", "SMTP_USER");
            var smtpPass = GetConfigValue("Smtp:Password", "SMTP_PASS");
            var fromEmail = GetConfigValue("Smtp:FromEmail", "SMTP_FROM") ?? smtpUser ?? "noreply@cinemahubvn.io.vn";

            // Nếu người dùng cấu hình dạng "Name <email@domain>" thì trích xuất phần email chuẩn
            if (fromEmail.Contains('<') && fromEmail.Contains('>'))
            {
                var start = fromEmail.IndexOf('<') + 1;
                var end = fromEmail.IndexOf('>', start);
                if (end > start)
                    fromEmail = fromEmail[start..end].Trim();
            }
            fromEmail = fromEmail.Trim('"').Trim();

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogWarning("📧 [SMTP chưa cấu hình] Email đặt lại mật khẩu cho {Email}. Link khôi phục: {ResetUrl}", toEmail, resetUrl);
                return;
            }

            int smtpPort = int.TryParse(smtpPortStr, out var p) ? p : 587;
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? toEmail : fullName);

            string htmlBody = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;background:#141414;color:#f5f5f5;padding:28px;border-radius:12px;border:1px solid #333;'>
    <h2 style='color:#e50914;margin-top:0;'>🎬 CinemaHub — Khôi phục mật khẩu</h2>
    <p>Xin chào <strong>{safeName}</strong>,</p>
    <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản CinemaHub gắn với địa chỉ email này.</p>
    <p style='margin:24px 0;text-align:center;'>
        <a href='{resetUrl}' style='background:#e50914;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:bold;display:inline-block;'>
            Đặt lại mật khẩu ngay
        </a>
    </p>
    <p style='font-size:13px;color:#aaa;'>Liên kết này chỉ có hiệu lực trong vòng <strong>15 phút</strong> và chỉ sử dụng được 1 lần duy nhất.</p>
    <p style='font-size:13px;color:#aaa;'>Nếu bạn không yêu cầu thay đổi mật khẩu, vui lòng bỏ qua email này.</p>
</div>";

            try
            {
                await SendSmtpAuthPlainAsync(
                    smtpHost,
                    smtpPort,
                    smtpUser,
                    smtpPass,
                    fromEmail,
                    toEmail.Trim(),
                    "[CinemaHub] Yêu cầu đặt lại mật khẩu tài khoản",
                    htmlBody);

                _logger.LogInformation("📧 Đã gửi email khôi phục mật khẩu thành công tới {Email} qua {Host}:{Port}", toEmail, smtpHost, smtpPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi gửi email khôi phục mật khẩu tới {Email} qua {Host}:{Port}", toEmail, smtpHost, smtpPort);
            }
        }

        public async Task<bool> SendRegistrationVerificationEmailAsync(string toEmail, string fullName, string otpCode, string verifyUrl)
        {
            var smtpHost = GetConfigValue("Smtp:Host", "SMTP_HOST");
            var smtpPortStr = GetConfigValue("Smtp:Port", "SMTP_PORT") ?? "587";
            var smtpUser = GetConfigValue("Smtp:Username", "SMTP_USER");
            var smtpPass = GetConfigValue("Smtp:Password", "SMTP_PASS");
            var fromEmail = GetConfigValue("Smtp:FromEmail", "SMTP_FROM") ?? smtpUser ?? "noreply@cinemahubvn.io.vn";

            if (fromEmail.Contains('<') && fromEmail.Contains('>'))
            {
                var start = fromEmail.IndexOf('<') + 1;
                var end = fromEmail.IndexOf('>', start);
                if (end > start)
                    fromEmail = fromEmail[start..end].Trim();
            }
            fromEmail = fromEmail.Trim('"').Trim();

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogWarning("📧 [SMTP chưa cấu hình] Mã OTP xác thực đăng ký cho {Email}: {OtpCode} | Link: {VerifyUrl}", toEmail, otpCode, verifyUrl);
                return false;
            }

            int smtpPort = int.TryParse(smtpPortStr, out var p) ? p : 587;
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? toEmail : fullName);
            var safeOtp = WebUtility.HtmlEncode(otpCode);

            string htmlBody = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;background:#141414;color:#f5f5f5;padding:28px;border-radius:12px;border:1px solid #333;'>
    <h2 style='color:#e50914;margin-top:0;'>🎬 CinemaHub — Xác thực Gmail đăng ký tài khoản</h2>
    <p>Xin chào <strong>{safeName}</strong>,</p>
    <p>Cảm ơn bạn đã đăng ký tài khoản tại <strong>CinemaHub</strong>. Để hoàn tất việc tạo tài khoản mới, vui lòng nhập mã xác thực (OTP) dưới đây trên trang xác thực:</p>
    <div style='margin:24px 0;text-align:center;'>
        <div style='display:inline-block;background:#1f1f1f;border:2px dashed #e50914;border-radius:10px;padding:14px 32px;font-size:28px;font-weight:bold;letter-spacing:8px;color:#ffffff;'>
            {safeOtp}
        </div>
    </div>
    <p style='text-align:center;color:#ccc;font-size:14px;'>Hoặc bấm trực tiếp vào nút bên dưới để xác thực và kích hoạt tài khoản ngay:</p>
    <p style='margin:20px 0;text-align:center;'>
        <a href='{verifyUrl}' style='background:#e50914;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:bold;display:inline-block;'>
            Xác thực Gmail &amp; Tạo tài khoản
        </a>
    </p>
    <p style='font-size:13px;color:#aaa;'>Mã xác thực và liên kết này có hiệu lực trong vòng <strong>15 phút</strong>. Tài khoản của bạn chỉ được khởi tạo sau khi xác thực thành công.</p>
    <p style='font-size:13px;color:#aaa;'>Nếu bạn không thực hiện đăng ký này, vui lòng bỏ qua email này.</p>
</div>";

            try
            {
                await SendSmtpAuthPlainAsync(
                    smtpHost,
                    smtpPort,
                    smtpUser,
                    smtpPass,
                    fromEmail,
                    toEmail.Trim(),
                    "[CinemaHub] Mã xác thực Gmail đăng ký tài khoản",
                    htmlBody);

                _logger.LogInformation("📧 Đã gửi email xác thực đăng ký thành công tới {Email} qua {Host}:{Port}", toEmail, smtpHost, smtpPort);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi gửi email xác thực đăng ký tới {Email} qua {Host}:{Port}", toEmail, smtpHost, smtpPort);
                return false;
            }
        }

        public async Task<bool> SendAdminMfaOtpEmailAsync(string toEmail, string fullName, string otpCode, string ipAddress)
        {
            var smtpHost = GetConfigValue("Smtp:Host", "SMTP_HOST");
            var smtpPortStr = GetConfigValue("Smtp:Port", "SMTP_PORT") ?? "587";
            var smtpUser = GetConfigValue("Smtp:Username", "SMTP_USER");
            var smtpPass = GetConfigValue("Smtp:Password", "SMTP_PASS");
            var fromEmail = GetConfigValue("Smtp:FromEmail", "SMTP_FROM") ?? smtpUser ?? "security@cinemahubvn.io.vn";

            if (fromEmail.Contains('<') && fromEmail.Contains('>'))
            {
                var start = fromEmail.IndexOf('<') + 1;
                var end = fromEmail.IndexOf('>', start);
                if (end > start)
                    fromEmail = fromEmail[start..end].Trim();
            }
            fromEmail = fromEmail.Trim('"').Trim();

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogWarning("📧 [SMTP chưa cấu hình] Mã OTP đăng nhập quản trị (MFA) cho {Email}: {OtpCode} (IP: {Ip})", toEmail, otpCode, ipAddress);
                return false;
            }

            int smtpPort = int.TryParse(smtpPortStr, out var p) ? p : 587;
            var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? toEmail : fullName);
            var safeOtp = WebUtility.HtmlEncode(otpCode);
            var safeIp = WebUtility.HtmlEncode(ipAddress);
            var nowStr = DateTime.UtcNow.AddHours(7).ToString("HH:mm:ss dd/MM/yyyy (GMT+7)");

            string htmlBody = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;background:#0d1117;color:#f0f6fc;padding:32px;border-radius:16px;border:1px solid rgba(255,46,85,0.4);box-shadow:0 12px 36px rgba(0,0,0,0.6);'>
    <div style='text-align:center;margin-bottom:24px;'>
        <div style='display:inline-block;padding:6px 16px;background:rgba(255,46,85,0.15);border:1px solid #FF2E55;color:#FF2E55;border-radius:999px;font-size:12px;font-weight:700;letter-spacing:1px;text-transform:uppercase;'>
            BẢO MẬT QUẢN TRỊ 2 LỚP (MFA)
        </div>
    </div>
    <h2 style='color:#ffffff;text-align:center;margin:0 0 16px 0;font-size:22px;'>Mã xác thực đăng nhập Quản Trị Viên</h2>
    <p style='color:#8b949e;line-height:1.6;'>Xin chào <strong>{safeName}</strong>,</p>
    <p style='color:#8b949e;line-height:1.6;'>Hệ thống vừa ghi nhận một yêu cầu đăng nhập vào Khu vực Quản trị CinemaHub từ địa chỉ IP: <code style='background:rgba(255,255,255,0.1);padding:2px 6px;border-radius:4px;color:#58a6ff;'>{safeIp}</code> vào lúc <strong>{nowStr}</strong>.</p>
    
    <div style='background:#161b22;border:2px dashed #FF2E55;border-radius:12px;padding:24px;text-align:center;margin:28px 0;'>
        <div style='font-size:12px;color:#8b949e;text-transform:uppercase;letter-spacing:1.5px;margin-bottom:8px;'>Mã OTP của bạn:</div>
        <div style='font-size:38px;font-weight:800;letter-spacing:10px;color:#FF2E55;font-family:monospace;margin:6px 0;'>{safeOtp}</div>
        <div style='font-size:12px;color:#e3b341;margin-top:8px;'>⏱ Mã này có hiệu lực trong vòng 5 phút (hết hạn sau 5 phút)</div>
    </div>

    <div style='border-top:1px solid #30363d;padding-top:18px;margin-top:24px;font-size:12px;color:#8b949e;line-height:1.5;'>
        <p style='margin:0 0 8px 0;'>⚠️ <strong>Cảnh báo bảo mật:</strong> Tuyệt đối không cung cấp mã OTP này cho bất kỳ ai. Quản trị viên hệ thống sẽ không bao giờ hỏi mã của bạn.</p>
        <p style='margin:0;'>Nếu bạn không thực hiện yêu cầu đăng nhập này, tài khoản của bạn có thể đang bị lộ mật khẩu. Vui lòng đổi mật khẩu ngay lập tức.</p>
    </div>
</div>";

            try
            {
                await SendSmtpAuthPlainAsync(
                    smtpHost,
                    smtpPort,
                    smtpUser,
                    smtpPass,
                    fromEmail,
                    toEmail.Trim(),
                    $"[CinemaHub] Mã xác thực 2 lớp (MFA) Đăng nhập Quản trị: {otpCode}",
                    htmlBody);

                _logger.LogInformation("📧 Đã gửi mã MFA Admin thành công tới {Email} (IP: {Ip})", toEmail, ipAddress);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi gửi email MFA Admin tới {Email}", toEmail);
                return false;
            }
        }

        /// <summary>
        /// Gửi email qua giao thức SMTP (hỗ trợ cả STARTTLS cổng 587 và Implicit SSL cổng 465)
        /// với cơ chế xác thực chuẩn RFC 4616 AUTH PLAIN & fallback AUTH LOGIN 2 bước
        /// (tương thích 100% với OxMail / Postfix / Dovecot và Gmail).
        /// </summary>
        private static async Task SendSmtpAuthPlainAsync(
            string host,
            int port,
            string username,
            string password,
            string fromEmail,
            string toEmail,
            string subject,
            string htmlBody)
        {
            using var tcp = new TcpClient();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            await tcp.ConnectAsync(host, port, cts.Token);

            Stream activeStream = tcp.GetStream();
            SslStream? sslStream = null;

            try
            {
                if (port == 465)
                {
                    sslStream = new SslStream(activeStream, leaveInnerStreamOpen: false);
                    await sslStream.AuthenticateAsClientAsync(host);
                    activeStream = sslStream;

                    await ReadSmtpResponseAsync(activeStream, expectedPrefix: "220");
                    await SendSmtpCommandAsync(activeStream, "EHLO cinemahubvn.io.vn", "250");
                }
                else
                {
                    await ReadSmtpResponseAsync(activeStream, expectedPrefix: "220");
                    await SendSmtpCommandAsync(activeStream, "EHLO cinemahubvn.io.vn", "250");
                    await SendSmtpCommandAsync(activeStream, "STARTTLS", "220");

                    sslStream = new SslStream(activeStream, leaveInnerStreamOpen: false);
                    await sslStream.AuthenticateAsClientAsync(host);
                    activeStream = sslStream;

                    await SendSmtpCommandAsync(activeStream, "EHLO cinemahubvn.io.vn", "250");
                }

                // Ưu tiên AUTH PLAIN chuẩn RFC 4616 (\0username\0password)
                var plainToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{username}\0{password}"));
                await WriteLineAsync(activeStream, $"AUTH PLAIN {plainToken}");
                var authResp = await ReadSmtpResponseRawAsync(activeStream);

                if (!authResp.StartsWith("235", StringComparison.Ordinal))
                {
                    // Fallback sang AUTH LOGIN 2 bước chuẩn
                    await SendSmtpCommandAsync(activeStream, "AUTH LOGIN", "334");
                    await SendSmtpCommandAsync(activeStream, Convert.ToBase64String(Encoding.UTF8.GetBytes(username)), "334");
                    await SendSmtpCommandAsync(activeStream, Convert.ToBase64String(Encoding.UTF8.GetBytes(password)), "235");
                }

                await SendSmtpCommandAsync(activeStream, $"MAIL FROM:<{fromEmail}>", "250");
                await SendSmtpCommandAsync(activeStream, $"RCPT TO:<{toEmail}>", "250");
                await SendSmtpCommandAsync(activeStream, "DATA", "354");

                var encodedSubject = "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(subject)) + "?=";
                var encodedFromName = "=?UTF-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("CinemaHub Security")) + "?=";
                var bodyB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody), Base64FormattingOptions.InsertLineBreaks);
                var messageId = $"<{Guid.NewGuid():N}@cinemahubvn.io.vn>";
                var dateHeader = DateTimeOffset.UtcNow.ToString("r");

                var mimeBuilder = new StringBuilder();
                mimeBuilder.Append($"From: {encodedFromName} <{fromEmail}>\r\n");
                mimeBuilder.Append($"To: <{toEmail}>\r\n");
                mimeBuilder.Append($"Subject: {encodedSubject}\r\n");
                mimeBuilder.Append($"Date: {dateHeader}\r\n");
                mimeBuilder.Append($"Message-ID: {messageId}\r\n");
                mimeBuilder.Append("MIME-Version: 1.0\r\n");
                mimeBuilder.Append("Content-Type: text/html; charset=utf-8\r\n");
                mimeBuilder.Append("Content-Transfer-Encoding: base64\r\n");
                mimeBuilder.Append("\r\n");
                mimeBuilder.Append(bodyB64);
                mimeBuilder.Append("\r\n.");

                await SendSmtpCommandAsync(activeStream, mimeBuilder.ToString(), "250");
                await WriteLineAsync(activeStream, "QUIT");
            }
            finally
            {
                if (sslStream != null)
                    await sslStream.DisposeAsync();
            }
        }

        private static async Task SendSmtpCommandAsync(Stream stream, string command, string expectedPrefix)
        {
            await WriteLineAsync(stream, command);
            await ReadSmtpResponseAsync(stream, expectedPrefix);
        }

        private static async Task WriteLineAsync(Stream stream, string line)
        {
            var bytes = Encoding.ASCII.GetBytes(line + "\r\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        private static async Task ReadSmtpResponseAsync(Stream stream, string expectedPrefix)
        {
            var resp = await ReadSmtpResponseRawAsync(stream);
            if (!resp.StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"SMTP phản hồi không hợp lệ (kỳ vọng {expectedPrefix}): {resp.Trim()}");
        }

        private static async Task<string> ReadSmtpResponseRawAsync(Stream stream)
        {
            var sb = new StringBuilder();
            var lineBuffer = new StringBuilder();
            var oneByte = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(oneByte, 0, 1);
                if (read == 0) break;

                char c = (char)oneByte[0];
                lineBuffer.Append(c);

                if (c == '\n')
                {
                    var line = lineBuffer.ToString();
                    sb.Append(line);
                    lineBuffer.Clear();

                    // Dòng cuối của phản hồi SMTP có dạng "250 OK" (ký tự thứ 4 là khoảng trắng, không phải '-')
                    if (line.Length >= 4 && char.IsDigit(line[0]) && char.IsDigit(line[1]) && char.IsDigit(line[2]) && line[3] != '-')
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
