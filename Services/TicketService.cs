using CinemaManagement.Data;
using CinemaManagement.Helpers;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CinemaManagement.Services
{
    public class TicketService : ITicketService
    {
        private readonly CinemaDbContext _context;

        public TicketService(CinemaDbContext context) => _context = context;

        /// <summary>
        /// Đặt vé với transaction để tránh race condition (double booking).
        /// </summary>
        public async Task<TicketDto> BookTicketAsync(int userId, BookTicketDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = VietnamTime.Now;

                var showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == dto.ShowtimeId)
                    ?? throw new InvalidOperationException("Suất chiếu không tồn tại.");

                if (showtime.StartTime <= now)
                    throw new InvalidOperationException("Suất chiếu đã bắt đầu hoặc đã kết thúc, không thể đặt vé.");

                var seat = await _context.Seats.FindAsync(dto.SeatId)
                    ?? throw new InvalidOperationException("Ghế không tồn tại.");

                if (seat.RoomId != showtime.RoomId)
                    throw new InvalidOperationException("Ghế không thuộc phòng chiếu này.");

                bool alreadyBooked = await _context.Tickets
                    .AnyAsync(t => t.ShowtimeId == dto.ShowtimeId
                               && t.SeatId == dto.SeatId
                               && (t.Status == "Booked" || t.Status == "Pending"));

                if (alreadyBooked)
                    throw new InvalidOperationException("Ghế này đã được đặt hoặc đang được giữ. Vui lòng chọn ghế khác.");

                var ticket = new Ticket
                {
                    ShowtimeId = dto.ShowtimeId,
                    SeatId = dto.SeatId,
                    UserId = userId,
                    BookingTime = VietnamTime.UtcNow,
                    Status = "Booked"
                };
                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                decimal actualPrice = showtime.Price + (seat.SeatType == "VIP" ? 15000m : 0m);

                var payment = new Payment
                {
                    TicketId = ticket.TicketId,
                    Amount = actualPrice,
                    Method = dto.PaymentMethod,
                    Status = "Completed",
                    PaidAt = VietnamTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new TicketDto
                {
                    TicketId = ticket.TicketId,
                    MovieTitle = showtime.Movie?.Title ?? "",
                    RoomName = showtime.Room?.RoomName ?? "",
                    StartTime = showtime.StartTime,
                    SeatNumber = seat.SeatNumber,
                    Price = actualPrice,
                    Status = ticket.Status,
                    BookingTime = ticket.BookingTime.ToVietnamTime(),
                    PaymentStatus = payment.Status
                };
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("Ghế này vừa được người khác đặt cùng lúc. Vui lòng chọn ghế khác.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<TicketDto>> GetUserTicketsAsync(int userId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.BookingTime)
                .Select(t => new TicketDto
                {
                    TicketId = t.TicketId,
                    MovieTitle = t.Showtime.Movie.Title,
                    RoomName = t.Showtime.Room.RoomName,
                    StartTime = t.Showtime.StartTime,
                    SeatNumber = t.Seat.SeatNumber,
                    Price = t.Payment != null ? t.Payment.Amount : (t.Showtime.Price + (t.Seat.SeatType == "VIP" ? 15000m : 0m)),
                    Status = t.Status,
                    BookingTime = t.BookingTime,
                    PaymentStatus = t.Payment != null ? t.Payment.Status : "N/A"
                })
                .ToListAsync();
        }

        public async Task<bool> CancelTicketAsync(int ticketId, int userId)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Showtime)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.UserId == userId);

            if (ticket == null || ticket.Status == "Cancelled") return false;

            if (ticket.Showtime != null && ticket.Showtime.StartTime <= VietnamTime.Now)
                return false;

            ticket.Status = "Cancelled";

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TicketId == ticketId);
            if (payment != null) payment.Status = "Refunded";

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Tạo Ticket (Pending) + Payment (Pending) trong cùng 1 transaction để khoá ghế ngay.
        /// </summary>
        public async Task<(TicketDto Ticket, int PaymentId)> CreatePendingBookingAsync(int userId, BookTicketDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = VietnamTime.Now;

                var showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == dto.ShowtimeId)
                    ?? throw new InvalidOperationException("Suất chiếu không tồn tại.");

                if (showtime.StartTime <= now)
                    throw new InvalidOperationException("Suất chiếu đã bắt đầu hoặc đã kết thúc, không thể đặt vé.");

                var seat = await _context.Seats.FindAsync(dto.SeatId)
                    ?? throw new InvalidOperationException("Ghế không tồn tại.");

                if (seat.RoomId != showtime.RoomId)
                    throw new InvalidOperationException("Ghế không thuộc phòng chiếu này.");

                bool alreadyBooked = await _context.Tickets
                    .AnyAsync(t => t.ShowtimeId == dto.ShowtimeId
                                && t.SeatId == dto.SeatId
                                && (t.Status == "Booked" || t.Status == "Pending"));

                if (alreadyBooked)
                    throw new InvalidOperationException("Ghế này đang được người khác chọn hoặc đã đặt. Vui lòng chọn ghế khác.");

                var utcNow = VietnamTime.UtcNow;
                var ticket = new Ticket
                {
                    ShowtimeId = dto.ShowtimeId,
                    SeatId = dto.SeatId,
                    UserId = userId,
                    BookingTime = utcNow,
                    HeldUntil = utcNow.AddMinutes(15),
                    Status = "Pending"
                };
                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                decimal actualPrice = showtime.Price + (seat.SeatType == "VIP" ? 15000m : 0m);

                var payment = new Payment
                {
                    TicketId = ticket.TicketId,
                    Amount = actualPrice,
                    Method = dto.PaymentMethod,
                    Status = "Pending",
                    PaidAt = utcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var ticketDto = new TicketDto
                {
                    TicketId = ticket.TicketId,
                    MovieTitle = showtime.Movie?.Title ?? "",
                    RoomName = showtime.Room?.RoomName ?? "",
                    StartTime = showtime.StartTime,
                    SeatNumber = seat.SeatNumber,
                    Price = actualPrice,
                    Status = ticket.Status,
                    BookingTime = ticket.BookingTime.ToVietnamTime(),
                    PaymentStatus = payment.Status
                };
                return (ticketDto, payment.PaymentId);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException("Ghế này vừa được người khác giữ chỗ. Vui lòng chọn ghế khác.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Xác nhận kết quả thanh toán từ VNPay (ReturnUrl / IPN).
        /// Có kiểm tra khớp số tiền (paidAmount) để chống giả mạo mệnh giá.
        /// </summary>
        public async Task<(bool Handled, string? ErrorReason)> ConfirmPaymentAsync(
            int paymentId,
            string transactionId,
            bool success,
            decimal? paidAmount = null)
        {
            var payment = await _context.Payments
                .Include(p => p.Ticket)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return (false, "Không tìm thấy đơn thanh toán.");

            if (payment.Status == "Completed")
                return (true, null);

            if (payment.Status == "Failed")
                return (false, "Đơn thanh toán này đã bị huỷ hoặc hết hạn trước đó.");

            if (paidAmount.HasValue && Math.Abs(paidAmount.Value - payment.Amount) > 0.01m)
            {
                return (false, $"Số tiền thanh toán không khớp (VNPay trả về: {paidAmount.Value:N0}₫, Giá vé: {payment.Amount:N0}₫).");
            }

            if (success)
            {
                payment.Status = "Completed";
                payment.TransactionId = transactionId;
                payment.PaidAt = VietnamTime.UtcNow;
                if (payment.Ticket != null)
                {
                    payment.Ticket.Status = "Booked";
                    payment.Ticket.HeldUntil = null;
                }
            }
            else
            {
                payment.Status = "Failed";
                if (payment.Ticket != null)
                {
                    payment.Ticket.Status = "Cancelled";
                    payment.Ticket.HeldUntil = null;
                }
            }

            await _context.SaveChangesAsync();
            return (success, success ? null : "Giao dịch không thành công.");
        }
    }
}
