using CinemaManagement.Helpers;
using CinemaManagement.Models.ViewModels;
using CinemaManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace CinemaManagement.Controllers
{
    [Authorize]
    [EnableRateLimiting("BookingLimit")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class PaymentController : Controller
    {
        private readonly IVnPayService _vnPayService;
        private readonly ITicketService _ticketService;
        private readonly ISecurityAlertService _securityAlert;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IVnPayService vnPayService,
            ITicketService ticketService,
            ISecurityAlertService securityAlert,
            ILogger<PaymentController> logger)
        {
            _vnPayService = vnPayService;
            _ticketService = ticketService;
            _securityAlert = securityAlert;
            _logger = logger;
        }

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
                    CreatedDate = VietnamTime.Now
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

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                    CreatedDate = VietnamTime.Now
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
        /// VNPay ReturnUrl (Sandbox & Production):
        /// Tuyệt đối KHÔNG huỷ vé nếu chữ ký vnp_SecureHash không hợp lệ (chống tấn công DoS huỷ vé người khác).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VnpayReturn()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = _vnPayService.PaymentExecute(Request.Query);

            if (string.IsNullOrEmpty(response.OrderId) || !int.TryParse(response.OrderId, out int paymentId))
            {
                _logger.LogWarning("VNPay return thiếu hoặc sai OrderId từ IP {IP}", ip);
                TempData["Error"] = "Phản hồi VNPay không hợp lệ.";
                return View("PaymentFail", response);
            }

            // 1. Chữ ký không hợp lệ -> KHÔNG cập nhật/huỷ DB, bắn cảnh báo bảo mật mức HIGH về Telegram
            if (!response.IsValidSignature)
            {
                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.High,
                    "VNPAY_SIGNATURE_TAMPERING",
                    "Phát hiện giả mạo chữ ký callback VNPay (ReturnUrl)",
                    $"OrderId={paymentId}, Amount={response.Amount:N0}₫, ResponseCode={response.VnPayResponseCode}. Đã chặn cập nhật DB để bảo vệ vé.",
                    IpAddress: ip,
                    Username: User.Identity?.Name,
                    Path: Request.Path));

                TempData["Error"] = "Chữ ký bảo mật VNPay không hợp lệ. Giao dịch bị từ chối.";
                return View("PaymentFail", response);
            }

            // 2. Chữ ký hợp lệ nhưng người dùng huỷ hoặc giao dịch thất bại trên cổng VNPay
            bool isSuccess = response.Success && response.VnPayResponseCode == "00";
            var (handled, errorReason) = await _ticketService.ConfirmPaymentAsync(
                paymentId, response.TransactionId, isSuccess, response.Amount);

            if (errorReason != null && errorReason.Contains("không khớp"))
            {
                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.High,
                    "VNPAY_AMOUNT_MISMATCH",
                    "Phát hiện sai lệch số tiền thanh toán VNPay",
                    $"OrderId={paymentId}: {errorReason}",
                    IpAddress: ip,
                    Username: User.Identity?.Name,
                    Path: Request.Path));

                TempData["Error"] = errorReason;
                return View("PaymentFail", response);
            }

            if (!isSuccess || !handled)
            {
                TempData["Error"] = errorReason ?? $"Thanh toán không thành công. Mã phản hồi VNPay: {response.VnPayResponseCode}";
                return View("PaymentFail", response);
            }

            TempData["Success"] = "Thanh toán VNPay thành công!";
            return View("PaymentSuccess", response);
        }

        /// <summary>
        /// Endpoint IPN (Server-to-Server / Sandbox Test) chuẩn VNPay.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VnpayIpn()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var response = _vnPayService.PaymentExecute(Request.Query);

            if (!response.IsValidSignature)
            {
                _securityAlert.EnqueueAlert(new SecurityAlert(
                    SecurityAlertLevel.High,
                    "VNPAY_IPN_TAMPERING",
                    "Giả mạo chữ ký tại cổng VNPay IPN",
                    $"OrderId={response.OrderId}, Amount={response.Amount:N0}₫",
                    IpAddress: ip,
                    Path: Request.Path));

                return Json(new { RspCode = "97", Message = "Invalid signature" });
            }

            if (!int.TryParse(response.OrderId, out int paymentId))
                return Json(new { RspCode = "01", Message = "Order not found" });

            bool isSuccess = response.Success && response.VnPayResponseCode == "00";
            var (_, errorReason) = await _ticketService.ConfirmPaymentAsync(
                paymentId, response.TransactionId, isSuccess, response.Amount);

            if (errorReason != null && errorReason.Contains("không khớp"))
                return Json(new { RspCode = "04", Message = "Invalid amount" });

            return Json(new { RspCode = "00", Message = "Confirm Success" });
        }
    }
}
