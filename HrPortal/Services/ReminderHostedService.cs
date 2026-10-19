using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
    public class ReminderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<ReminderOptions> _options;
        private readonly ILogger<ReminderHostedService> _logger;

        public ReminderHostedService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<ReminderOptions> options,
            ILogger<ReminderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        public static DateTime? LastRunAt { get; private set; }

        public static int LastRunCount { get; private set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.CurrentValue.Enabled)
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

        internal TimeSpan UntilNextRun(DateTime now)
        {
            var hour = Math.Clamp(_options.CurrentValue.RunAtHour, 0, 23);

            var next = now.Date.AddHours(hour);

            if (next <= now)
                next = next.AddDays(1);

            return next - now;
        }
    }
}
