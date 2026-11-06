using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HrPortal.Services
{
    /// <summary>
    /// Who this copy of the portal belongs to. Each company in the group
    /// runs its own deployment of the same code, and one deployment is
    /// one company, so this is set once at startup and read from
    /// anywhere, including the static code behind printed documents and
    /// emails.
    ///
    /// The defaults are Intellego. Setting Brand__Key to another company
    /// reads Brands/{key}.json, with its artwork in wwwroot/brands/{key}
    /// and any front page wording in Views/Brands/{key}/_About.cshtml.
    /// </summary>
    public sealed class Brand
    {
        public const string DefaultKey = "intellego";

        public static Brand Current { get; private set; } = new();

        /// <summary>Called once, as the portal starts.</summary>
        public static void Use(Brand brand) => Current = brand;

        public string Key { get; set; } = DefaultKey;

        /// <summary>What the portal is called: the sidebar, page titles, emails.</summary>
        public string PortalName { get; set; } = "My Intellego";

        /// <summary>The company in a sentence: "the Intellego team".</summary>
        public string ShortName { get; set; } = "Intellego";

        /// <summary>The company in full, without the legal ending.</summary>
        public string FullName { get; set; } = "Intellego Investment Consultants";

        public string LegalName { get; set; } = "Intellego Investment Consultants (Pvt) Ltd";

        /// <summary>The name on a printed form when there is no logo to print.</summary>
        public string LetterheadName { get; set; } = "INTELLEGO INVESTMENT\nCONSULTANTS (PVT) LTD";

        public string Tagline { get; set; } = "Investing in people | Building tomorrow";

        public BrandColors Colors { get; set; } = BrandColors.Intellego();

        /// <summary>
        /// Whether this company has quarterly pension fund reporting. Only
        /// the investment consultants do: another company's brand file has
        /// to ask for it.
        /// </summary>
        public bool FundReporting { get; set; } = true;

        public bool IsDefault => Key == DefaultKey;

        /// <summary>The address of a piece of artwork, for a page.</summary>
        public string Asset(string file) => $"~/brands/{Key}/{file}";

        public string AboutPartial => $"~/Views/Brands/{Key}/_About.cshtml";

        /// <summary>
        /// The brand this deployment is set to. Asking for a company with
        /// no brand file stops the portal starting rather than showing
        /// one company's staff another company's name.
        /// </summary>
        public static Brand Load(IConfiguration configuration, string contentRoot)
        {
            var key = configuration["Brand:Key"]?.Trim();

            if (string.IsNullOrEmpty(key) || key == DefaultKey)
                return new Brand();

            if (!Regex.IsMatch(key, "^[a-z0-9-]+$"))
                throw new InvalidOperationException($"Brand:Key \"{key}\" should be lower case letters, numbers and hyphens.");

            var path = Path.Combine(contentRoot, "Brands", key + ".json");

            if (!File.Exists(path))
                throw new InvalidOperationException($"Brand:Key is \"{key}\" but {path} does not exist.");

            var brand = new Brand { Colors = new BrandColors(), FundReporting = false };

            new ConfigurationBuilder()
                .AddJsonFile(path)
                .Build()
                .Bind(brand);

            brand.Key = key;

            return brand;
        }
    }

    /// <summary>
    /// A company's colours. Four are enough: the rest are worked out from
    /// them, so a brand file stays short. Exact tweaks go in Overrides,
    /// which is how Intellego keeps the shades it has always had.
    ///
    /// Sidebar should be dark, since white text sits on it. Primary and
    /// Accent carry white text too.
    /// </summary>
    public sealed class BrandColors
    {
        public string Primary { get; set; } = "#10675d";

        /// <summary>The sidebar, banners and printed headers.</summary>
        public string Sidebar { get; set; } = "#0c4a40";

        /// <summary>A strong second colour: small highlights and some buttons.</summary>
        public string Accent { get; set; } = "#7e6e1e";

        /// <summary>A light second colour, shown on the dark banners.</summary>
        public string AccentSoft { get; set; } = "#c2b480";

        /// <summary>Exact values by name, for a shade the working-out gets wrong.</summary>
        public Dictionary<string, string> Overrides { get; set; } = new();

        private string Pick(string name, Func<string> derive) =>
            Overrides.TryGetValue(name, out var exact) ? exact : derive();

        /// <summary>The on-screen colours, by the name the style sheets know them by.</summary>
        public Dictionary<string, string> CssVariables() => new()
        {
            ["--primary"] = Primary,
            ["--primary-rgb"] = Rgb(Primary),
            ["--link-rgb"] = Rgb(Link),
            ["--link-hover-rgb"] = Rgb(Pick("--primary-dark", () => Mix(Primary, "#000000", 0.15))),
            ["--primary-dark"] = Pick("--primary-dark", () => Mix(Primary, "#000000", 0.15)),
            ["--primary-deep"] = Pick("--primary-deep", () => Mix(Primary, "#000000", 0.4)),
            ["--primary-light"] = Pick("--primary-light", () => Mix(Primary, "#ffffff", 0.88)),
            ["--primary-surface"] = Pick("--primary-surface", () => Mix(Primary, "#ffffff", 0.88)),
            ["--primary-soft"] = Pick("--primary-soft", () => Mix(Primary, "#ffffff", 0.9)),
            ["--primary-softer"] = Pick("--primary-softer", () => Mix(Primary, "#ffffff", 0.94)),
            ["--primary-edge"] = Pick("--primary-edge", () => Mix(Primary, "#ffffff", 0.75)),
            ["--primary-focus"] = Pick("--primary-focus", () => Mix(Primary, "#ffffff", 0.3)),
            ["--primary-bright"] = Pick("--primary-bright", () => Mix(Primary, "#ffffff", 0.15)),
            ["--primary-glow"] = $"rgba({Rgb(Primary)},0.25)",
            ["--brand-teal"] = Link,
            ["--brand-beige"] = AccentSoft,
            ["--brand-tan"] = Pick("--brand-tan", () => Mix(AccentSoft, "#000000", 0.13)),
            ["--accent-gold"] = Accent,
            ["--accent-gold-dark"] = Pick("--accent-gold-dark", () => Mix(Accent, "#000000", 0.2)),
            ["--accent-tint"] = Pick("--accent-tint", () => Mix(Accent, "#ffffff", 0.95)),
            ["--accent-tint-mid"] = Pick("--accent-tint-mid", () => Mix(Accent, "#ffffff", 0.85)),
            ["--accent-tint-strong"] = Pick("--accent-tint-strong", () => Mix(Accent, "#ffffff", 0.7)),
            ["--sidebar-bg"] = Sidebar,
            ["--sidebar-rgb"] = Rgb(Sidebar),
            ["--sidebar-active"] = Pick("--sidebar-active", () => Mix(Sidebar, "#ffffff", 0.14)),
            ["--sidebar-ink"] = Pick("--sidebar-ink", () => Mix(Sidebar, "#ffffff", 0.8)),
            ["--sidebar-ink-dim"] = Pick("--sidebar-ink-dim", () => Mix(Sidebar, "#ffffff", 0.58)),
            ["--hero-from"] = Pick("--hero-from", () => Mix(Sidebar, Primary, 0.2)),
            ["--hero-to"] = Pick("--hero-to", () => Mix(Sidebar, "#000000", 0.08)),
            ["--hero-deep"] = Pick("--hero-deep", () => Mix(Sidebar, "#000000", 0.35))
        };

        /// <summary>Links, and the one accent on printed forms.</summary>
        public string Link => Pick("--brand-teal", () => Primary);

        public string PrintGold => Pick("print-gold", () => Accent);

        public string PrintMint => Pick("print-mint", () => Mix(Primary, "#ffffff", 0.85));

        public string PrintMintPale => Pick("print-mint-pale", () => Mix(Primary, "#ffffff", 0.94));

        public string PrintBanner => Pick("print-banner", () => Sidebar);

        public string PrintBannerDepth => Pick("print-banner-depth", () => Mix(Sidebar, Primary, 0.35));

        public string PrintBannerSweep => Pick("print-banner-sweep", () => Mix(Sidebar, Primary, 0.65));

        public string PrintBannerLine => Pick("print-banner-line", () => AccentSoft);

        public string PrintOnBanner => Pick("print-on-banner", () => Mix(Sidebar, "#ffffff", 0.85));

        public string PrintOnBannerAccent => Pick("print-on-banner-accent", () => AccentSoft);

        public string EmailAccentTint => Pick("email-accent-tint", () => Mix(Accent, "#ffffff", 0.88));

        public string EmailSecondary => Pick("email-secondary", () => Mix(Primary, "#000000", 0.05));

        public string EmailSecondaryTint => Pick("email-secondary-tint", () => Mix(Primary, "#ffffff", 0.88));

        /// <summary>The style block that puts these colours over the defaults in the style sheets.</summary>
        public string CssOverride()
        {
            var css = new StringBuilder(":root{");

            foreach (var (name, value) in CssVariables())
                css.Append(name).Append(':').Append(value).Append(';');

            return css.Append('}').ToString();
        }

        /// <summary>The colours Intellego has always used, exactly.</summary>
        public static BrandColors Intellego() => new()
        {
            Overrides = new()
            {
                ["--primary-dark"] = "#056459",
                ["--primary-deep"] = "#043f38",
                ["--primary-light"] = "#e2efed",
                ["--primary-surface"] = "#e2efed",
                ["--primary-soft"] = "#e8f3f1",
                ["--primary-softer"] = "#f1f8f7",
                ["--primary-edge"] = "#c3ded9",
                ["--primary-focus"] = "#4e9c92",
                ["--primary-bright"] = "#2a8f82",
                ["--brand-teal"] = "#018080",
                ["--brand-tan"] = "#a8977b",
                ["--accent-gold-dark"] = "#635616",
                ["--accent-tint"] = "#fbf9f0",
                ["--accent-tint-mid"] = "#f4efdb",
                ["--accent-tint-strong"] = "#ece4c2",
                ["--sidebar-active"] = "#176a5b",
                ["--sidebar-ink"] = "#cfdedb",
                ["--sidebar-ink-dim"] = "#9db5af",
                ["--hero-from"] = "#0c5047",
                ["--hero-to"] = "#0a443d",
                ["--hero-deep"] = "#063029",
                ["print-gold"] = "#8a7a28",
                ["print-mint"] = "#dceee7",
                ["print-mint-pale"] = "#f0f8f4",
                ["print-banner"] = "#0b4b3e",
                ["print-banner-depth"] = "#0f6151",
                ["print-banner-sweep"] = "#14715e",
                ["print-banner-line"] = "#c9a94a",
                ["print-on-banner"] = "#d8ece5",
                ["print-on-banner-accent"] = "#c9b878",
                ["email-accent-tint"] = "#f6f1dc",
                ["email-secondary"] = "#016a6a",
                ["email-secondary-tint"] = "#e2efed"
            }
        };

        private static (int R, int G, int B) Parts(string hex)
        {
            var h = hex.TrimStart('#');

            int At(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            return (At(0), At(2), At(4));
        }

        /// <summary>"16, 103, 93", for the style sheets' rgba() colours.</summary>
        public static string Rgb(string hex)
        {
            var (r, g, b) = Parts(hex);

            return $"{r}, {g}, {b}";
        }

        /// <summary>A colour moved towards another: 0 is the first, 1 the second.</summary>
        public static string Mix(string from, string to, double amount)
        {
            var a = Parts(from);
            var b = Parts(to);

            int Blend(int x, int y) => (int)Math.Round(x + (y - x) * amount);

            return $"#{Blend(a.R, b.R):x2}{Blend(a.G, b.G):x2}{Blend(a.B, b.B):x2}";
        }
    }
}
