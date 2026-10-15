using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
    /// <summary>
    /// Runs the reminders once a day.
    ///
    /// Everything else the portal sends happens because somebody
    /// clicked something. This is the only part that acts on its own,
    /// which is what makes a licence expiring or a request sitting
    /// untouched visible at all.
    /// </summary>
    public class ReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReminderOptions _options;
        private readonly ILogger<ReminderHostedService> _logger;

        public ReminderHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<ReminderOptions> options,
            ILogger<ReminderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// When the last run finished and what it sent, so the page can
        /// say whether reminders are actually going out.
        /// </summary>
        public static DateTime? LastRunAt { get; private set; }

        public static int LastRunCount { get; private set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation(
                    "Reminders are turned off, so nothing will be sent.");

                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = UntilNextRun(DateTime.Now);

                _logger.LogInformation(
                    "Next reminder run in {Hours:0.#} hour(s).",
                    delay.TotalHours);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await RunOnceAsync(stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var reminders = scope.ServiceProvider
                    .GetRequiredService<ReminderService>();

                var run = await reminders.RunAsync();

                Record(run);
            }
            catch (Exception ex)
            {
                // A failed run must not take the application with it.
                // Tomorrow's run will pick up whatever was missed,
                // since nothing was marked as sent.
                _logger.LogError(
                    ex,
                    "The reminder run failed. It will be tried again " +
                    "at the next scheduled time.");
            }
        }

        public static void Record(ReminderRun run)
        {
            LastRunAt = DateTime.Now;
            LastRunCount = run.Total;
        }

        /// <summary>
        /// How long until the next run at the configured hour. Running
        /// on startup instead would mean a restart could send the same
        /// batch again the moment the repeat interval allowed it.
        /// </summary>
        internal TimeSpan UntilNextRun(DateTime now)
        {
            var hour = Math.Clamp(_options.RunAtHour, 0, 23);

            var next = now.Date.AddHours(hour);

            if (next <= now)
                next = next.AddDays(1);

            return next - now;
        }
    }
}
