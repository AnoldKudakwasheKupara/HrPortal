using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>One person and whether they have read the notice.</summary>
    public record PrivacyNoticeReader(
        int EmployeeId,
        string Name,
        string? Department,
        DateTime? AcknowledgedAt);

    /// <summary>
    /// Keeps the record of who has confirmed reading the privacy
    /// notice. Reading it is not required to use the portal; the
    /// record shows who chose to confirm.
    ///
    /// Changing what the notice says means raising Version, so
    /// confirmations of the old wording are not counted for the new.
    /// </summary>
    public class PrivacyNoticeService
    {
        public const string Version = "2026-10-29";

        public static readonly DateTime VersionDate = new(2026, 10, 29);

        private readonly ApplicationDbContext _context;

        public PrivacyNoticeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DateTime?> AcknowledgedAtAsync(int userId)
        {
            return await _context.PrivacyNoticeAcknowledgements
                .AsNoTracking()
                .Where(a => a.UserId == userId && a.Version == Version)
                .Select(a => (DateTime?)a.AcknowledgedAt)
                .FirstOrDefaultAsync();
        }

        public async Task AcknowledgeAsync(int userId, string? ipAddress)
        {
            if (await AcknowledgedAtAsync(userId) != null)
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
    }
}
