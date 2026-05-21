namespace CinemaManagement.Models.ViewModels
{
    using System;

    /// <summary>
    /// DTO truyền vào VnPayService để khởi tạo URL thanh toán.
    /// </summary>
    public class VnPaymentRequestModel
    {
        /// <summary>
        /// Mã đơn hàng nội bộ — ở đây dùng PaymentId để dễ tra cứu khi VNPay redirect về.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả đơn hàng hiển thị trên cổng VNPay.
        /// </summary>
        public string OrderDescription { get; set; } = string.Empty;

        /// <summary>
        /// Loại đơn (mặc định "other" cho sandbox demo).
        /// </summary>
        public string OrderType { get; set; } = "other";

        /// <summary>
        /// Số tiền thanh toán (đơn vị VND, sẽ tự nhân 100 trước khi gửi sang VNPay).
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Họ tên khách hàng (tuỳ chọn).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Kết quả xử lý sau khi VNPay redirect về ReturnUrl.
    /// </summary>
    public class VnPaymentResponseModel
    {
        public bool Success { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string OrderDescription { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string VnPayResponseCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
