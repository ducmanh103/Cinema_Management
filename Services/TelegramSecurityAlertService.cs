using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CinemaManagement.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CinemaManagement.Services
{
    public class TelegramSecurityAlertService : BackgroundService, ISecurityAlertService
    {
        private readonly Channel<SecurityAlert> _channel;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramSecurityAlertService> _logger;

        // Chống bão tin nhắn (Deduplication 60s cho cùng Category + IP/User)
        private readonly ConcurrentDictionary<string, (DateTime LastSentUtc, int SuppressedCount)> _dedupCache = new();

        // Đếm số lần bị chặn HTTP 429 theo IP trong cửa sổ 1 phút
        private readonly ConcurrentDictionary<string, (DateTime WindowStartUtc, int Count, bool Alerted)> _rateLimitSpikes = new();

        public TelegramSecurityAlertService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TelegramSecurityAlertService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _channel = Channel.CreateBounded<SecurityAlert>(new BoundedChannelOptions(200)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void EnqueueAlert(SecurityAlert alert)
        {
            var enriched = alert with { TimestampUtc = alert.TimestampUtc ?? DateTime.UtcNow };
            _channel.Writer.TryWrite(enriched);
        }

        public void RecordRateLimitRejection(string policyOrPath, string ipAddress, string? username)
        {
            var now = DateTime.UtcNow;
            var key = $"{ipAddress}_{policyOrPath}";

            var state = _rateLimitSpikes.AddOrUpdate(
                key,
                _ => (now, 1, false),
                (_, existing) =>
                {
                    if ((now - existing.WindowStartUtc).TotalSeconds > 60)
                        return (now, 1, false);
                    return (existing.WindowStartUtc, existing.Count + 1, existing.Alerted);
                });

            // Bắn cảnh báo khi 1 IP bị chặn 429 từ 15 lần trở lên trong 1 phút
            if (state.Count >= 15 && !state.Alerted)
            {
                _rateLimitSpikes[key] = (state.WindowStartUtc, state.Count, true);
                EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.Medium,
                    "RATE_LIMIT_SPIKE",
                    "Phát hiện dấu hiệu tấn công / quét tự động (HTTP 429 Spike)",
                    $"Thiết bị đã vượt ngưỡng giới hạn {state.Count} lần/phút tại vùng bảo vệ: {policyOrPath}",
                    IpAddress: ipAddress,
                    Username: username,
                    Path: policyOrPath));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🛡️ TelegramSecurityAlertService background worker started.");

            await foreach (var alert in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAlertAsync(alert, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi cảnh báo bảo mật tới Telegram.");
                }
            }
        }

        private async Task ProcessAlertAsync(SecurityAlert alert, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var dedupKey = $"{alert.Category}:{alert.IpAddress ?? alert.Username ?? alert.Title}";

            int previousSuppressed = 0;
            if (_dedupCache.TryGetValue(dedupKey, out var existing))
            {
                if ((now - existing.LastSentUtc).TotalSeconds < 60)
                {
                    _dedupCache[dedupKey] = (existing.LastSentUtc, existing.SuppressedCount + 1);
                    return;
                }
                previousSuppressed = existing.SuppressedCount;
            }
            _dedupCache[dedupKey] = (now, 0);

            // Dọn dẹp cache chống rò rỉ bộ nhớ trên VPS 2GB RAM
            if (_dedupCache.Count > 500)
            {
                foreach (var kv in _dedupCache)
                {
                    if ((now - kv.Value.LastSentUtc).TotalMinutes > 5)
                        _dedupCache.TryRemove(kv.Key, out _);
                }
            }
            if (_rateLimitSpikes.Count > 500)
            {
                foreach (var kv in _rateLimitSpikes)
                {
                    if ((now - kv.Value.WindowStartUtc).TotalMinutes > 5)
                        _rateLimitSpikes.TryRemove(kv.Key, out _);
                }
            }

            // Luôn ghi log cảnh báo bảo mật ra Serilog
            _logger.LogWarning(
                "🚨 [SECURITY ALERT - {Level}] [{Category}] {Title} | IP: {IP} | User: {User} | Path: {Path} | {Details}",
                alert.Level, alert.Category, alert.Title, alert.IpAddress ?? "N/A", alert.Username ?? "Anonymous", alert.Path ?? "N/A", alert.Details);

            var botToken = _configuration["Telegram:BotToken"] ?? Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
            var chatId = _configuration["Telegram:ChatId"] ?? Environment.GetEnvironmentVariable("TELEGRAM_ALERT_CHAT_ID");

            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
                return;

            string icon = alert.Level switch
            {
                SecurityAlertLevel.Critical => "💥 [CRITICAL]",
                SecurityAlertLevel.High => "🚨 [HIGH]",
                SecurityAlertLevel.Medium => "⚠️ [MEDIUM]",
                _ => "ℹ️ [INFO]"
            };

            var vnTime = (alert.TimestampUtc ?? now).ToVietnamTime().ToString("HH:mm:ss dd/MM/yyyy");
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{icon} CINEMAHUB SECURITY ALERT</b>");
            sb.AppendLine($"<b>Phân loại:</b> <code>{Html(alert.Category)}</code>");
            sb.AppendLine($"<b>Sự kiện:</b> {Html(alert.Title)}");
            if (!string.IsNullOrWhiteSpace(alert.IpAddress))
                sb.AppendLine($"<b>IP nguồn:</b> <code>{Html(alert.IpAddress)}</code>");
            if (!string.IsNullOrWhiteSpace(alert.Username))
                sb.AppendLine($"<b>Tài khoản:</b> <code>{Html(alert.Username)}</code>");
            if (!string.IsNullOrWhiteSpace(alert.Path))
                sb.AppendLine($"<b>Endpoint:</b> <code>{Html(alert.Path)}</code>");
            sb.AppendLine($"<b>Thời gian (VN):</b> {vnTime}");
            sb.AppendLine($"<b>Chi tiết:</b> {Html(MaskSensitive(alert.Details))}");

            if (previousSuppressed > 0)
                sb.AppendLine($"<i>(+{previousSuppressed} sự kiện tương tự đã được gom nhóm trong 60s qua)</i>");

            var payload = JsonSerializer.Serialize(new
            {
                chat_id = chatId,
                text = sb.ToString(),
                parse_mode = "HTML",
                disable_web_page_preview = true
            });

            var client = _httpClientFactory.CreateClient("TelegramBot");
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"https://api.telegram.org/bot{botToken}/sendMessage", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Telegram API trả về mã lỗi {StatusCode}: {Body}", response.StatusCode, errBody);
            }
        }

        private static string Html(string? input) => WebUtility.HtmlEncode(input ?? string.Empty);

        private static string MaskSensitive(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Length > 500) input = input[..500] + "...";
            return input;
        }
    }
}
