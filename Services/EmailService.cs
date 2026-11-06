using System;
using System.Net;
using System.Net.Mail;
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
            var fromEmail = GetConfigValue("Smtp:FromEmail", "SMTP_FROM") ?? smtpUser ?? "noreply@cinemahub.vn";

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogWarning("📧 [SMTP chưa cấu hình] Email đặt lại mật khẩu cho {Email}. Link khôi phục (chỉ dùng cho môi trường dev/test): {ResetUrl}", toEmail, resetUrl);
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

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true,
                Timeout = 15000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, "CinemaHub Security"),
                Subject = "[CinemaHub] Yêu cầu đặt lại mật khẩu tài khoản",
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("📧 Đã gửi email khôi phục mật khẩu tới {Email}", toEmail);
        }
    }
}

