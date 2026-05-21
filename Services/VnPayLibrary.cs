using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace CinemaManagement.Services
{
    /// <summary>
    /// Helper chuẩn của VNPay (lấy từ tài liệu sandbox.vnpayment.vn) để:
    ///  - Sắp xếp tham số theo thứ tự alphabet (Ordinal)
    ///  - URL-encode đúng chuẩn VNPay yêu cầu
    ///  - Ký HMAC-SHA512 trên chuỗi raw
    ///  - Verify chữ ký khi VNPay redirect về
    /// </summary>
    public class VnPayLibrary
    {
        public const string VERSION = "2.1.0";

        private readonly SortedList<string, string> _requestData = new(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new(new VnPayCompare());

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) _requestData.Add(key, value);
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) _responseData.Add(key, value);
        }

        public string GetResponseData(string key) =>
            _responseData.TryGetValue(key, out var v) ? v : string.Empty;

        /// <summary>
        /// Build URL thanh toán có kèm vnp_SecureHash.
        /// </summary>
        public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
        {
            var data = new StringBuilder();
            foreach (var kv in _requestData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    data.Append(WebUtility.UrlEncode(kv.Key))
                        .Append('=')
                        .Append(WebUtility.UrlEncode(kv.Value))
                        .Append('&');
                }
            }

            // bỏ '&' cuối
            string queryString = data.ToString();
            if (queryString.Length > 0) queryString = queryString.Remove(queryString.Length - 1, 1);

            string signData = queryString;
            string vnpSecureHash = HmacSHA512(vnpHashSecret, signData);

            return $"{baseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
        }

        /// <summary>
        /// Validate chữ ký đối chiếu với HashSecret.
        /// </summary>
        public bool ValidateSignature(string inputHash, string secretKey)
        {
            string rspRaw = GetResponseRaw();
            string myChecksum = HmacSHA512(secretKey, rspRaw);
            return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetResponseRaw()
        {
            // VNPay yêu cầu loại bỏ vnp_SecureHash & vnp_SecureHashType khi tính chữ ký
            _responseData.Remove("vnp_SecureHashType");
            _responseData.Remove("vnp_SecureHash");

            var data = new StringBuilder();
            foreach (var kv in _responseData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    data.Append(WebUtility.UrlEncode(kv.Key))
                        .Append('=')
                        .Append(WebUtility.UrlEncode(kv.Value))
                        .Append('&');
                }
            }
            if (data.Length > 0) data.Remove(data.Length - 1, 1);
            return data.ToString();
        }

        public static string HmacSHA512(string key, string inputData)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            byte[] hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
            var sb = new StringBuilder(hashValue.Length * 2);
            foreach (var b in hashValue) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Lấy IP của client để gửi sang VNPay (vnp_IpAddr).
        /// </summary>
        public static string GetIpAddress(HttpContext context)
        {
            try
            {
                var remoteIp = context.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(remoteIp) || remoteIp == "::1") return "127.0.0.1";
                return remoteIp;
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }

    /// <summary>
    /// Comparer ordinal — VNPay quy định sort theo Ordinal (en-US).
    /// </summary>
    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            var info = CompareInfo.GetCompareInfo("en-US");
            return info.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}
