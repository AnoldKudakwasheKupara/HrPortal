using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
    /// <summary>
    /// What a single reminder run did, so the run can be reported
    /// rather than disappearing into the log.
    /// </summary>
    public class ReminderRun
    {
        public Dictionary<string, int> Sent { get; } = new();

        public int Total => Sent.Values.Sum();

        public void Add(string kind, int count)
        {
            if (count <= 0)
                return;

            Sent[kind] = Sent.TryGetValue(kind, out var existing)
                ? existing + count
                : count;
        }
    }

    /// <summary>
    /// Raises the reminders nobody would otherwise get.
    ///
    /// Every other notification in the portal is a reaction to somebody
    /// doing something. These are the opposite: nothing happens, and
    /// that is exactly the problem. A licence quietly expires, a
    /// contract runs out, a request sits unapproved.
    /// </summary>
    public class ReminderService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ReminderOptions _options;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(
            ApplicationDbContext context,
            NotificationService notifications,
            UserManager<ApplicationUser> userManager,
            IOptions<ReminderOptions> options,
            ILogger<ReminderService> logger)
        {
            _context = context;
            _notifications = notifications;
            _userManager = userManager;
            _options = options.Value;
            _logger = logger;
        }

        public const string LicenceExpiry = "Licence expiry";
        public const string DocumentExpiry = "Document expiry";
        public const string ContractEnding = "Contract ending";

        public async Task<ReminderRun> RunAsync()
        {
            var run = new ReminderRun();

            var today = DateTime.Today;
            var horizon = today.AddDays(_options.ExpiryWindowDays);

            run.Add(
                LicenceExpiry,
                await RemindExpiringLicencesAsync(today, horizon));

            run.Add(
                DocumentExpiry,
                await RemindExpiringDocumentsAsync(today, horizon));

            run.Add(
                ContractEnding,
                await RemindEndingContractsAsync(today, horizon));

            await _context.SaveChangesAsync();

            if (run.Total > 0)
            {
                _logger.LogInformation(
                    "Reminder run sent {Count} reminder(s).",
                    run.Total);
            }

            return run;
        }

        private async Task<int> RemindExpiringLicencesAsync(
            DateTime today,
            DateTime horizon)
        {
            var licences = await _context.Licenses
                .AsNoTracking()
                .Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate <= horizon)
                .OrderBy(l => l.ExpiryDate)
                .ToListAsync();

            if (licences.Count == 0)
                return 0;

            // Licences sit with the administrator, who keeps the
            // register and places the renewal.
            var recipients = await EmployeeIdsInRoleAsync(Roles.Administrator);

            var sent = 0;

            foreach (var licence in licences)
            {
                var due = Due(today, licence.ExpiryDate);

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            LicenceExpiry,
                            licence.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Licence {due}",
                        $"{licence.Name} {due} " +
                        $"({licence.ExpiryDate:d MMM yyyy}).",
                        $"/License/Details/{licence.Id}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindExpiringDocumentsAsync(
            DateTime today,
            DateTime horizon)
        {
            var documents = await _context.EmployeeDocuments
                .AsNoTracking()
                .Include(d => d.Employee)
                .Where(d =>
                    d.ExpiryDate != null &&
                    d.ExpiryDate <= horizon &&
                    d.Employee.IsActive)
                .OrderBy(d => d.ExpiryDate)
                .ToListAsync();

            if (documents.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var document in documents)
            {
                var due = Due(today, document.ExpiryDate!.Value);

                var who =
                    $"{document.Employee.FirstName} " +
                    $"{document.Employee.LastName}";

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            DocumentExpiry,
                            document.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Document {due}",
                        $"{document.OriginalFileName} on {who}'s file " +
                        $"{due} ({document.ExpiryDate:d MMM yyyy}).",
                        $"/EmployeeDocument?employeeId={document.EmployeeId}");

                    sent++;
                }
            }

            return sent;
        }

        private async Task<int> RemindEndingContractsAsync(
            DateTime today,
            DateTime horizon)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.ContractEndDate != null &&
                    e.ContractEndDate <= horizon)
                .OrderBy(e => e.ContractEndDate)
                .ToListAsync();

            if (employees.Count == 0)
                return 0;

            var recipients = await EmployeeIdsInRoleAsync(Roles.HrOfficer);

            var sent = 0;

            foreach (var employee in employees)
            {
                var due = Due(today, employee.ContractEndDate!.Value);

                foreach (var employeeId in recipients)
                {
                    if (!await ShouldSendAsync(
                            ContractEnding,
                            employee.Id.ToString(),
                            employeeId,
                            today))
                    {
                        continue;
                    }

                    _notifications.Notify(
                        employeeId,
                        $"Contract {due}",
                        $"{employee.FirstName} {employee.LastName}'s " +
                        $"{Humanise(employee.EmploymentType)} contract {due} " +
                        $"({employee.ContractEndDate:d MMM yyyy}).",
                        $"/Employee/Details/{employee.Id}");

                    sent++;
                }
            }

            return sent;
        }

        /// <summary>
        /// "expired 3 days ago", "expires today" or "expires in 12
        /// days", so the title says the urgency rather than the reader
        /// having to work it out from a date.
        /// </summary>
        private static string Due(DateTime today, DateTime date)
        {
            var days = (date.Date - today).Days;

            return days switch
            {
                < 0 => $"expired {-days} day{Plural(-days)} ago",
                0 => "expires today",
                _ => $"expires in {days} day{Plural(days)}"
            };
        }

        private static string Plural(int count) => count == 1 ? "" : "s";

        /// <summary>
        /// Turns an enum name into something that reads as English in
        /// the middle of a sentence, so FixedTerm becomes "fixed term".
        /// </summary>
        private static string Humanise(Enum value)
        {
            var name = value.ToString();

            var spaced = string.Concat(
                name.Select((c, i) =>
                    i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));

            return spaced.ToLowerInvariant();
        }

        /// <summary>
        /// True when this reminder has not gone to this person about
        /// this thing inside the repeat interval. Records the send so
        /// the next run knows.
        /// </summary>
        private async Task<bool> ShouldSendAsync(
            string kind,
            string referenceKey,
            int employeeId,
            DateTime today)
        {
            var existing = await _context.ReminderLogs
                .FirstOrDefaultAsync(r =>
                    r.Kind == kind &&
                    r.ReferenceKey == referenceKey &&
                    r.EmployeeId == employeeId);

            if (existing == null)
            {
                _context.ReminderLogs.Add(new ReminderLog
                {
                    Kind = kind,
                    ReferenceKey = referenceKey,
                    EmployeeId = employeeId,
                    LastSentOn = today
                });

                return true;
            }

            if (existing.LastSentOn.Date.AddDays(
                    _options.RepeatEveryDays) > today)
            {
                return false;
            }

            existing.LastSentOn = today;

            return true;
        }

        /// <summary>
        /// The employees behind the accounts in a role. A role holder
        /// with no employee record cannot be notified, since
        /// notifications belong to an employee.
        /// </summary>
        private async Task<List<int>> EmployeeIdsInRoleAsync(string role)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);

            if (users.Count == 0)
                return new List<int>();

            var userIds = users.Select(u => u.Id).ToList();

            return await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.IsActive &&
                    e.UserId != null &&
                    userIds.Contains(e.UserId.Value))
                .Select(e => e.Id)
                .ToListAsync();
        }
    }
}
