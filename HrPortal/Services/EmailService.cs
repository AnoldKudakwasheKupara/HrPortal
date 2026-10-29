using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using HrPortal.Services.Notifications;
using MimeKit;

namespace HrPortal.Services
{
    public class EmailService
    {
        private readonly EmailOptions _options;
        private readonly BrandAssets _brand;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailOptions> options,
            BrandAssets brand,
            ILogger<EmailService> logger)
        {
            _options = options.Value;
            _brand = brand;
            _logger = logger;
        }

        public bool IsEnabled => _options.Enabled;

        public async Task<bool> SendNotificationAsync(
            OutboundNotification notification,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
                return false;

            if (string.IsNullOrWhiteSpace(notification.EmailAddress))
            {
                _logger.LogDebug(
                    "Skipping notification email with no recipient: {Subject}",
                    notification.Title);

                return false;
            }

            if (string.IsNullOrWhiteSpace(_options.FromAddress))
            {
                _logger.LogWarning(
                    "Email is enabled but Email:FromAddress is not set, " +
                    "so nothing can be sent.");

                return false;
            }

            try
            {
                var message = Build(notification);

                if (_options.Delivery == EmailDelivery.File)
                {
                    await WriteToPickupAsync(message, cancellationToken);
                    return true;
                }

                await SendOverSmtpAsync(message, cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Could not send the notification email to {Recipient}: " +
                    "{Subject}",
                    notification.EmailAddress,
                    notification.Title);

                return false;
            }
        }

        private MimeMessage Build(OutboundNotification notification)
        {
            var logo = _brand.EmailMark;

            var rendered = NotificationEmail.Render(
                notification,
                _options.PortalBaseUrl,
                hasLogo: logo != null);

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(_options.FromName, _options.FromAddress));

            message.To.Add(new MailboxAddress(
                notification.DisplayName ?? string.Empty,
                notification.EmailAddress));

            message.Subject = rendered.Subject;

            var body = new BodyBuilder
            {
                HtmlBody = rendered.Html,
                TextBody = rendered.Text
            };

            if (logo != null)
            {
                var image = body.LinkedResources.Add(
                    "intellego-logo.png",
                    logo,
                    new ContentType("image", "png"));

                image.ContentId = NotificationEmail.LogoContentId;
            }

            message.Body = body.ToMessageBody();

            return message;
        }

        private async Task SendOverSmtpAsync(
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.Auto,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                await client.AuthenticateAsync(
                    _options.UserName,
                    _options.Password ?? string.Empty,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);
        }

        private async Task WriteToPickupAsync(
            MimeMessage message,
            CancellationToken cancellationToken)
        {
            var directory = Path.IsPathRooted(_options.PickupDirectory)
                ? _options.PickupDirectory
                : Path.Combine(
                    AppContext.BaseDirectory,
                    _options.PickupDirectory);

            Directory.CreateDirectory(directory);

            var name =
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-" +
                $"{Guid.NewGuid():N}.eml";

            await using var stream = File.Create(Path.Combine(directory, name));

            await message.WriteToAsync(stream, cancellationToken);

            _logger.LogInformation(
                "Notification email written to {Path}",
                Path.Combine(directory, name));
        }
    }
}
