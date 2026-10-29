using System.Security.Cryptography;
using System.Text;

namespace HrPortal.Services
{
    /// <summary>
    /// Encrypts what goes in the password vault with AES-256-GCM,
    /// under a key read from Vault:Key: 32 random bytes, base64. It
    /// lives in the host's settings and nowhere in the database, so
    /// the database alone, or a backup of it, reads as noise.
    ///
    /// Losing the key loses the vault, so it should also be kept
    /// somewhere safe away from the server.
    /// </summary>
    public class VaultCipher
    {
        public const string KeySetting = "Vault:Key";

        private const string Prefix = "v1:";

        private const int NonceSize = 12;

        private const int TagSize = 16;

        private static readonly byte[] Purpose =
            Encoding.UTF8.GetBytes("HrPortal.Vault.v1");

        private readonly byte[]? _key;

        public VaultCipher(IConfiguration configuration)
        {
            var configured = configuration[KeySetting];

            if (string.IsNullOrWhiteSpace(configured))
                return;

            try
            {
                var key = Convert.FromBase64String(configured.Trim());

                if (key.Length == 32)
                    _key = key;
            }
            catch (FormatException)
            {
            }
        }

        /// <summary>
        /// Whether a usable key is set. Without one the vault says
        /// how to set it up and stores nothing.
        /// </summary>
        public bool IsReady => _key != null;

        public string Encrypt(string plain)
        {
            var key = _key ?? throw NotReady();

            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var data = Encoding.UTF8.GetBytes(plain);
            var cipher = new byte[data.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, data, cipher, tag, Purpose);

            CryptographicOperations.ZeroMemory(data);

            var packed = new byte[NonceSize + TagSize + cipher.Length];

            nonce.CopyTo(packed, 0);
            tag.CopyTo(packed, NonceSize);
            cipher.CopyTo(packed, NonceSize + TagSize);

            return Prefix + Convert.ToBase64String(packed);
        }

        public string Decrypt(string stored)
        {
            var key = _key ?? throw NotReady();

            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
                throw new CryptographicException("Not a vault value.");

            var packed = Convert.FromBase64String(stored[Prefix.Length..]);

            if (packed.Length < NonceSize + TagSize)
                throw new CryptographicException("Not a vault value.");

            var nonce = packed.AsSpan(0, NonceSize);
            var tag = packed.AsSpan(NonceSize, TagSize);
            var cipher = packed.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(nonce, cipher, tag, plain, Purpose);

            return Encoding.UTF8.GetString(plain);
        }

        private static InvalidOperationException NotReady() =>
            new($"{KeySetting} is not set to a 32 byte base64 key.");
    }
}
