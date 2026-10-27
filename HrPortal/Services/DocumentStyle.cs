using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// The chrome every printed document shares: the banner, the
    /// numbered section headings, the field boxes, the approval
    /// blocks and the footer.
    ///
    /// The forms are laid out the way the portal shows them, so a
    /// printed page and the screen it came from read as one document.
    /// Fields keep their boxes even when empty, because these are
    /// signed by hand and a box tells the signer where to write.
    /// </summary>
    public static class DocumentStyle
    {
        // ---- brand ----------------------------------------------

        /// <summary>The leading accent from intellego-ic.com.</summary>
        public const string Teal = "#018080";

        /// <summary>Section titles and the company name.</summary>
        public const string Green = "#10675d";

        /// <summary>The big section numerals and the footer rule.</summary>
        public const string Gold = "#8a7a28";

        /// <summary>Body text. Warmer and softer than pure black.</summary>
        public const string Ink = "#3f4245";

        /// <summary>A name or heading, a shade firmer than body.</summary>
        public const string Strong = "#1f2933";

        /// <summary>Labels, sub-headings and anything secondary.</summary>
        public const string Muted = "#6b6f74";

        // ---- form furniture -------------------------------------

        /// <summary>The outline around an input box.</summary>
        public const string FieldLine = "#dfe3e6";

        /// <summary>Hairlines between sections and table rows.</summary>
        public const string Rule = "#e7e3db";

        /// <summary>Text standing in for an entry not yet made.</summary>
        public const string Placeholder = "#9aa0a6";

        /// <summary>The fill behind a label cell or a dead field.</summary>
        public const string Wash = "#f1f3f4";

        /// <summary>Tints behind the employee card and the pledge.</summary>
        public const string Mint = "#dceee7";

        public const string MintPale = "#f0f8f4";

        /// <summary>Required marks, and the declined box.</summary>
        public const string Alert = "#d93025";

        public const string AlertPale = "#fdeded";

        /// <summary>The favourable outcome box.</summary>
        public const string Good = "#157a55";

        /// <summary>Shown where a value was never captured.</summary>
        public const string NotRecorded = "—";

        private const float Gutter = 28f;

        // ---- page -----------------------------------------------

        /// <summary>
        /// No page margin: the banner runs to the paper's edge, so
        /// every other band puts its own gutter back. Takes the body
        /// size because the report sets a larger one than the forms,
        /// and it has to come through here -- a second
        /// DefaultTextStyle call replaces this one outright rather
        /// than adding to it, and silently drops the typeface.
        /// </summary>
        public static void Apply(PageDescriptor page, float fontSize = 9)
        {
            page.Size(PageSizes.A4);
            page.Margin(0);

            page.DefaultTextStyle(t => t
                .FontSize(fontSize)
                .FontColor(Ink)
                .FontFamily(DocumentFonts.Family, "Lato"));
        }

        /// <summary>
        /// Puts the page gutter back inside a full-bleed page.
        /// </summary>
        public static IContainer Gutters(IContainer container) =>
            container.PaddingHorizontal(Gutter);

        // ---- banner ---------------------------------------------

        /// <summary>
        /// The green band across the head of every document: the
        /// company mark reversed out of it on the left, what this
        /// document is on the right, and a gold sweep behind both.
        /// The artwork is drawn rather than placed, so it scales to
        /// the paper instead of pixelating.
        /// </summary>
        public static void Banner(
            IContainer container,
            BrandAssets brand,
            string title,
            string? reference)
        {
            // The flat colour is painted rather than drawn: QuestPDF's
            // SVG reader does not resolve a url(#...) gradient fill and
            // silently renders nothing, which leaves white text on
            // white paper. Only the shapes come from the vector.
            container.Height(BannerHeight).Background(BannerBase)
                .Layers(layers =>
            {
                // Sized explicitly. An Extend()ed layer gives the
                // vector nothing to measure against and it draws
                // nothing at all, silently.
                layers.Layer()
                    .Width(PageSizes.A4.Width)
                    .Height(BannerHeight)
                    .Svg(BannerArt);

                layers.PrimaryLayer()
                    .PaddingHorizontal(Gutter)
                    .PaddingVertical(8)
                    .Row(row =>
                    {
                        row.RelativeItem().AlignLeft().AlignMiddle()
                            .Element(e => Mark(e, brand));

                        row.RelativeItem().AlignRight().AlignMiddle()
                            .Column(right =>
                            {
                                right.Item().AlignRight()
                                    .Text(title.ToUpperInvariant())
                                    .FontSize(16)
                                    .Bold()
                                    .FontColor(Colors.White)
                                    .LetterSpacing(0.02f);

                                if (!string.IsNullOrWhiteSpace(reference))
                                {
                                    right.Item().AlignRight().PaddingTop(1)
                                        .Text(reference)
                                        .FontSize(11)
                                        .FontColor("#d8ece5");
                                }

                                right.Item().AlignRight().PaddingTop(8)
                                    .Text("INVESTING IN PEOPLE  |  " +
                                          "BUILDING TOMORROW")
                                    .FontSize(6.5f)
                                    .SemiBold()
                                    .FontColor("#c9b878")
                                    .LetterSpacing(0.16f);
                            });
                    });
            });
        }

        private static void Mark(IContainer container, BrandAssets brand)
        {
            if (brand.HasMarkOnDark)
            {
                container.Width(120).Image(brand.MarkOnDark!);
                return;
            }

            container.Text("INTELLEGO INVESTMENT\nCONSULTANTS (PVT) LTD")
                .FontSize(11)
                .Bold()
                .FontColor(Colors.White)
                .LineHeight(1.3f);
        }

        private const float BannerHeight = 82f;

        /// <summary>The flat green the banner is painted.</summary>
        private const string BannerBase = "#0b4b3e";

        /// <summary>
        /// The depth and the gold sweep over the flat green. Every
        /// fill is a literal colour; stacking two translucent shapes
        /// gives the band its gradient without asking the SVG reader
        /// for one.
        /// </summary>
        private const string BannerArt =
            "<svg xmlns='http://www.w3.org/2000/svg' " +
            "viewBox='0 0 1024 168' preserveAspectRatio='none'>" +
            "<path d='M300 168 L1024 168 L1024 0 L560 0 Z' " +
            "fill='#0f6151' opacity='0.55'/>" +
            "<path d='M548 168 C676 128 744 52 800 -24 L1024 -24 " +
            "L1024 168 Z' fill='#14715e' opacity='0.75'/>" +
            "<path d='M470 176 C612 120 688 40 736 -28' fill='none' " +
            "stroke='#c9a94a' stroke-width='2.6'/>" +
            "</svg>";

        /// <summary>
        /// The head of every page after the first. The full banner
        /// belongs to page one; repeating it wastes a third of each
        /// continuation sheet and makes a two-page form look like
        /// two separate documents.
        /// </summary>
        public static void RunningHead(
            IContainer container,
            string title,
            string? reference)
        {
            container.PaddingHorizontal(Gutter).PaddingTop(16)
                .Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem()
                            .Text(title.ToUpperInvariant())
                            .FontSize(9)
                            .Bold()
                            .FontColor(Green)
                            .LetterSpacing(0.06f);

                        if (!string.IsNullOrWhiteSpace(reference))
                        {
                            row.AutoItem()
                                .Text(reference)
                                .FontSize(9)
                                .SemiBold()
                                .FontColor(Teal);
                        }
                    });

                    column.Item().PaddingTop(5)
                        .LineHorizontal(1.2f)
                        .LineColor(Teal);
                });
        }

        /// <summary>
        /// The banner on page one, the running head thereafter.
        /// </summary>
        public static void Head(
            IContainer container,
            BrandAssets brand,
            string title,
            string? reference)
        {
            container.Column(column =>
            {
                column.Item().ShowOnce().Element(e =>
                    Banner(e, brand, title, reference));

                column.Item().SkipOnce().Element(e =>
                    RunningHead(e, title, reference));
            });
        }

        // ---- sections -------------------------------------------

        /// <summary>
        /// A numbered section heading: the numeral large and in gold,
        /// then a rule, then what the section is and a line saying
        /// what belongs in it.
        /// </summary>
        public static void Section(
            IContainer container,
            string number,
            string title,
            string? subtitle = null)
        {
            container.Row(row =>
            {
                row.AutoItem().AlignMiddle()
                    .Text(number)
                    .FontSize(21)
                    .Bold()
                    .FontColor(Gold);

                row.AutoItem().PaddingHorizontal(13)
                    .LineVertical(1.2f)
                    .LineColor(Rule);

                row.RelativeItem().AlignMiddle().Column(column =>
                {
                    column.Item()
                        .Text(title.ToUpperInvariant())
                        .FontSize(11)
                        .Bold()
                        .FontColor(Green)
                        .LetterSpacing(0.03f);

                    if (string.IsNullOrWhiteSpace(subtitle))
                        return;

                    column.Item().PaddingTop(1)
                        .Text(subtitle)
                        .FontSize(8)
                        .FontColor(Muted);
                });
            });
        }

        /// <summary>The hairline that closes one section off.</summary>
        public static void Divider(IContainer container)
        {
            container.PaddingVertical(4)
                .LineHorizontal(0.7f)
                .LineColor(Rule);
        }

        // ---- fields ---------------------------------------------

        /// <summary>
        /// A field label. The red mark means the portal would not let
        /// the form be sent without it.
        /// </summary>
        public static void Label(
            IContainer container,
            string text,
            bool required = false)
        {
            container.Text(label =>
            {
                label.Span(text)
                    .FontSize(8.5f)
                    .FontColor(Ink);

                if (required)
                    label.Span(" *").FontSize(8.5f).FontColor(Alert);
            });
        }

        /// <summary>
        /// The boxed entry the portal shows. An empty one keeps its
        /// box and says what belongs there, so somebody completing
        /// the printed copy by hand knows where to write.
        /// </summary>
        public static void Box(
            IContainer container,
            string? value,
            string placeholder = "",
            string? icon = null,
            string? trailingIcon = null,
            bool dead = false,
            float minHeight = 21)
        {
            var filled = !string.IsNullOrWhiteSpace(value);

            container
                .MinHeight(minHeight)
                .Background(dead ? Wash : Colors.White)
                .Border(0.8f)
                .BorderColor(FieldLine)
                .CornerRadius(5)
                .PaddingVertical(4)
                .PaddingHorizontal(9)
                .Row(row =>
                {
                    if (icon != null)
                    {
                        row.AutoItem().AlignMiddle().PaddingRight(7)
                            .Width(11).Height(11).Svg(icon);
                    }

                    row.RelativeItem().AlignMiddle()
                        .Text(filled ? Lines(value!) : placeholder)
                        .FontSize(9.5f)
                        .FontColor(filled ? Ink : Placeholder);

                    if (trailingIcon != null)
                    {
                        row.AutoItem().AlignMiddle().PaddingLeft(7)
                            .Width(11).Height(11).Svg(trailingIcon);
                    }
                });
        }

        /// <summary>A label with its box underneath.</summary>
        public static void Field(
            IContainer container,
            string label,
            string? value,
            bool required = false,
            string? icon = null,
            string? trailingIcon = null,
            bool dead = false,
            float minHeight = 21)
        {
            container.Column(column =>
            {
                column.Item().Element(e => Label(e, label, required));

                column.Item().PaddingTop(4).Element(e => Box(
                    e, value, "", icon, trailingIcon, dead, minHeight));
            });
        }

        /// <summary>
        /// A field nobody has filled in yet, waiting for a pen.
        /// </summary>
        public static void Blank(
            IContainer container,
            string label,
            string placeholder = "Sign here",
            bool required = false,
            string? icon = null)
        {
            container.Column(column =>
            {
                column.Item().Element(e => Label(e, label, required));

                column.Item().PaddingTop(4).Element(e => Box(
                    e, null, placeholder, icon ?? Icons.Pen));
            });
        }

        /// <summary>
        /// The label-and-value table the forms open with: labels on
        /// the wash, values on white, hairlines between.
        /// </summary>
        public static void DetailTable(
            IContainer container,
            IEnumerable<(string Label, string? Value)> rows)
        {
            var list = rows.ToList();

            container
                .Border(0.8f)
                .BorderColor(FieldLine)
                .CornerRadius(5)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(118);
                        columns.RelativeColumn();
                    });

                    for (var i = 0; i < list.Count; i++)
                    {
                        var last = i == list.Count - 1;

                        table.Cell()
                            .Background(Wash)
                            .BorderBottom(last ? 0 : 0.7f)
                            .BorderRight(0.7f)
                            .BorderColor(FieldLine)
                            .PaddingVertical(3)
                            .PaddingHorizontal(9)
                            .Text(list[i].Label)
                            .FontSize(8.5f)
                            .FontColor(Muted);

                        table.Cell()
                            .BorderBottom(last ? 0 : 0.7f)
                            .BorderColor(FieldLine)
                            .PaddingVertical(3)
                            .PaddingHorizontal(9)
                            .Text(string.IsNullOrWhiteSpace(list[i].Value)
                                ? NotRecorded
                                : Lines(list[i].Value!))
                            .FontSize(9.5f);
                    }
                });
        }

        /// <summary>
        /// The card naming who the form is about, with their initial
        /// standing in for a photograph.
        /// </summary>
        public static void PersonCard(
            IContainer container,
            string name,
            string? line,
            string? chip)
        {
            var initial = string.IsNullOrWhiteSpace(name)
                ? "?"
                : name.Trim()[0].ToString().ToUpperInvariant();

            container
                .Border(0.8f)
                .BorderColor(FieldLine)
                .CornerRadius(5)
                .Padding(10)
                .Row(row =>
                {
                    row.AutoItem().AlignMiddle().PaddingRight(12)
                        .Width(38).Height(38)
                        .Background(Mint)
                        .CornerRadius(19)
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(initial)
                        .FontSize(16)
                        .Bold()
                        .FontColor(Green);

                    row.RelativeItem().AlignMiddle().Column(column =>
                    {
                        column.Item()
                            .Text(name)
                            .FontSize(12)
                            .Bold()
                            .FontColor(Strong);

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            column.Item().PaddingTop(1)
                                .Text(line)
                                .FontSize(9)
                                .FontColor(Muted);
                        }

                        if (string.IsNullOrWhiteSpace(chip))
                            return;

                        column.Item().PaddingTop(6).AlignLeft()
                            .Background(Mint)
                            .CornerRadius(3)
                            .PaddingVertical(3)
                            .PaddingHorizontal(8)
                            .Text(chip)
                            .FontSize(8)
                            .SemiBold()
                            .FontColor(Green);
                    });
                });
        }

        // ---- choices --------------------------------------------

        /// <summary>
        /// The yes/no the portal asks with radio buttons, printed the
        /// same way so the answer is read at a glance.
        /// </summary>
        public static void Choice(
            IContainer container,
            IEnumerable<(string Label, bool Chosen)> options)
        {
            container.Row(row =>
            {
                foreach (var option in options)
                {
                    row.AutoItem().PaddingRight(26).Row(item =>
                    {
                        item.AutoItem().AlignMiddle().PaddingRight(7)
                            .Width(13).Height(13)
                            .Svg(option.Chosen
                                ? Icons.RadioOn
                                : Icons.RadioOff);

                        item.AutoItem().AlignMiddle()
                            .Text(option.Label)
                            .FontSize(9.5f)
                            .FontColor(option.Chosen ? Ink : Muted);
                    });
                }
            });
        }

        /// <summary>
        /// One of the outcome boxes. Green for the favourable one and
        /// red for the other, tinted rather than filled so both still
        /// read through a mono printer.
        /// </summary>
        public static void Outcome(
            IContainer container,
            string label,
            bool marked,
            bool adverse = false)
        {
            var line = adverse ? Alert : Good;
            var fill = adverse ? AlertPale : MintPale;

            container
                .Background(fill)
                .Border(0.9f)
                .BorderColor(line)
                .CornerRadius(5)
                .PaddingVertical(6)
                .PaddingHorizontal(12)
                .Row(row =>
                {
                    row.AutoItem().AlignMiddle().PaddingRight(10)
                        .Width(13).Height(13)
                        .Svg(marked
                            ? Icons.CheckedBox(line)
                            : Icons.EmptyBox(line));

                    row.RelativeItem().AlignMiddle()
                        .Text(label.ToUpperInvariant())
                        .FontSize(9.5f)
                        .Bold()
                        .FontColor(line)
                        .LetterSpacing(0.03f);
                });
        }

        /// <summary>
        /// One line of the approval block: who signs, where they
        /// sign, and the date beside it. Carries whoever has already
        /// decided in the portal, and leaves the box empty where
        /// nobody has.
        /// </summary>
        public static void SignatureRow(
            IContainer container,
            string role,
            string? signedBy,
            string? date)
        {
            container
                .Border(0.8f)
                .BorderColor(FieldLine)
                .CornerRadius(5)
                .Padding(5)
                .Row(row =>
                {
                    row.ConstantItem(126).AlignMiddle().PaddingLeft(3)
                        .Text(role)
                        .FontSize(9)
                        .Bold()
                        .FontColor(Strong);

                    row.RelativeItem().Element(e => Box(
                        e, signedBy, "Sign here", Icons.Pen));

                    row.AutoItem().AlignMiddle().PaddingHorizontal(10)
                        .Text("Date")
                        .FontSize(8.5f)
                        .FontColor(Muted);

                    row.ConstantItem(132).Element(e => Box(
                        e, date, "Select date", Icons.Calendar));
                });
        }

        // ---- asides ---------------------------------------------

        /// <summary>
        /// Something the reader must not skip: the pledge, or the
        /// standing instruction on the next of kin form.
        /// </summary>
        public static void Callout(IContainer container, string text) =>
            Callout(container, t => t.Span(text));

        /// <summary>
        /// The same, where part of the text needs emphasis.
        /// </summary>
        public static void Callout(
            IContainer container,
            Action<TextDescriptor> text)
        {
            container
                .Background(MintPale)
                .BorderLeft(2.5f)
                .BorderColor(Teal)
                .CornerRadius(4)
                .PaddingVertical(8)
                .PaddingHorizontal(13)
                .Text(t =>
                {
                    t.DefaultTextStyle(s => s
                        .FontSize(9.5f)
                        .FontColor(Ink)
                        .LineHeight(1.45f));

                    text(t);
                });
        }

        /// <summary>
        /// A note the portal recorded rather than something written
        /// on the page: kept small and set apart so it is never
        /// mistaken for a handwritten entry.
        /// </summary>
        public static void Note(IContainer container, string text)
        {
            container
                .BorderLeft(2)
                .BorderColor(Teal)
                .PaddingLeft(7)
                .Text(text)
                .FontSize(8)
                .FontColor(Muted);
        }

        /// <summary>
        /// The count the portal shows under a limited text box.
        /// </summary>
        public static void Counter(
            IContainer container,
            string? value,
            int limit)
        {
            container.AlignRight().PaddingTop(3)
                .Text($"{value?.Length ?? 0}/{limit}")
                .FontSize(7.5f)
                .FontColor(Placeholder);
        }

        // ---- footer ---------------------------------------------

        /// <summary>
        /// Company on the left, what this page is in the middle, page
        /// count on the right, under a gold rule. The reference
        /// repeats here so a page separated from the others can still
        /// be placed.
        /// </summary>
        public static void Footer(
            IContainer container,
            string? reference)
        {
            container.PaddingHorizontal(Gutter).PaddingBottom(16)
                .Column(column =>
                {
                    column.Item()
                        .LineHorizontal(1.1f)
                        .LineColor(Gold);

                    column.Item().PaddingTop(7).Row(row =>
                    {
                        row.AutoItem().AlignLeft()
                            .Text("Intellego Investment Consultants " +
                                  "(Pvt) Ltd")
                            .FontSize(7.5f)
                            .SemiBold()
                            .FontColor(Green);

                        row.RelativeItem().AlignCenter().PaddingHorizontal(10)
                            .Text(text =>
                        {
                            text.DefaultTextStyle(s => s
                                .FontSize(7.5f)
                                .FontColor(Muted));

                            if (!string.IsNullOrWhiteSpace(reference))
                                text.Span($"{reference}  |  ");

                            text.Span(
                                $"Printed {DateTime.Now:d MMMM yyyy}" +
                                "  |  For internal use only");
                        });

                        row.AutoItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(s => s
                                .FontSize(7.5f)
                                .FontColor(Muted));

                            text.Span("Page ");
                            text.CurrentPageNumber().SemiBold()
                                .FontColor(Ink);
                            text.Span(" of ");
                            text.TotalPages().SemiBold().FontColor(Ink);
                        });
                    });
                });
        }

        // ---- helpers --------------------------------------------

        /// <summary>
        /// Addresses and free text arrive with whatever line endings
        /// the browser sent. QuestPDF honours a bare newline, so
        /// normalise to that rather than printing a stray glyph.
        /// </summary>
        private static string Lines(string value) =>
            value.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
