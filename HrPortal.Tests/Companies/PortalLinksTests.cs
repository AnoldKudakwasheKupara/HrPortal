using HrPortal.Services;
using Microsoft.Extensions.Configuration;

namespace HrPortal.Tests.Companies
{
    [Collection("Brand list")]
    public sealed class PortalLinksTests : IDisposable
    {
        private static readonly string PortalFolder = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal"));

        private readonly List<Brand> _brandsBefore = Brand.All.ToList();
        private readonly string _mainBefore = Brand.Main.Key;

        public PortalLinksTests()
        {
            var settings = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Brand:Urls:mmc"] = "https://mmc.example/"
                })
                .Build();

            var (brands, main) = Brand.LoadAll(settings, PortalFolder);

            Brand.UseAll(brands, main);
        }

        public void Dispose() => Brand.UseAll(_brandsBefore, _mainBefore);

        [Fact]
        public void A_link_for_the_other_company_goes_to_its_portal()
        {
            Assert.Equal("https://mmc.example/LeaveApproval/Details/4",
                PortalLinks.For("mmc", "/LeaveApproval/Details/4", "https://intellego.example"));
        }

        [Fact]
        public void The_main_company_uses_the_email_portal_address()
        {
            Assert.Equal("https://intellego.example/MyLeave/Details/1",
                PortalLinks.For("intellego", "/MyLeave/Details/1", "https://intellego.example/"));
        }

        [Fact]
        public void Without_a_known_address_the_link_stays_as_it_was()
        {
            Assert.Equal("/MyLeave/Details/1", PortalLinks.For("intellego", "/MyLeave/Details/1", null));
            Assert.Equal("/MyLeave/Details/1", PortalLinks.For(null, "/MyLeave/Details/1", "https://intellego.example"));
            Assert.Equal("/MyLeave/Details/1", PortalLinks.For("nobody", "/MyLeave/Details/1", "https://intellego.example"));
        }

        [Fact]
        public void Links_that_are_not_on_the_portal_are_left_alone()
        {
            Assert.Equal("https://elsewhere.example/x", PortalLinks.For("mmc", "https://elsewhere.example/x", null));
            Assert.Equal("//elsewhere.example/x", PortalLinks.For("mmc", "//elsewhere.example/x", null));
            Assert.Null(PortalLinks.For("mmc", null, null));
        }
    }
}
