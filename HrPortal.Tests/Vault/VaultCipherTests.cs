using HrPortal.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace HrPortal.Tests.Vault
{
    public sealed class VaultCipherTests : IDisposable
    {
        private sealed class Env : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = string.Empty;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "HrPortal";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = "Testing";
        }

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vault-test-" + Guid.NewGuid().ToString("N"));

        private VaultCipher Cipher(string? key = null) =>
            new(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { [VaultCipher.KeySetting] = key })
                    .Build(),
                new Env { ContentRootPath = _root });

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Fact]
        public void It_works_with_no_setup_and_makes_its_own_key()
        {
            var cipher = Cipher();

            Assert.True(File.Exists(cipher.KeyFilePath));
            Assert.Equal("router-pass!", cipher.Decrypt(cipher.Encrypt("router-pass!")));
        }

        [Fact]
        public void What_is_stored_is_not_the_password()
        {
            Assert.DoesNotContain("router-pass", Cipher().Encrypt("router-pass!"));
        }

        [Fact]
        public void Passwords_can_still_be_read_after_a_restart()
        {
            var stored = Cipher().Encrypt("router-pass!");

            Assert.Equal("router-pass!", Cipher().Decrypt(stored));
        }

        [Fact]
        public void A_key_in_the_settings_is_used_instead_and_no_file_is_made()
        {
            var cipher = Cipher(Convert.ToBase64String(new byte[32]));

            Assert.Null(cipher.KeyFilePath);
            Assert.Equal("x", cipher.Decrypt(cipher.Encrypt("x")));
            Assert.False(Directory.Exists(_root));
        }
    }
}
