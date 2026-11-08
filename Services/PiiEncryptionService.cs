using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CinemaManagement.Services
{
    public class PiiEncryptionService : IPiiEncryptionService
    {
        private const string Prefix = "ENC:v1:";
        private readonly byte[] _key;
        private readonly byte[] _hashKey;
        private readonly ILogger<PiiEncryptionService> _logger;

        public PiiEncryptionService(IConfiguration config, ILogger<PiiEncryptionService> logger)
        {
            _logger = logger;

            var rawKey = config["Security:PiiEncryptionKey"] 
                ?? Environment.GetEnvironmentVariable("PII_ENCRYPTION_KEY")
                ?? "CinemaHub_SuperSecure_DefaultMasterKey_PII_AES256GCM_2026!";

            // Tạo key mã hoá 256-bit chuẩn từ secret
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey + "#CinemaHub_Enc_Pepper"));

            // Tạo key Blind Index tách biệt
            _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey + "#CinemaHub_Hmac_Salt"));
        }

        public string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            // Nếu đã được mã hóa trước đó thì không mã hóa đè
            if (plainText.StartsWith(Prefix, StringComparison.Ordinal)) return plainText;

            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);

                // AES-GCM chuẩn: Nonce/IV 12 bytes, Tag 16 bytes
                var nonce = new byte[12];
                RandomNumberGenerator.Fill(nonce);

                var tag = new byte[16];
                var cipherBytes = new byte[plainBytes.Length];

                using var aesGcm = new AesGcm(_key, 16);
                aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

                // Ghép [Nonce (12)] + [Tag (16)] + [Ciphertext (N)]
                var resultBytes = new byte[nonce.Length + tag.Length + cipherBytes.Length];
                Buffer.BlockCopy(nonce, 0, resultBytes, 0, nonce.Length);
                Buffer.BlockCopy(tag, 0, resultBytes, nonce.Length, tag.Length);
                Buffer.BlockCopy(cipherBytes, 0, resultBytes, nonce.Length + tag.Length, cipherBytes.Length);

                return Prefix + Convert.ToBase64String(resultBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi mã hoá PII");
                return plainText;
            }
        }

        public string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            // Nếu dữ liệu chưa được mã hoá (dữ liệu cũ trong DB), bảo lưu tương thích ngược
            if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return cipherText;
            }

            try
            {
                var base64 = cipherText[Prefix.Length..];
                var data = Convert.FromBase64String(base64);

                if (data.Length < 12 + 16)
                {
                    _logger.LogWarning("Dữ liệu mã hoá không đúng định dạng (quá ngắn)");
                    return cipherText;
                }

                var nonce = new byte[12];
                var tag = new byte[16];
                var cipherBytes = new byte[data.Length - 12 - 16];

                Buffer.BlockCopy(data, 0, nonce, 0, 12);
                Buffer.BlockCopy(data, 12, tag, 0, 16);
                Buffer.BlockCopy(data, 28, cipherBytes, 0, cipherBytes.Length);

                var plainBytes = new byte[cipherBytes.Length];
                using var aesGcm = new AesGcm(_key, 16);
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi giải mã PII");
                return "[Dữ liệu mã hoá]";
            }
        }

        public string? ComputeEmailHash(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var normalized = email.Trim().ToLowerInvariant();
            using var hmac = new HMACSHA256(_hashKey);
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}

