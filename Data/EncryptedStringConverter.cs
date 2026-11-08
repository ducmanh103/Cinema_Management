using CinemaManagement.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CinemaManagement.Data
{
    public class EncryptedStringConverter : ValueConverter<string?, string?>
    {
        public static IPiiEncryptionService? EncryptionService { get; set; }

        public EncryptedStringConverter()
            : base(
                v => EncryptionService != null ? EncryptionService.Encrypt(v) : v,
                v => EncryptionService != null ? EncryptionService.Decrypt(v) : v)
        {
        }
    }
}

