using HrPortal.Services;

namespace HrPortal.Tests.Branding
{
    public class DarkThemeTests
    {
        private static string Sheet(string name) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "HrPortal", "wwwroot", "css", name)));

        [Theory]
        [InlineData("--link-rgb-dark")]
        [InlineData("--link-hover-rgb-dark")]
        public void Intellegos_dark_links_in_the_style_sheet_match_its_colours(string name)
        {
            var value = BrandColors.Intellego().CssVariables()[name];

            Assert.Contains($"{name}: {value};", Sheet("app-core.css"));
        }

        [Fact]
        public void Another_company_gets_light_links_worked_out_from_its_own_colour()
        {
            var navy = new BrandColors
            {
                Primary = "#102147",
                Overrides = { ["--primary-bright"] = "#2a4a8f" }
            };

            var parts = navy.CssVariables()["--link-rgb-dark"].Split(", ").Select(int.Parse).ToArray();

            Assert.Equal(new[] { 159, 174, 205 }, parts);
        }

        [Fact]
        public void The_dark_theme_only_applies_when_chosen()
        {
            var sheet = Sheet("theme-dark.css");

            var rules = sheet.Split('}')
                .Select(r => r.Trim())
                .Where(r => r.Length > 0 && !r.StartsWith(".theme-toggle"));

            Assert.All(rules, r => Assert.StartsWith("html[data-theme=\"dark\"]", r));
        }
    }
}
