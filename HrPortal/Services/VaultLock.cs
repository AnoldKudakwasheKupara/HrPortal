using Microsoft.Extensions.Caching.Memory;

namespace HrPortal.Services
{
    /// <summary>
    /// Opening the vault asks for the person's password again, even
    /// though they are signed in, and stays open for a short while
    /// after. Somebody who walks up to a signed-in desk cannot read
    /// the passwords, and a stolen sign-in cookie on its own is not
    /// enough either.
    ///
    /// Kept on the server, by sign-in, so signing out or the portal
    /// restarting locks it again.
    /// </summary>
    public class VaultLock
    {
        public static readonly TimeSpan OpenFor = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _cache;

        public VaultLock(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static string Key(int userId) => $"vault-open:{userId}";

        public bool IsOpen(int userId) => _cache.TryGetValue(Key(userId), out _);

        public void Open(int userId) =>
            _cache.Set(Key(userId), true, OpenFor);

        public void Close(int userId) => _cache.Remove(Key(userId));
    }
}
