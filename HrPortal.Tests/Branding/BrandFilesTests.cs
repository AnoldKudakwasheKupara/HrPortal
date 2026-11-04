using HrPortal.Services;
using Microsoft.Extensions.Configuration;

namespace HrPortal.Tests.Branding
{
    /// <summary>
    /// Every company in Brands/ must be able to go live: it loads, has all
    /// its artwork, and its colours can carry the white text put on them.
    /// </summary>
    public class BrandFilesTests
    {
        private static readonly string Project = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal"));

        public static TheoryData<string> Companies()
        {
            var data = new TheoryData<string> { Brand.DefaultKey };

            foreach (var file in Directory.GetFiles(Path.Combine(Project, "Brands"), "*.json"))
                data.Add(Path.GetFileNameWithoutExtension(file));

            return data;
        }

        private static Brand Load(string key) =>
            Brand.Load(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Brand:Key"] = key })
                    .Build(),
                Project);

        private static double Luminance(string hex)
        {
            var h = hex.TrimStart('#');

            double Part(int i)
            {
                var c = Convert.ToInt32(h.Substring(i, 2), 16) / 255d;

                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Part(0) + 0.7152 * Part(2) + 0.0722 * Part(4);
        }

        private static double ContrastWithWhite(string hex) => 1.05 / (Luminance(hex) + 0.05);

        [Theory]
        [MemberData(nameof(Companies))]
        public void It_has_all_its_artwork(string key)
        {
            foreach (var file in new[] { "logo.png", "logo-on-dark.png", "logo-email.png", "icon-32.png", "icon-180.png", "icon-512.png" })
                Assert.True(File.Exists(Path.Combine(Project, "wwwroot", "brands", key, file)), $"{key} has no {file}");
        }

        [Theory]
        [MemberData(nameof(Companies))]
        public void It_names_the_company_and_has_front_page_wording(string key)
        {
            var brand = Load(key);

            Assert.False(string.IsNullOrWhiteSpace(brand.PortalName));
            Assert.False(string.IsNullOrWhiteSpace(brand.LegalName));
            Assert.True(File.Exists(Path.Combine(Project, "Views", "Brands", key, "_About.cshtml")), $"{key} has no front page wording");

            if (key != Brand.DefaultKey)
                Assert.DoesNotContain("Intellego", brand.PortalName + brand.ShortName + brand.FullName + brand.LegalName + brand.LetterheadName + brand.Tagline);
        }

        [Theory]
        [MemberData(nameof(Companies))]
        public void White_text_is_readable_on_its_colours(string key)
        {
            var colors = Load(key).Colors;

            foreach (var (name, hex) in new[] { ("Primary", colors.Primary), ("Sidebar", colors.Sidebar), ("Accent", colors.Accent) })
                Assert.True(ContrastWithWhite(hex) >= 4.5, $"{key}: white on {name} {hex} is only {ContrastWithWhite(hex):0.0}:1");
        }
    }
}
