using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using HrPortal.Services.Notifications;
using MimeKit;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HrPortal.Services
{
    public class EmailService
    {
        private readonly EmailOptions _options;
        private readonly BrandAssets _brand;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<EmailService> _logger;

        private readonly SemaphoreSlim _tokenGate = new(1, 1);

        private readonly Dictionary<string, (string Token, DateTimeOffset Expires)> _graphTokens = new();

        /// <summary>
        /// Who an email goes out as, and the Microsoft 365 app that may
        /// send it: a company's own mailbox when it has one, otherwise
        /// the portal's.
        /// </summary>
        private sealed record Sender(string Address, string? TenantId, string? ClientId, string? ClientSecret);

        public EmailService(
            IOptions<EmailOptions> options,
            BrandAssets brand,
            IHttpClientFactory http,
            ILogger<EmailService> logger)
        {
            _options = options.Value;
            _brand = brand;
            _http = http;
            _logger = logger;
        }

        public bool IsEnabled => _options.Enabled;

        private Sender SenderFor(Brand brand)
        {
            var box = brand.Mailbox;

            static string? Pick(string? own, string? shared) =>
                string.IsNullOrWhiteSpace(own) ? shared : own;

            return new Sender(
                Pick(box?.FromAddress, _options.FromAddress) ?? string.Empty,
                Pick(box?.TenantId, _options.TenantId),
                Pick(box?.ClientId, _options.ClientId),
                Pick(box?.ClientSecret, _options.ClientSecret));
        }

        /// <summary>
        /// Sends one email, branded for the company it names, or for
        /// the portal being used when it names none, so somebody who
        /// works for MMC Capital hears from MMC Capital even when the
        /// email starts on Intellego's portal.
        /// </summary>
        public async Task<bool> SendNotificationAsync(
            OutboundNotification notification,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
                return false;

            var brand = Brand.ForKey(notification.BrandKey) ?? Brand.Current;
            var before = Brand.Current;

            Brand.Enter(brand);

            try
            {
                return await SendAsBrandAsync(notification, SenderFor(brand), cancellationToken);
            }
            finally
            {
                Brand.Enter(before);
            }
        }

        private async Task<bool> SendAsBrandAsync(
            OutboundNotification notification,
            Sender sender,
            CancellationToken cancellationToken)
        {

            if (string.IsNullOrWhiteSpace(notification.EmailAddress))
            {
                _logger.LogDebug(
                    "Skipping notification email with no recipient: {Subject}",
                    notification.Title);

                return false;
            }

            if (string.IsNullOrWhiteSpace(sender.Address))
            {
                _logger.LogWarning(
                    "Email is enabled but Email:FromAddress is not set, " +
                    "so nothing can be sent.");

                return false;
            }

            if (_options.Delivery == EmailDelivery.Graph &&
                (string.IsNullOrWhiteSpace(sender.TenantId) ||
                 string.IsNullOrWhiteSpace(sender.ClientId) ||
                 string.IsNullOrWhiteSpace(sender.ClientSecret)))
            {
                _logger.LogWarning(
                    "Email is set to go through Microsoft Graph but " +
                    "Email:TenantId, Email:ClientId or Email:ClientSecret " +
                    "is not set, so nothing can be sent.");

                return false;
            }

            try
            {
                var message = Build(notification, sender);

                switch (_options.Delivery)
                {
                    case EmailDelivery.File:
                        await WriteToPickupAsync(message, cancellationToken);
                        break;

                    case EmailDelivery.Graph:
                        await SendOverGraphAsync(message, sender, cancellationToken);
                        break;

                    default:
                        await SendOverSmtpAsync(message, cancellationToken);
                        break;
                }

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

        private MimeMessage Build(OutboundNotification notification, Sender sender)
        {
            var logo = _brand.EmailMark;

            var rendered = NotificationEmail.Render(
                notification,
                _options.LinkBase,
                hasLogo: logo != null);

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(_options.SenderName, sender.Address));

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
                    "logo.png",
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

        /// <summary>
        /// Hands the finished message to Microsoft Graph as it
        /// stands, so the branded body and the embedded logo arrive
        /// exactly as they were built. Graph takes it base64-encoded,
        /// answers 202 when it has accepted it, and files a copy in
        /// the sending mailbox's Sent Items.
        /// </summary>
        private async Task SendOverGraphAsync(
            MimeMessage message,
            Sender sender,
            CancellationToken cancellationToken)
        {
            var token = await GraphTokenAsync(sender, cancellationToken);

            using var mime = new MemoryStream();

            await message.WriteToAsync(mime, cancellationToken);

            var content = new StringContent(
                Convert.ToBase64String(mime.ToArray()));

            content.Headers.ContentType =
                new MediaTypeHeaderValue("text/plain");

            var url =
                $"{_options.GraphBase.TrimEnd('/')}/v1.0/users/" +
                $"{Uri.EscapeDataString(sender.Address)}/sendMail";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.CreateClient()
                .SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                var detail = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                throw new InvalidOperationException(
                    "Microsoft Graph did not accept the message from " +
                    $"{sender.Address} ({(int)response.StatusCode}): " +
                    detail);
            }
        }

        /// <summary>
        /// A token for Graph, signed in as the sending company's app.
        /// Kept until five minutes before it runs out, so a morning of
        /// reminders shares one sign-in per app rather than taking one
        /// each.
        /// </summary>
        private async Task<string> GraphTokenAsync(
            Sender sender,
            CancellationToken cancellationToken)
        {
            var key = $"{sender.TenantId}|{sender.ClientId}";

            await _tokenGate.WaitAsync(cancellationToken);

            try
            {
                if (_graphTokens.TryGetValue(key, out var held) &&
                    DateTimeOffset.UtcNow < held.Expires)
                {
                    return held.Token;
                }

                var url =
                    $"{_options.GraphLoginBase.TrimEnd('/')}/" +
                    $"{Uri.EscapeDataString(sender.TenantId!)}" +
                    "/oauth2/v2.0/token";

                var form = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["client_id"] = sender.ClientId!,
                        ["client_secret"] = sender.ClientSecret!,
                        ["scope"] =
                            $"{_options.GraphBase.TrimEnd('/')}/.default",
                        ["grant_type"] = "client_credentials"
                    });

                using var response = await _http.CreateClient()
                    .PostAsync(url, form, cancellationToken);

                var body = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        "Microsoft Entra ID did not sign the portal in " +
                        $"({(int)response.StatusCode}): {body}");
                }

                using var json = JsonDocument.Parse(body);

                var token = json.RootElement
                    .GetProperty("access_token")
                    .GetString()!;

                var lifetime = json.RootElement
                    .GetProperty("expires_in")
                    .GetInt32();

                _graphTokens[key] = (token, DateTimeOffset.UtcNow
                    .AddSeconds(Math.Max(60, lifetime - 300)));

                return token;
            }
            finally
            {
                _tokenGate.Release();
            }
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
