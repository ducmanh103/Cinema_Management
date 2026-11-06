using CinemaManagement.Helpers;
using CinemaManagement.Models.ViewModels;

namespace CinemaManagement.Services
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _config;

        public VnPayService(IConfiguration config) => _config = config;

        public string CreatePaymentUrl(HttpContext context, VnPaymentRequestModel model)
        {
            var tmnCode    = _config["VnPay:TmnCode"]    ?? throw new InvalidOperationException("VnPay:TmnCode chưa cấu hình.");
            var hashSecret = _config["VnPay:HashSecret"] ?? throw new InvalidOperationException("VnPay:HashSecret chưa cấu hình.");
            var baseUrl    = _config["VnPay:BaseUrl"]    ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            var version    = _config["VnPay:Version"]    ?? VnPayLibrary.VERSION;
            var command    = _config["VnPay:Command"]    ?? "pay";
            var currCode   = _config["VnPay:CurrCode"]   ?? "VND";
            var locale     = _config["VnPay:Locale"]     ?? "vn";
            var bankCode   = _config["VnPay:BankCode"]   ?? string.Empty;

            var returnUrl  = _config["VnPay:ReturnUrl"];
            if (string.IsNullOrWhiteSpace(returnUrl))
                returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/Payment/VnpayReturn";

            // VNPay yêu cầu thời gian theo múi giờ GMT+7 (yyyyMMddHHmmss)
            var vnNow = VietnamTime.Now;
            var createDate = model.CreatedDate.ToVietnamTime();
            if (createDate == default) createDate = vnNow;

            var pay = new VnPayLibrary();

            pay.AddRequestData("vnp_Version",   version);
            pay.AddRequestData("vnp_Command",   command);
            pay.AddRequestData("vnp_TmnCode",   tmnCode);
            pay.AddRequestData("vnp_Amount",    ((long)(model.Amount * 100M)).ToString());
            pay.AddRequestData("vnp_CreateDate", createDate.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode",  currCode);
            pay.AddRequestData("vnp_IpAddr",    VnPayLibrary.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale",    locale);
            pay.AddRequestData("vnp_OrderInfo", string.IsNullOrWhiteSpace(model.OrderDescription)
                                                    ? $"Thanh toan ve xem phim {model.OrderId}"
                                                    : model.OrderDescription);
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", returnUrl);

            string timestamp = vnNow.ToString("yyyyMMddHHmmss");
            pay.AddRequestData("vnp_TxnRef", $"{model.OrderId}_{timestamp}");

            var expireDate = createDate.AddMinutes(15);
            pay.AddRequestData("vnp_ExpireDate", expireDate.ToString("yyyyMMddHHmmss"));

            if (!string.IsNullOrWhiteSpace(bankCode))
                pay.AddRequestData("vnp_BankCode", bankCode);

            return pay.CreateRequestUrl(baseUrl, hashSecret);
        }

        public VnPaymentResponseModel PaymentExecute(IQueryCollection collections)
        {
            var hashSecret = _config["VnPay:HashSecret"]
                             ?? throw new InvalidOperationException("VnPay:HashSecret chưa cấu hình.");

            var pay = new VnPayLibrary();
            foreach (var (key, value) in collections)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    pay.AddResponseData(key, value.ToString());
            }

            var txnRef          = pay.GetResponseData("vnp_TxnRef");
            var vnpTransNo      = pay.GetResponseData("vnp_TransactionNo");
            var responseCode    = pay.GetResponseData("vnp_ResponseCode");
            var orderInfo       = pay.GetResponseData("vnp_OrderInfo");
            var amountRaw       = pay.GetResponseData("vnp_Amount");
            var vnpSecureHash   = collections.FirstOrDefault(p => p.Key == "vnp_SecureHash").Value.ToString();

            string orderId = txnRef;
            if (txnRef.Contains('_'))
                orderId = txnRef.Split('_')[0];
            else if (txnRef.Length > 6 && txnRef.All(char.IsDigit))
                orderId = txnRef.Substring(0, txnRef.Length - 6);

            decimal amount = 0;
            if (long.TryParse(amountRaw, out var raw)) amount = raw / 100M;

            bool isValid = !string.IsNullOrEmpty(vnpSecureHash) && pay.ValidateSignature(vnpSecureHash, hashSecret);
            if (!isValid)
            {
                return new VnPaymentResponseModel
                {
                    IsValidSignature  = false,
                    Success           = false,
                    OrderId           = orderId,
                    Amount            = amount,
                    VnPayResponseCode = responseCode
                };
            }

            return new VnPaymentResponseModel
            {
                IsValidSignature  = true,
                Success           = responseCode == "00",
                PaymentMethod     = "VnPay",
                OrderDescription  = orderInfo,
                OrderId           = orderId,
                TransactionId     = vnpTransNo,
                Token             = vnpSecureHash,
                VnPayResponseCode = responseCode,
                Amount            = amount
            };
        }
    }
}
