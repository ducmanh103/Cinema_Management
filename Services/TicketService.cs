using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

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
            // Dùng transaction đảm bảo tính nguyên tử
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Kiểm tra suất chiếu tồn tại
                var showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == dto.ShowtimeId)
                    ?? throw new InvalidOperationException("Suất chiếu không tồn tại.");

                // 2. Kiểm tra ghế tồn tại và thuộc phòng chiếu
                var seat = await _context.Seats.FindAsync(dto.SeatId)
                    ?? throw new InvalidOperationException("Ghế không tồn tại.");

                if (seat.RoomId != showtime.RoomId)
                    throw new InvalidOperationException("Ghế không thuộc phòng chiếu này.");

                // 3. Kiểm tra ghế đã được đặt chưa (khóa bản ghi với row-level check)
                bool alreadyBooked = await _context.Tickets
                    .AnyAsync(t => t.ShowtimeId == dto.ShowtimeId
                               && t.SeatId == dto.SeatId
                               && t.Status == "Booked");

                if (alreadyBooked)
                    throw new InvalidOperationException("Ghế này đã được đặt. Vui lòng chọn ghế khác.");

                // 4. Tạo Ticket
                var ticket = new Ticket
                {
                    ShowtimeId = dto.ShowtimeId,
                    SeatId = dto.SeatId,
                    UserId = userId,
                    BookingTime = DateTime.Now,
                    Status = "Booked"
                };
                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync(); // Unique index sẽ chặn nếu race condition

                // Tính toán giá thực tế bao gồm phụ thu ghế VIP
                decimal actualPrice = showtime.Price + (seat.SeatType == "VIP" ? 15000m : 0m);

                // 5. Tạo Payment
                var payment = new Payment
                {
                    TicketId = ticket.TicketId,
                    Amount = actualPrice,
                    Method = dto.PaymentMethod,
                    Status = "Completed",
                    PaidAt = DateTime.Now
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
                    BookingTime = ticket.BookingTime,
                    PaymentStatus = payment.Status
                };
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
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.UserId == userId);

            if (ticket == null || ticket.Status == "Cancelled") return false;

            ticket.Status = "Cancelled";

            // Cập nhật payment
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.TicketId == ticketId);
            if (payment != null) payment.Status = "Refunded";

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Tạo Ticket (Booked) + Payment (Pending) trong cùng 1 transaction để khoá ghế ngay.
        /// Trả về TicketDto + PaymentId để controller dùng làm OrderId gửi sang VNPay.
        /// </summary>
        public async Task<(TicketDto Ticket, int PaymentId)> CreatePendingBookingAsync(int userId, BookTicketDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var showtime = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == dto.ShowtimeId)
                    ?? throw new InvalidOperationException("Su\u1ea5t chi\u1ebfu kh\u00f4ng t\u1ed3n t\u1ea1i.");

                var seat = await _context.Seats.FindAsync(dto.SeatId)
                    ?? throw new InvalidOperationException("Gh\u1ebf kh\u00f4ng t\u1ed3n t\u1ea1i.");

                if (seat.RoomId != showtime.RoomId)
                    throw new InvalidOperationException("Gh\u1ebf kh\u00f4ng thu\u1ed9c ph\u00f2ng chi\u1ebfu n\u00e0y.");

                bool alreadyBooked = await _context.Tickets
                    .AnyAsync(t => t.ShowtimeId == dto.ShowtimeId
                                && t.SeatId == dto.SeatId
                                && t.Status == "Booked");

                if (alreadyBooked)
                    throw new InvalidOperationException("Gh\u1ebf n\u00e0y \u0111\u00e3 \u0111\u01b0\u1ee3c \u0111\u1eb7t. Vui l\u00f2ng ch\u1ecdn gh\u1ebf kh\u00e1c.");

                var ticket = new Ticket
                {
                    ShowtimeId = dto.ShowtimeId,
                    SeatId = dto.SeatId,
                    UserId = userId,
                    BookingTime = DateTime.Now,
                    Status = "Booked" // khoá ghế nhờ unique index
                };
                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                decimal actualPrice = showtime.Price + (seat.SeatType == "VIP" ? 15000m : 0m);

                var payment = new Payment
                {
                    TicketId = ticket.TicketId,
                    Amount = actualPrice,
                    Method = dto.PaymentMethod, // "VnPay"
                    Status = "Pending",
                    PaidAt = DateTime.Now
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
                    BookingTime = ticket.BookingTime,
                    PaymentStatus = payment.Status
                };
                return (ticketDto, payment.PaymentId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Sau khi VNPay redirect về:
        ///   - success = true  → Payment.Status = Completed
        ///   - success = false → Payment.Status = Failed, Ticket.Status = Cancelled (giải phóng ghế)
        /// </summary>
        public async Task<bool> ConfirmPaymentAsync(int paymentId, string transactionId, bool success)
        {
            var payment = await _context.Payments
                .Include(p => p.Ticket)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null) return false;
            if (payment.Status == "Completed") return true; // idempotent: tránh xử lý 2 lần

            if (success)
            {
                payment.Status = "Completed";
                payment.PaidAt = DateTime.Now;
            }
            else
            {
                payment.Status = "Failed";
                if (payment.Ticket != null) payment.Ticket.Status = "Cancelled";
            }

            await _context.SaveChangesAsync();
            return success;
        }
    }
}
