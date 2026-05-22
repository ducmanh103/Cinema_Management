using CinemaManagement.Data;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CinemaManagement.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly IShowtimeService _showtimeService;
        private readonly ITicketService _ticketService;
        private readonly IVnPayService _vnPayService;

        public BookingController(IShowtimeService showtimeService, ITicketService ticketService, IVnPayService vnPayService)
        {
            _showtimeService = showtimeService;
            _ticketService = ticketService;
            _vnPayService = vnPayService;
        }

        // GET: /Booking/SelectSeat/5  (showtimeId)
        public async Task<IActionResult> SelectSeat(int id)
        {
            var showtime = await _showtimeService.GetShowtimeByIdAsync(id);
            if (showtime == null) return NotFound();

            var seats = await _showtimeService.GetSeatStatusAsync(id);

            ViewBag.Showtime = showtime;
            return View(seats);
        }

        // POST: /Booking/Confirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(BookTicketDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            if (dto.PaymentMethod == "Cash" && !User.IsInRole("Admin") && !User.IsInRole("Staff"))
            {
                TempData["Error"] = "Khách hàng mua vé online bắt buộc phải thanh toán bằng thẻ/ngân hàng.";
                return RedirectToAction(nameof(SelectSeat), new { id = dto.ShowtimeId });
            }

            try
            {
                // Flow VnPay: tạo pending booking rồi redirect sang cổng thanh toán
                if (dto.PaymentMethod == "VnPay")
                {
                    var (ticket, paymentId) = await _ticketService.CreatePendingBookingAsync(userId, dto);
                    var model = new CinemaManagement.Models.ViewModels.VnPaymentRequestModel
                    {
                        OrderId = paymentId.ToString(),
                        OrderDescription = $"Thanh toan ve {ticket.MovieTitle} - Ghe {ticket.SeatNumber}",
                        Amount = ticket.Price,
                        Name = User.Identity?.Name ?? string.Empty,
                        CreatedDate = DateTime.Now
                    };
                    var paymentUrl = _vnPayService.CreatePaymentUrl(HttpContext, model);
                    return Redirect(paymentUrl);
                }

                // Flow Cash: tạo booking hoàn tất ngay
                var cashTicket = await _ticketService.BookTicketAsync(userId, dto);
                TempData["Success"] = "Đặt vé thành công!";
                return RedirectToAction(nameof(MyTickets));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(SelectSeat), new { id = dto.ShowtimeId });
            }
        }

        // GET: /Booking/MyTickets
        public async Task<IActionResult> MyTickets()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            var tickets = await _ticketService.GetUserTicketsAsync(userId);
            return View(tickets);
        }

        // POST: /Booking/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            var result = await _ticketService.CancelTicketAsync(id, userId);
            TempData[result ? "Success" : "Error"] = result ? "Đã huỷ vé." : "Không thể huỷ vé.";
            return RedirectToAction(nameof(MyTickets));
        }
    }
}
