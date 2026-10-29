using System.Security.Claims;
using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HrPortal.Services
{
    /// <summary>One person and whether they have read the notice.</summary>
    public record PrivacyNoticeReader(
        int EmployeeId,
        string Name,
        string? Department,
        DateTime? AcknowledgedAt);

    /// <summary>
    /// Makes sure everybody who signs in has read the privacy notice
    /// once, and keeps the record of who has.
    ///
    /// Changing what the notice says means raising Version, which
    /// asks everybody to read it again.
    /// </summary>
    public class PrivacyNoticeService
    {
        public const string Version = "2026-10-29";

        public static readonly DateTime VersionDate = new(2026, 10, 29);

        private static readonly TimeSpan Remember = TimeSpan.FromHours(1);

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public PrivacyNoticeService(
            ApplicationDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<DateTime?> AcknowledgedAtAsync(int userId)
        {
            return await _context.PrivacyNoticeAcknowledgements
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.Version == Version)
                .Select(a => (DateTime?)a.AcknowledgedAt)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Asked on every page, so a yes is remembered for an hour
        /// rather than looked up each time. A no is not remembered:
        /// it lasts only until they read the notice.
        /// </summary>
        private async Task<bool> HasReadAsync(int userId)
        {
            var key = $"privacy:{Version}:{userId}";

            if (_cache.TryGetValue(key, out _))
                return true;

            var read = await _context.PrivacyNoticeAcknowledgements
                .AnyAsync(a => a.UserId == userId && a.Version == Version);

            if (read)
                _cache.Set(key, true, Remember);

            return read;
        }

        public async Task AcknowledgeAsync(int userId, string? ipAddress)
        {
            if (await HasReadAsync(userId))
                return;

            _context.PrivacyNoticeAcknowledgements.Add(
                new PrivacyNoticeAcknowledgement
                {
                    UserId = userId,
                    Version = Version,
                    AcknowledgedAt = DateTime.UtcNow,
                    IpAddress = ipAddress
                });

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Everybody still working here who can sign in, and when
        /// they read this version, newest readers last and those who
        /// have not read it first.
        /// </summary>
        public async Task<List<PrivacyNoticeReader>> ReadersAsync()
        {
            var people = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive && e.UserId != null)
                .Select(e => new
                {
                    e.Id,
                    Name = e.FirstName + " " + e.LastName,
                    Department = e.Department == null ? null : e.Department.Name,
                    AcknowledgedAt = _context.PrivacyNoticeAcknowledgements
                        .Where(a => a.UserId == e.UserId && a.Version == Version)
                        .Select(a => (DateTime?)a.AcknowledgedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return people
                .OrderBy(p => p.AcknowledgedAt.HasValue)
                .ThenBy(p => p.AcknowledgedAt)
                .ThenBy(p => p.Name)
                .Select(p => new PrivacyNoticeReader(
                    p.Id,
                    p.Name,
                    p.Department,
                    p.AcknowledgedAt))
                .ToList();
        }

        /// <summary>
        /// Sends somebody who is signed in but has not read this
        /// version to the notice, then back to where they were going.
        ///
        /// Only page visits are stopped. A form on its way in, the
        /// keep-alive and anything a script asks for go through, so
        /// nothing half-done is lost; and the sign-in pages and the
        /// notice itself are always open.
        /// </summary>
        public static async Task GateAsync(HttpContext context, Func<Task> next)
        {
            var request = context.Request;

            if (context.User.Identity?.IsAuthenticated != true ||
                !HttpMethods.IsGet(request.Method) ||
                request.Headers.XRequestedWith == "XMLHttpRequest" ||
                IsOpen(request.Path))
            {
                await next();
                return;
            }

            var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(id, out var userId))
            {
                await next();
                return;
            }

            var notice = context.RequestServices
                .GetRequiredService<PrivacyNoticeService>();

            if (await notice.HasReadAsync(userId))
            {
                await next();
                return;
            }

            var back = request.PathBase + request.Path + request.QueryString;

            context.Response.Redirect(
                "/Home/Privacy?returnUrl=" + Uri.EscapeDataString(back));
        }

        private static bool IsOpen(PathString path) =>
            path.StartsWithSegments("/Home/Privacy") ||
            path.StartsWithSegments("/Privacy") ||
            path.StartsWithSegments("/Home/Error") ||
            path.StartsWithSegments("/Session") ||
            path.StartsWithSegments("/Identity");
    }
}
