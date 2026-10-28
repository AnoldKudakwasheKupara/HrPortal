namespace HrPortal.Services
{
    /// <summary>
    /// Runs the training reminders once a day.
    ///
    /// It also runs shortly after the portal starts. A server that
    /// was off at seven in the morning would otherwise skip that
    /// day altogether, and the log of what has already gone out
    /// keeps the catch-up from repeating anything.
    /// </summary>
    public class TrainingReminderWorker : BackgroundService
    {
        /// <summary>
        /// The hour reminders go out, in the server's own time.
        /// Early enough to be read before the working day.
        /// </summary>
        private const int SendAtHour = 7;

        private static readonly TimeSpan SettleOnStart =
            TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TrainingReminderWorker> _logger;

        public TrainingReminderWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<TrainingReminderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await Delay(SettleOnStart, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);

                await Delay(UntilNextRun(DateTime.Now), stoppingToken);
            }
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var reminders = scope.ServiceProvider
                    .GetRequiredService<TrainingReminderService>();

                await reminders.SendDueAsync(DateTime.Today);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Could not send today's training reminders.");
            }
        }

        /// <summary>
        /// How long until the next send time. Worked out from the
        /// clock each time rather than counted in 24 hour steps, so
        /// the hour does not drift.
        /// </summary>
        private static TimeSpan UntilNextRun(DateTime now)
        {
            var next = now.Date.AddHours(SendAtHour);

            if (next <= now)
                next = next.AddDays(1);

            return next - now;
        }

        private static async Task Delay(
            TimeSpan span,
            CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(span, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
