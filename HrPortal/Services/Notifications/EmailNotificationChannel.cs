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

        public async Task DeliverAsync(
            IReadOnlyList<OutboundNotification> batch,
            CancellationToken cancellationToken = default)
        {
            foreach (var item in batch)
            {
                await _email.SendAsync(
                    item.EmailAddress,
                    item.DisplayName,
                    item.Title,
                    item.Message,
                    item.Link,
                    cancellationToken);
            }
        }
    }
}
