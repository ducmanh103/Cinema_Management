using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CinemaManagement.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IVnPayService _vnPayService;
        private readonly ITicketService _ticketService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IVnPayService vnPayService,
            ITicketService ticketService,
            ILogger<PaymentController> logger)
        {
            _vnPayService = vnPayService;
            _ticketService = ticketService;
            _logger = logger;
        }

        /// <summary>
        /// Bước 1: User submit chọn ghế + phương thức "VnPay" → tạo booking pending → redirect sang cổng VNPay.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVnPayUrl(BookTicketDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            try
            {
                var (ticket, paymentId) = await _ticketService.CreatePendingBookingAsync(userId, dto);

                var model = new VnPaymentRequestModel
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
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("SelectSeat", "Booking", new { id = dto.ShowtimeId });
            }
        }

        /// <summary>
        /// Endpoint AJAX (tuỳ chọn) cho frontend muốn fetch URL thay vì form submit.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateVnPayUrlAjax([FromBody] BookTicketDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            try
            {
                var (ticket, paymentId) = await _ticketService.CreatePendingBookingAsync(userId, dto);
                var model = new VnPaymentRequestModel
                {
                    OrderId = paymentId.ToString(),
                    OrderDescription = $"Thanh toan ve {ticket.MovieTitle} - Ghe {ticket.SeatNumber}",
                    Amount = ticket.Price,
                    Name = User.Identity?.Name ?? string.Empty,
                    CreatedDate = DateTime.Now
                };
                var paymentUrl = _vnPayService.CreatePaymentUrl(HttpContext, model);
                return Ok(new { paymentUrl, ticketId = ticket.TicketId, paymentId });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bước 2: VNPay redirect về sau khi user thanh toán xong (vnp_ReturnUrl).
        /// Verify chữ ký → cập nhật Payment/Ticket → render view kết quả.
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // VNPay redirect không có cookie nên cần allow
        public async Task<IActionResult> VnpayReturn()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);

            if (string.IsNullOrEmpty(response.OrderId))
            {
                _logger.LogWarning("VNPay return không có OrderId");
                TempData["Error"] = "Phản hồi VNPay không hợp lệ.";
                return View("PaymentFail", response);
            }

            if (!int.TryParse(response.OrderId, out int paymentId))
            {
                TempData["Error"] = "Mã đơn hàng không hợp lệ.";
                return View("PaymentFail", response);
            }

            // Nếu chữ ký không hợp lệ → coi như failure và huỷ ticket
            if (response.Token == null || !response.Success || response.VnPayResponseCode != "00")
            {
                await _ticketService.ConfirmPaymentAsync(paymentId, response.TransactionId, false);
                TempData["Error"] = $"Thanh toán không thành công. Mã lỗi: {response.VnPayResponseCode}";
                return View("PaymentFail", response);
            }

            // Thành công
            await _ticketService.ConfirmPaymentAsync(paymentId, response.TransactionId, true);
            TempData["Success"] = "Thanh toán VNPay thành công!";
            return View("PaymentSuccess", response);
        }
    }
}
