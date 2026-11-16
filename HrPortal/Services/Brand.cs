using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HrPortal.Services
{
    /// <summary>
    /// The company a page is for. One deployment serves every company in
    /// the group, each on its own web address, so the brand is chosen
    /// for each request from the address it came in on and read from
    /// anywhere, including the static code behind printed documents and
    /// emails. Work done outside a request, such as the nightly
    /// reminders, sees the deployment's main brand unless it enters
    /// another.
    ///
    /// The defaults are Intellego. Every Brands/{key}.json is another
    /// company, with its artwork in wwwroot/brands/{key} and any front
    /// page wording in Views/Brands/{key}/_About.cshtml. Brand__Key picks
    /// the main brand; Brand__Hosts__{key} lists the addresses that show
    /// a brand, and Brand__Urls__{key} the address its emails link to.
    /// </summary>
    public sealed class Brand
    {
        public const string DefaultKey = "intellego";

        private static readonly AsyncLocal<Brand?> Request = new();

        private static Brand _main = new();

        private static IReadOnlyDictionary<string, Brand> _byKey =
            new Dictionary<string, Brand> { [DefaultKey] = _main };

        private static IReadOnlyDictionary<string, Brand> _byHost =
            new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The brand of the request being served, or the main brand.</summary>
        public static Brand Current => Request.Value ?? _main;

        /// <summary>The brand the deployment falls back to, for unknown addresses and background work.</summary>
        public static Brand Main => _main;

        public static IReadOnlyCollection<Brand> All => _byKey.Values.ToList();

        /// <summary>Makes one brand the only brand. Used by tests and single-company setups.</summary>
        public static void Use(Brand brand) => UseAll([brand], brand.Key);

        public static void UseAll(IEnumerable<Brand> brands, string mainKey)
        {
            var byKey = brands.ToDictionary(b => b.Key);

            if (!byKey.TryGetValue(mainKey, out var main))
                throw new InvalidOperationException($"There is no brand \"{mainKey}\" to use as the main brand.");

            _byKey = byKey;
            _byHost = HostMap(byKey.Values);
            _main = main;
            Request.Value = null;
        }

        /// <summary>Each listed web address and its brand. An address listed for two brands is refused.</summary>
        public static IReadOnlyDictionary<string, Brand> HostMap(IEnumerable<Brand> brands)
        {
            var byHost = new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase);

            foreach (var brand in brands)
            {
                foreach (var host in brand.Hosts)
                {
                    if (byHost.TryGetValue(host, out var other) && other.Key != brand.Key)
                        throw new InvalidOperationException($"The address {host} is listed for both {other.Key} and {brand.Key}.");

                    byHost[host] = brand;
                }
            }

            return byHost;
        }

        /// <summary>The brand for a web address; the main brand for any address not listed.</summary>
        public static Brand ForHost(string? host) =>
            host != null && _byHost.TryGetValue(host, out var brand) ? brand : _main;

        public static Brand? ForKey(string? key) =>
            key != null && _byKey.TryGetValue(key, out var brand) ? brand : null;

        /// <summary>Makes a brand current for the rest of this request or piece of work.</summary>
        public static void Enter(Brand brand) => Request.Value = brand;

        public string Key { get; set; } = DefaultKey;

        /// <summary>The web addresses, without scheme or port, that show this brand.</summary>
        public string[] Hosts { get; set; } = [];

        /// <summary>
        /// Where links in this company's emails point, such as
        /// https://hr.example.com. Null falls back to Email:PortalBaseUrl
        /// for the main brand, or the address the request came in on.
        /// </summary>
        public string? PortalUrl { get; set; }

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
        /// What the company calls the person who holds the HR Officer
        /// role, such as "HR Manager".
        /// </summary>
        public string HrTitle { get; set; } = "HR Officer";

        /// <summary>
        /// What the company calls the people who hold the MD role and
        /// give the final decision, such as "Managing Director".
        /// </summary>
        public string DirectorTitle { get; set; } = "Managing Director";

        /// <summary>
        /// The final approver in a sentence: "the Managing Director",
        /// or "a director" where any of several may decide.
        /// </summary>
        public string TheDirector { get; set; } = "the Managing Director";

        /// <summary>The same, short enough for a button or a stage: "the MD".</summary>
        public string TheDirectorShort { get; set; } = "the MD";

        /// <summary>
        /// Who gives the final decision on a course application, such
        /// as "Managing Consultant".
        /// </summary>
        public string CourseApproverTitle { get; set; } = "Managing Consultant";

        /// <summary>The same in a sentence: "the Managing Consultant".</summary>
        public string TheCourseApprover { get; set; } = "the Managing Consultant";

        /// <summary>
        /// Whether HR's approval of leave is the final decision, with no
        /// director after it. It follows the company that employs the
        /// person asking, whichever portal they ask on.
        /// </summary>
        public bool LeaveEndsWithHr { get; set; }

        /// <summary>
        /// Whether the company keeps a register of client complaints in
        /// the portal, as a stockbroker must.
        /// </summary>
        public bool ComplaintsRegister { get; set; }

        /// <summary>A phrase made to start a sentence.</summary>
        public static string Capitalised(string phrase) =>
            phrase.Length == 0 ? phrase : char.ToUpperInvariant(phrase[0]) + phrase[1..];

        /// <summary>The name to show for a role this company may call something else.</summary>
        public string RoleTitle(string role) =>
            role switch
            {
                "HR Officer" => HrTitle,
                "MD" => DirectorTitle,
                _ => role
            };

        public bool IsDefault => Key == DefaultKey;

        /// <summary>The address of a piece of artwork, for a page.</summary>
        public string Asset(string file) => $"~/brands/{Key}/{file}";

        public string AboutPartial => $"~/Views/Brands/{Key}/_About.cshtml";

        /// <summary>
        /// The brand this deployment is set to. Asking for a company with
        /// no brand file stops the portal starting rather than showing
        /// one company's staff another company's name.
        /// </summary>
        public static Brand Load(IConfiguration configuration, string contentRoot) =>
            Load(configuration["Brand:Key"]?.Trim(), configuration, contentRoot);

        /// <summary>Every company with a brand file, plus Intellego, and the key of the main one.</summary>
        public static (IReadOnlyList<Brand> Brands, string MainKey) LoadAll(IConfiguration configuration, string contentRoot)
        {
            var keys = new List<string> { DefaultKey };
            var folder = Path.Combine(contentRoot, "Brands");

            if (Directory.Exists(folder))
            {
                keys.AddRange(Directory.GetFiles(folder, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(k => k != null && k != DefaultKey)
                    .Select(k => k!)
                    .OrderBy(k => k));
            }

            var main = configuration["Brand:Key"]?.Trim();

            if (string.IsNullOrEmpty(main))
                main = DefaultKey;

            if (!keys.Contains(main))
                keys.Add(main);

            return (keys.Select(k => Load(k, configuration, contentRoot)).ToList(), main);
        }

        private static Brand Load(string? key, IConfiguration configuration, string contentRoot)
        {
            Brand brand;

            if (string.IsNullOrEmpty(key) || key == DefaultKey)
            {
                brand = new Brand();
            }
            else
            {
                if (!Regex.IsMatch(key, "^[a-z0-9-]+$"))
                    throw new InvalidOperationException($"Brand:Key \"{key}\" should be lower case letters, numbers and hyphens.");

                var path = Path.Combine(contentRoot, "Brands", key + ".json");

                if (!File.Exists(path))
                    throw new InvalidOperationException($"Brand:Key is \"{key}\" but {path} does not exist.");

                brand = new Brand { Colors = new BrandColors() };

                new ConfigurationBuilder()
                    .AddJsonFile(path)
                    .Build()
                    .Bind(brand);

                brand.Key = key;
            }

            var hosts = configuration[$"Brand:Hosts:{brand.Key}"];

            if (!string.IsNullOrWhiteSpace(hosts))
            {
                brand.Hosts = brand.Hosts
                    .Concat(hosts.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
                    .Select(h => h.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToArray();
            }

            var url = configuration[$"Brand:Urls:{brand.Key}"];

            if (!string.IsNullOrWhiteSpace(url))
                brand.PortalUrl = url.Trim().TrimEnd('/');

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
