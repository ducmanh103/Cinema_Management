namespace CinemaManagement.Services
{
    public interface IPiiEncryptionService
    {
        /// <summary>
        /// Mã hoá dữ liệu cá nhân (Email, FullName) bằng chuẩn AES-256-GCM có xác thực toàn vẹn.
        /// </summary>
        string? Encrypt(string? plainText);

        /// <summary>
        /// Giải mã dữ liệu cá nhân. Tự động nhận diện dữ liệu chưa mã hoá để tương thích ngược.
        /// </summary>
        string? Decrypt(string? cipherText);

        /// <summary>
        /// Tính toán Blind Index (HMAC-SHA256) cho Email để hỗ trợ tìm kiếm chính xác và ràng buộc Unique trong DB.
        /// </summary>
        string? ComputeEmailHash(string? email);
    }
}

