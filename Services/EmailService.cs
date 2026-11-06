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
