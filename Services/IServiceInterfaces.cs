using CinemaManagement.Models.ViewModels;

namespace CinemaManagement.Services
{
    public interface IMovieService
    {
        Task<List<MovieDto>> GetAllMoviesAsync();
        Task<MovieDto?> GetMovieByIdAsync(int id);
        Task<MovieDto> CreateMovieAsync(CreateMovieDto dto);
        Task<bool> UpdateMovieAsync(int id, CreateMovieDto dto);
        Task<bool> DeleteMovieAsync(int id);
    }

    public interface IShowtimeService
    {
        Task<List<ShowtimeDto>> GetShowtimesByMovieAsync(int movieId);
        Task<List<ShowtimeDto>> GetShowtimesByMovieAndFiltersAsync(int movieId, DateTime? date, int? theaterId);
        Task<List<ShowtimeDto>> GetShowtimesByDateAsync(DateTime date);
        Task<List<ShowtimeDto>> GetShowtimesByTheaterAsync(int theaterId);
        Task<List<ShowtimeDto>> GetShowtimesByDateAndTheaterAsync(DateTime date, int? theaterId);
        Task<ShowtimeDto?> GetShowtimeByIdAsync(int id);
        Task<List<SeatStatusDto>> GetSeatStatusAsync(int showtimeId);
        Task<ShowtimeDto> CreateShowtimeAsync(CreateShowtimeDto dto);
        Task<bool> DeleteShowtimeAsync(int id);
    }

    public interface ITicketService
    {
        Task<TicketDto> BookTicketAsync(int userId, BookTicketDto dto);
        Task<List<TicketDto>> GetUserTicketsAsync(int userId);
        Task<bool> CancelTicketAsync(int ticketId, int userId);

        /// <summary>
        /// Tạo Ticket (Pending) + Payment (Pending) cho flow thanh toán VNPay.
        /// Ghế được khoá ngay (unique index) để tránh user khác đặt cùng lúc.
        /// </summary>
        Task<(TicketDto Ticket, int PaymentId)> CreatePendingBookingAsync(int userId, BookTicketDto dto);

        /// <summary>
        /// Đánh dấu Payment Completed (success=true) hoặc Failed + huỷ Ticket (success=false).
        /// Đối chiếu số tiền thực trả (paidAmount) nếu được cung cấp.
        /// </summary>
        Task<(bool Handled, string? ErrorReason)> ConfirmPaymentAsync(int paymentId, string transactionId, bool success, decimal? paidAmount = null);

        /// <summary>
        /// Giải phóng ngay các vé đang ở trạng thái Pending của chính user (khi bấm Back từ VNPay về trang chọn ghế)
        /// đồng thời dọn các vé Pending đã quá hạn HeldUntil.
        /// </summary>
        Task<int> ReleaseUserPendingTicketsAsync(int userId, int? showtimeId = null);
    }

    /// <summary>
    /// Service tạo URL thanh toán VNPay sandbox và verify response trả về.
    /// </summary>
    public interface IVnPayService
    {
        string CreatePaymentUrl(HttpContext context, VnPaymentRequestModel model);
        VnPaymentResponseModel PaymentExecute(IQueryCollection collections);
    }

    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetUrl);
        Task<bool> SendRegistrationVerificationEmailAsync(string toEmail, string fullName, string otpCode, string verifyUrl);
    }

    public enum SecurityAlertLevel
    {
        Info,
        Medium,
        High,
        Critical
    }

    public record SecurityAlert(
        SecurityAlertLevel Level,
        string Category,
        string Title,
        string Details,
        string? IpAddress = null,
        string? Username = null,
        string? Path = null,
        DateTime? TimestampUtc = null);

    public interface ISecurityAlertService
    {
        void EnqueueAlert(SecurityAlert alert);
        void RecordRateLimitRejection(string policyOrPath, string ipAddress, string? username);
    }
}
