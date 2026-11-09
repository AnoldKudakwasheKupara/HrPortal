using HrPortal.Services;
using Microsoft.Extensions.Configuration;

namespace HrPortal.Tests.Branding
{
    public sealed class BrandTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "brand-test-" + Guid.NewGuid().ToString("N"));

        public BrandTests() => Directory.CreateDirectory(Path.Combine(_root, "Brands"));

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private Brand Load(string? key) =>
            Brand.Load(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Brand:Key"] = key })
                    .Build(),
                _root);

        private void File(string key, string json) =>
            System.IO.File.WriteAllText(Path.Combine(_root, "Brands", key + ".json"), json);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("intellego")]
        public void With_no_company_set_it_is_intellego_exactly_as_before(string? key)
        {
            var brand = Load(key);

            Assert.True(brand.IsDefault);
            Assert.Equal("My Intellego", brand.PortalName);
            Assert.Equal("Intellego Investment Consultants (Pvt) Ltd", brand.LegalName);
            Assert.Equal("#10675d", brand.Colors.Primary);
            Assert.Equal("#056459", brand.Colors.CssVariables()["--primary-dark"]);
            Assert.Equal("#018080", brand.Colors.Link);
            Assert.Equal("#0b4b3e", brand.Colors.PrintBanner);
            Assert.Equal("~/brands/intellego/logo.png", brand.Asset("logo.png"));
        }

        [Fact]
        public void A_brand_file_sets_the_company_and_works_out_its_shades()
        {
            File("example", """
                {
                  "PortalName": "Example Portal",
                  "ShortName": "Example",
                  "FullName": "Example Holdings",
                  "LegalName": "Example Holdings (Pvt) Ltd",
                  "Colors": { "Primary": "#102a6b", "Sidebar": "#0a1a44", "Accent": "#b8860b", "AccentSoft": "#e8cf8a" }
                }
                """);

            var brand = Load("example");

            Assert.False(brand.IsDefault);
            Assert.Equal("example", brand.Key);
            Assert.Equal("Example Portal", brand.PortalName);
            Assert.Equal("~/brands/example/logo.png", brand.Asset("logo.png"));

            var css = brand.Colors.CssVariables();

            Assert.Equal("#102a6b", css["--primary"]);
            Assert.Equal("16, 42, 107", css["--primary-rgb"]);
            Assert.Equal("#0a1a44", css["--sidebar-bg"]);

            var everything = string.Join(" ", css.Values) + brand.Colors.CssOverride() +
                             brand.Colors.PrintBanner + brand.Colors.PrintGold + brand.Colors.PrintMint;

            foreach (var intellego in new[] { "#10675d", "#056459", "#0c4a40", "#018080", "#7e6e1e", "#0b4b3e", "16, 103, 93" })
                Assert.DoesNotContain(intellego, everything);
        }

        [Fact]
        public void An_exact_shade_in_the_file_wins()
        {
            File("example", """{ "Colors": { "Primary": "#102a6b", "Overrides": { "--primary-dark": "#123456" } } }""");

            Assert.Equal("#123456", Load("example").Colors.CssVariables()["--primary-dark"]);
        }

        [Fact]
        public void A_company_with_no_brand_file_stops_the_portal_rather_than_showing_the_wrong_name()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Load("nobody"));

            Assert.Contains("does not exist", ex.Message);
        }

        [Theory]
        [InlineData("../secrets")]
        [InlineData("Example")]
        public void A_badly_formed_company_key_is_refused(string key) =>
            Assert.Throws<InvalidOperationException>(() => Load(key));

        [Theory]
        [InlineData("#000000", "#ffffff", 0.5, "#808080")]
        [InlineData("#102030", "#ffffff", 0, "#102030")]
        [InlineData("#102030", "#000000", 1, "#000000")]
        public void Colours_mix_as_expected(string a, string b, double amount, string expected) =>
            Assert.Equal(expected, BrandColors.Mix(a, b, amount));

        [Fact]
        public void Every_colour_the_style_sheets_use_can_be_set()
        {
            var sheet = System.IO.File.ReadAllText(Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal", "wwwroot", "css", "app-core.css")));

            var variables = new Brand().Colors.CssVariables();

            foreach (var name in variables.Keys)
                Assert.Contains(name + ":", sheet);
        }
    }
}
