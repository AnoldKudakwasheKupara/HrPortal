using System.Security.Cryptography;
using System.Text;

namespace HrPortal.Services
{
    /// <summary>
    /// Encrypts what goes in the password vault with AES-256-GCM. The
    /// portal makes its own key the first time it is needed and keeps it
    /// on the server's disk, beside the sign-in keys and outside the
    /// database, so there is nothing to set up. Vault:Key, 32 random bytes
    /// in base64, takes its place if it is set.
    ///
    /// The key goes in the portal's backups, because losing it loses the
    /// vault.
    /// </summary>
    public class VaultCipher
    {
        public const string KeySetting = "Vault:Key";

        private const string Prefix = "v1:";

        private const int NonceSize = 12;

        private const int TagSize = 16;

        private static readonly byte[] Purpose =
            Encoding.UTF8.GetBytes("HrPortal.Vault.v1");

        private readonly byte[] _key;

        public VaultCipher(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configured = configuration[KeySetting];

            if (!string.IsNullOrWhiteSpace(configured))
            {
                _key = Parse(configured.Trim())
                    ?? throw new InvalidOperationException($"{KeySetting} is not a 32 byte base64 key.");

                return;
            }

            KeyFilePath = Path.Combine(environment.ContentRootPath, "App_Data", "keys", "vault.key");

            _key = LoadOrCreate(KeyFilePath);
        }

        /// <summary>Where the key is kept, when the portal made it itself.</summary>
        public string? KeyFilePath { get; }

        private static byte[]? Parse(string text)
        {
            try
            {
                var key = Convert.FromBase64String(text);

                return key.Length == 32 ? key : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static byte[] LoadOrCreate(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (!File.Exists(path))
            {
                try
                {
                    var options = new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write
                    };

                    if (!OperatingSystem.IsWindows())
                        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

                    using var file = new FileStream(path, options);

                    file.Write(Encoding.ASCII.GetBytes(
                        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
                }
                catch (IOException) when (File.Exists(path))
                {
                }
            }

            return Parse(File.ReadAllText(path).Trim())
                ?? throw new InvalidOperationException($"{path} does not hold a 32 byte base64 key.");
        }

        public string Encrypt(string plain)
        {
            var key = _key;

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
            var key = _key;

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
    }
}
