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
        /// Tạo Ticket (Booked) + Payment (Pending) cho flow thanh toán VNPay.
        /// Ghế được khoá ngay (unique index) để tránh user khác đặt cùng lúc.
        /// </summary>
        Task<(TicketDto Ticket, int PaymentId)> CreatePendingBookingAsync(int userId, BookTicketDto dto);

        /// <summary>
        /// Đánh dấu Payment Completed (success=true) hoặc Failed + huỷ Ticket (success=false).
        /// </summary>
        Task<bool> ConfirmPaymentAsync(int paymentId, string transactionId, bool success);
    }

    /// <summary>
    /// Service tạo URL thanh toán VNPay sandbox và verify response trả về.
    /// </summary>
    public interface IVnPayService
    {
        string CreatePaymentUrl(HttpContext context, VnPaymentRequestModel model);
        VnPaymentResponseModel PaymentExecute(IQueryCollection collections);
    }
}
