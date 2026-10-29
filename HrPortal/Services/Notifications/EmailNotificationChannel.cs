namespace HrPortal.Services.Notifications
{
    public class EmailNotificationChannel : INotificationChannel
    {
        private readonly EmailService _email;

        public EmailNotificationChannel(EmailService email)
        {
            _email = email;
        }

        public string Name => "Email";

        public bool IsEnabled => _email.IsEnabled;

        public async Task<DeliveryReport> DeliverAsync(
            IReadOnlyList<OutboundNotification> batch,
            CancellationToken cancellationToken = default)
        {
            var delivered = 0;
            var failed = 0;

            foreach (var item in batch)
            {
                var sent = await _email.SendNotificationAsync(
                    item,
                    cancellationToken);

                if (sent)
                    delivered++;
                else
                    failed++;
            }

            return new DeliveryReport(delivered, failed);
        }
    }
}
