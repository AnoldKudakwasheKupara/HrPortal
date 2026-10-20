using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace HrPortal.Services
{
    public class EmailService
    {
        private readonly EmailOptions _options;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<EmailOptions> options,
            ILogger<EmailService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public bool IsEnabled => _options.Enabled;

        public async Task<bool> SendAsync(
            string toAddress,
            string? toName,
            string subject,
            string body,
            string? link = null,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
                return false;

            if (string.IsNullOrWhiteSpace(toAddress))
            {
                _logger.LogDebug(
                    "Skipping notification email with no recipient: {Subject}",
                    subject);

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
                var message = Build(toAddress, toName, subject, body, link);

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
                    toAddress,
                    subject);

                return false;
            }
        }

        private MimeMessage Build(
            string toAddress,
            string? toName,
            string subject,
            string body,
            string? link)
        {
            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(_options.FromName, _options.FromAddress));

            message.To.Add(new MailboxAddress(toName ?? string.Empty, toAddress));

            message.Subject = subject;

            var text = body.TrimEnd();

            var url = AbsoluteLink(link);

            if (url != null)
                text += $"\n\nOpen it here: {url}";

            text +=
                $"\n\n--\n{_options.FromName}\n" +
                "This is an automatic message. Replies are not read.";

            message.Body = new TextPart(TextFormat.Plain) { Text = text };

            return message;
        }

        private string? AbsoluteLink(string? link)
        {
            if (string.IsNullOrWhiteSpace(link))
                return null;

            if (string.IsNullOrWhiteSpace(_options.PortalBaseUrl))
                return null;

            return _options.PortalBaseUrl.TrimEnd('/') +
                   "/" + link.TrimStart('/');
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
