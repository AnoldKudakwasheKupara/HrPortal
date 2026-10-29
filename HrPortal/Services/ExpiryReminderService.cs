using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Warns about licences and certifications before they run out:
    /// once a month ahead, which is time to arrange a renewal, and
    /// again a week ahead, which is the last call.
    ///
    /// Each reminder goes once. A server that was off on the day
    /// catches up the next morning, and something recorded with
    /// less than a month to go gets only the reminder it is now due.
    /// </summary>
    public class ExpiryReminderService
    {
        public const int FirstReminderDays = 30;

        public const int LastReminderDays = 7;

        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly ApproverDirectory _people;
        private readonly ILogger<ExpiryReminderService> _logger;

        public ExpiryReminderService(
            ApplicationDbContext context,
            NotificationService notifications,
            ApproverDirectory people,
            ILogger<ExpiryReminderService> logger)
        {
            _context = context;
            _notifications = notifications;
            _people = people;
            _logger = logger;
        }

        public async Task<int> SendDueAsync(DateTime today)
        {
            var day = today.Date;

            var sent = await LicencesAsync(day) + await CertificationsAsync(day);

            if (sent > 0)
            {
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Sent {Count} expiry reminder(s) for {Day:d}.",
                    sent,
                    day);
            }

            return sent;
        }

        /// <summary>
        /// Licences are the company's, so the administrators who look
        /// after them are told, and whoever one is assigned to as well.
        /// </summary>
        private async Task<int> LicencesAsync(DateTime day)
        {
            var licences = await _context.Licenses
                .AsNoTracking()
                .Include(l => l.Vendor)
                .Include(l => l.AssignedToEmployee)
                .Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate >= day &&
                    l.ExpiryDate < day.AddDays(FirstReminderDays + 1))
                .ToListAsync();

            if (licences.Count == 0)
                return 0;

            var administrators = await _people
                .EmployeeIdsWithRoleAsync(Roles.Administrator);

            var sent = 0;

            foreach (var licence in licences)
            {
                var expiry = licence.ExpiryDate.Date;

                if (await DueStageAsync(
                        ExpiryReminderKind.Licence,
                        licence.Id,
                        expiry,
                        day) is not int stage)
                {
                    continue;
                }

                var when = When(expiry, day);
                var kind = KindFor(stage);

                var from = licence.Vendor == null
                    ? string.Empty
                    : $" from {licence.Vendor.Name}";

                var renewal = licence.AutoRenews
                    ? " It is set to renew automatically, so check that " +
                      "the renewal goes through and record it in the portal."
                    : " Arrange the renewal before then and record it in " +
                      "the portal.";

                foreach (var administrator in administrators)
                {
                    _notifications.Notify(
                        administrator,
                        $"{licence.Name} expires {when}",
                        $"The {Described(licence.Type)} {licence.Name}{from} " +
                        $"expires {when} " +
                        $"({expiry:d MMM yyyy}).{renewal}",
                        $"/License/Details/{licence.Id}",
                        kind);
                }

                if (licence.AssignedToEmployee is { IsActive: true } holder &&
                    !administrators.Contains(holder.Id))
                {
                    _notifications.Notify(
                        holder.Id,
                        $"{licence.Name} expires {when}",
                        $"The licence {licence.Name}, which is assigned to " +
                        $"you, expires {when} ({expiry:d MMM yyyy}). The " +
                        "administrators have been reminded to renew it.",
                        null,
                        kind);
                }

                Record(ExpiryReminderKind.Licence, licence.Id, expiry, stage, day);

                sent++;
            }

            return sent;
        }

        /// <summary>
        /// A certification is the holder's to renew, so they are told,
        /// and HR, who keep the record of it, are told too.
        /// </summary>
        private async Task<int> CertificationsAsync(DateTime day)
        {
            var certifications = await _context.Certifications
                .AsNoTracking()
                .Include(c => c.Employee)
                .Where(c =>
                    c.Employee.IsActive &&
                    c.ExpiryDate != null &&
                    c.ExpiryDate >= day &&
                    c.ExpiryDate < day.AddDays(FirstReminderDays + 1))
                .ToListAsync();

            if (certifications.Count == 0)
                return 0;

            var hr = await _people.EmployeeIdsAsync(ApprovalRole.HROfficer);

            var sent = 0;

            foreach (var certification in certifications)
            {
                var expiry = certification.ExpiryDate!.Value.Date;

                if (await DueStageAsync(
                        ExpiryReminderKind.Certification,
                        certification.Id,
                        expiry,
                        day) is not int stage)
                {
                    continue;
                }

                var when = When(expiry, day);
                var kind = KindFor(stage);
                var holder = certification.Employee;
                var name = $"{holder.FirstName} {holder.LastName}";

                var issuer = string.IsNullOrWhiteSpace(certification.IssuedBy)
                    ? string.Empty
                    : $" from {certification.IssuedBy}";

                _notifications.Notify(
                    holder.Id,
                    $"Your {certification.Name} certification expires {when}",
                    $"Your {certification.Name} certification{issuer} " +
                    $"expires {when} ({expiry:d MMM yyyy}). Please arrange " +
                    "to renew it and send HR a copy of the new certificate.",
                    null,
                    kind);

                foreach (var officer in hr.Where(id => id != holder.Id))
                {
                    _notifications.Notify(
                        officer,
                        $"{certification.Name} certification for {name} " +
                        $"expires {when}",
                        $"The {certification.Name} certification{issuer} " +
                        $"held by {name} expires {when} " +
                        $"({expiry:d MMM yyyy}). They have been reminded " +
                        "to renew it.",
                        $"/Certification?employeeId={holder.Id}",
                        kind);
                }

                Record(
                    ExpiryReminderKind.Certification,
                    certification.Id,
                    expiry,
                    stage,
                    day);

                sent++;
            }

            return sent;
        }

        /// <summary>
        /// Which reminder is due now, or null when it has already
        /// gone. Inside the last week only the last reminder is due,
        /// so a month's notice that was missed is not sent late on
        /// top of it.
        /// </summary>
        private async Task<int?> DueStageAsync(
            ExpiryReminderKind kind,
            int recordId,
            DateTime expiry,
            DateTime day)
        {
            var stage = (expiry - day).Days <= LastReminderDays
                ? LastReminderDays
                : FirstReminderDays;

            var sent = await _context.ExpiryReminders
                .AnyAsync(r =>
                    r.Kind == kind &&
                    r.RecordId == recordId &&
                    r.ExpiryDate == expiry &&
                    r.DaysBefore == stage);

            return sent ? null : stage;
        }

        private static string Described(LicenseType type) =>
            type == LicenseType.Other
                ? "licence"
                : $"{type.ToString().ToLowerInvariant()} licence";

        private static EmailKind KindFor(int stage) =>
            stage == LastReminderDays
                ? EmailKind.ExpiringThisWeek
                : EmailKind.ExpiringSoon;

        private static string When(DateTime expiry, DateTime day) =>
            (expiry - day).Days switch
            {
                0 => "today",
                1 => "tomorrow",
                var days => $"in {days} days"
            };

        private void Record(
            ExpiryReminderKind kind,
            int recordId,
            DateTime expiry,
            int stage,
            DateTime day)
        {
            _context.ExpiryReminders.Add(
                new ExpiryReminder
                {
                    Kind = kind,
                    RecordId = recordId,
                    ExpiryDate = expiry,
                    DaysBefore = stage,
                    SentOn = day
                });
        }
    }
}
