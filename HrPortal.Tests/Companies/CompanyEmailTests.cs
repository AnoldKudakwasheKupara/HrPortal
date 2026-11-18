using HrPortal.Services;
using HrPortal.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HrPortal.Tests.Companies
{
    [Collection("Brand list")]
    public sealed class CompanyEmailTests : IDisposable
    {
        private static readonly string PortalFolder = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal"));

        private readonly List<Brand> _brandsBefore = Brand.All.ToList();
        private readonly string _mainBefore = Brand.Main.Key;
        private readonly string _pickup = Path.Combine(Path.GetTempPath(), "mail-test-" + Guid.NewGuid().ToString("N"));
        private readonly EmailService _email;

        public CompanyEmailTests()
        {
            var settings = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Brand:Mail:mmc:FromAddress"] = "no-reply@mmc.example",
                    ["Brand:Mail:mmc:TenantId"] = "mmc-tenant",
                    ["Brand:Urls:mmc"] = "https://mmc.example"
                })
                .Build();

            var (brands, main) = Brand.LoadAll(settings, PortalFolder);

            Brand.UseAll(brands, main);
            Brand.Enter(Brand.Main);

            _email = new EmailService(
                Options.Create(new EmailOptions
                {
                    Enabled = true,
                    Delivery = EmailDelivery.File,
                    PickupDirectory = _pickup,
                    FromAddress = "no-reply@intellego.example",
                    PortalBaseUrl = "https://intellego.example"
                }),
                new BrandAssets(new Web(Path.Combine(PortalFolder, "wwwroot")), NullLogger<BrandAssets>.Instance),
                new NoHttp(),
                NullLogger<EmailService>.Instance);
        }

        public void Dispose()
        {
            Brand.UseAll(_brandsBefore, _mainBefore);
            Brand.Enter(Brand.Main);

            if (Directory.Exists(_pickup))
                Directory.Delete(_pickup, recursive: true);
        }

        private static OutboundNotification To(string? brandKey) =>
            new(1, "person@example.com", "A Person", "A", "Leave approved", "Your leave was approved.", "/MyLeave", EmailKind.Approved, brandKey);

        private MimeMessage Sent()
        {
            var file = Assert.Single(Directory.GetFiles(_pickup, "*.eml"));
            return MimeMessage.Load(file);
        }

        [Fact]
        public async Task Somebody_at_mmc_hears_from_mmc_whichever_portal_sent_it()
        {
            Assert.True(await _email.SendNotificationAsync(To("mmc")));

            var message = Sent();
            var from = Assert.Single(message.From.Mailboxes);

            Assert.Equal("no-reply@mmc.example", from.Address);
            Assert.Equal("My MMC", from.Name);
            Assert.Contains("https://mmc.example/MyLeave", message.HtmlBody);
            Assert.DoesNotContain("Intellego", message.HtmlBody);
        }

        [Fact]
        public async Task Somebody_at_intellego_hears_from_intellego()
        {
            Assert.True(await _email.SendNotificationAsync(To("intellego")));

            var from = Assert.Single(Sent().From.Mailboxes);

            Assert.Equal("no-reply@intellego.example", from.Address);
            Assert.Equal("My Intellego", from.Name);
        }

        [Fact]
        public async Task An_email_naming_no_company_follows_the_portal_in_use()
        {
            Brand.Enter(Brand.ForKey("mmc")!);

            Assert.True(await _email.SendNotificationAsync(To(null)));

            Assert.Equal("no-reply@mmc.example", Assert.Single(Sent().From.Mailboxes).Address);
            Assert.Equal("mmc", Brand.Current.Key);
        }

        [Fact]
        public void A_company_mailbox_takes_what_it_leaves_out_from_the_email_settings()
        {
            var mmc = Brand.ForKey("mmc")!;

            Assert.Equal("no-reply@mmc.example", mmc.Mailbox!.FromAddress);
            Assert.Equal("mmc-tenant", mmc.Mailbox.TenantId);
            Assert.Null(mmc.Mailbox.ClientId);
            Assert.Null(Brand.ForKey("intellego")!.Mailbox);
        }

        private sealed class NoHttp : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => throw new InvalidOperationException("No network in tests.");
        }

        private sealed class Web : IWebHostEnvironment
        {
            public Web(string root)
            {
                WebRootPath = root;
                ContentRootPath = Path.GetDirectoryName(root)!;
                WebRootFileProvider = new NullFileProvider();
                ContentRootFileProvider = new NullFileProvider();
            }

            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; }
            public string ApplicationName { get; set; } = "HrPortal";
            public IFileProvider ContentRootFileProvider { get; set; }
            public string ContentRootPath { get; set; }
            public string EnvironmentName { get; set; } = "Test";
        }
    }
}
