using CinemaManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Services
{
    /// <summary>
    /// Background service tự động hủy các booking có Payment "Pending" quá 15 phút.
    /// Giải phóng ghế bị khóa khi user bỏ ngang thanh toán VnPay.
    /// </summary>
    public class PendingBookingCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PendingBookingCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _pendingTimeout = TimeSpan.FromMinutes(15);

        public PendingBookingCleanupService(IServiceProvider serviceProvider, ILogger<PendingBookingCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PendingBookingCleanupService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredPendingBookingsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during pending booking cleanup.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CleanupExpiredPendingBookingsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

            var cutoff = DateTime.Now.Subtract(_pendingTimeout);

            // Tìm các payment Pending có ticket được đặt quá hạn
            var expiredPayments = await context.Payments
                .Include(p => p.Ticket)
                .Where(p => p.Status == "Pending" && p.Ticket != null && p.Ticket.BookingTime < cutoff)
                .ToListAsync();

            if (expiredPayments.Count == 0) return;

            foreach (var payment in expiredPayments)
            {
                payment.Status = "Failed";
                if (payment.Ticket != null)
                {
                    payment.Ticket.Status = "Cancelled";
                }
            }

            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired pending booking(s).", expiredPayments.Count);
        }
    }
}
