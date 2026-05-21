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
            var bankCode   = _config["VnPay:BankCode"]   ?? string.Empty; // "VNPAYQR" → vào thẳng QR

            // Nếu ReturnUrl không cấu hình thì tự sinh dựa trên request hiện tại
            var returnUrl  = _config["VnPay:ReturnUrl"];
            if (string.IsNullOrWhiteSpace(returnUrl))
                returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/Payment/VnpayReturn";

            var pay = new VnPayLibrary();

            pay.AddRequestData("vnp_Version",   version);
            pay.AddRequestData("vnp_Command",   command);
            pay.AddRequestData("vnp_TmnCode",   tmnCode);
            // VNPay yêu cầu Amount nhân 100 và làm tròn về long
            pay.AddRequestData("vnp_Amount",    ((long)(model.Amount * 100M)).ToString());
            pay.AddRequestData("vnp_CreateDate",model.CreatedDate.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode",  currCode);
            pay.AddRequestData("vnp_IpAddr",    VnPayLibrary.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale",    locale);
            pay.AddRequestData("vnp_OrderInfo", string.IsNullOrWhiteSpace(model.OrderDescription)
                                                    ? $"Thanh toan ve xem phim {model.OrderId}"
                                                    : model.OrderDescription);
            pay.AddRequestData("vnp_OrderType", model.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", returnUrl);
            // TxnRef phải duy nhất. Dùng paymentId + 6 số cuối ticks → vẫn parse được, không chứa ký tự đặc biệt.
            string ticksTail = (DateTime.Now.Ticks % 1000000).ToString("D6");
            pay.AddRequestData("vnp_TxnRef", $"{model.OrderId}{ticksTail}");

            // Nếu cấu hình BankCode (ví dụ VNPAYQR) → vào thẳng trang QR
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

            // TxnRef dạng "{paymentId}{6-digit-ticks}". Strip 6 chữ số cuối để lấy paymentId.
            string orderId = txnRef;
            if (txnRef.Length > 6 && txnRef.All(char.IsDigit))
                orderId = txnRef.Substring(0, txnRef.Length - 6);
            else if (txnRef.Contains('_'))
                orderId = txnRef.Split('_')[0]; // fallback cho format cũ

            decimal amount = 0;
            if (long.TryParse(amountRaw, out var raw)) amount = raw / 100M;

            bool isValid = pay.ValidateSignature(vnpSecureHash, hashSecret);
            if (!isValid)
            {
                return new VnPaymentResponseModel { Success = false, OrderId = orderId };
            }

            // ResponseCode "00" = thành công
            return new VnPaymentResponseModel
            {
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
