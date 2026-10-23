using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Services.Reminders;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
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

    public class ReminderService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ReminderOptions _options;
        private readonly IEnumerable<IReminderRule> _rules;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(
            ApplicationDbContext context,
            NotificationService notifications,
            UserManager<ApplicationUser> userManager,
            IOptionsSnapshot<ReminderOptions> options,
            IEnumerable<IReminderRule> rules,
            ILogger<ReminderService> logger)
        {
            _context = context;
            _notifications = notifications;
            _userManager = userManager;
            _options = options.Value;
            _rules = rules;
            _logger = logger;
        }

        public const string LicenceExpiry = ReminderKinds.LicenceExpiry;
        public const string DocumentExpiry = ReminderKinds.DocumentExpiry;
        public const string ContractEnding = ReminderKinds.ContractEnding;
        public const string LeaveWaiting = ReminderKinds.LeaveWaiting;
        public const string MaintenanceDue = ReminderKinds.MaintenanceDue;
        public const string TrainingDue = ReminderKinds.TrainingDue;
        public const string CertificationExpiry = ReminderKinds.CertificationExpiry;
        public const string PolicyReview = ReminderKinds.PolicyReview;
        public const string PolicyUnread = ReminderKinds.PolicyUnread;

        public async Task<ReminderRun> RunAsync()
        {
            var run = new ReminderRun();

            var today = DateTime.Today;

            var context = new ReminderRunContext(
                _context,
                _notifications,
                _userManager,
                _options,
                today,
                today.AddDays(_options.ExpiryWindowDays));

            await context.LoadSentLogAsync();

            foreach (var rule in _rules)
            {
                try
                {
                    run.Add(rule.Kind, await rule.RunAsync(context));
                }
                catch (Exception ex)
                {
                    // One rule failing should not cost the others their
                    // reminders, but it must not pass unnoticed either.
                    _logger.LogError(
                        ex,
                        "The {Kind} reminder rule failed; the rest of the " +
                        "run continued.",
                        rule.Kind);
                }
            }

            await _context.SaveChangesAsync();

            if (run.Total > 0)
            {
                _logger.LogInformation(
                    "Reminder run sent {Count} reminder(s).",
                    run.Total);
            }

            return run;
        }
    }
}
