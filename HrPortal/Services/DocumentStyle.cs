using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// The chrome every printed document shares: the letterhead, the
    /// section headings, the field and signature rules, the footer.
    /// It lived four times over in the form services, in a grey that
    /// belonged to no one, which is why the forms read as generic.
    /// One copy here, in the company's colours, keeps them
    /// consistent and keeps a restyle to a single file.
    ///
    /// These are printed and signed by hand, so colour earns its
    /// place in rules, labels and headings and stays out of the body:
    /// a form that goes through a mono laser printer still has to
    /// read cleanly, and toner costs money.
    /// </summary>
    public static class DocumentStyle
    {
        /// <summary>The leading accent from intellego-ic.com.</summary>
        public const string Teal = "#018080";

        /// <summary>Deeper green, for headings that carry weight.</summary>
        public const string Green = "#10675d";

        /// <summary>Body text. Warmer and softer than pure black.</summary>
        public const string Ink = "#3f4245";

        /// <summary>Labels and anything secondary.</summary>
        public const string Muted = "#6b6f74";

        /// <summary>Hairlines, field underlines, table borders.</summary>
        public const string Rule = "#d8d3c8";

        /// <summary>The warm paper tint behind label cells.</summary>
        public const string Wash = "#f6f4ef";

        /// <summary>The company gold, used sparingly for emphasis.</summary>
        public const string Gold = "#7e6e1e";

        /// <summary>Shown where a value was never captured.</summary>
        public const string NotRecorded = "—";

        /// <summary>
        /// Applies the shared page setup: A4, margins, Montserrat.
        /// </summary>
        public static void Apply(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(1.25f, Unit.Centimetre);

            page.DefaultTextStyle(t => t
                .FontSize(9)
                .FontColor(Ink)
                .FontFamily(DocumentFonts.Family, "Lato"));
        }

        /// <summary>
        /// The masthead: artwork on the left, what this document is
        /// and its reference on the right, over a teal rule. Falls
        /// back to the company name set in type where the artwork
        /// cannot be read.
        /// </summary>
        public static void Letterhead(
            IContainer container,
            BrandAssets brand,
            string title,
            string? reference)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().AlignLeft().AlignMiddle()
                        .Element(e =>
                        {
                            if (brand.HasLogo)
                            {
                                e.Width(150).Image(brand.Logo!);
                                return;
                            }

                            e.Text("INTELLEGO INVESTMENT\nCONSULTANTS (PVT) LTD")
                                .FontSize(10)
                                .Bold()
                                .FontColor(Green)
                                .LineHeight(1.3f);
                        });

                    row.RelativeItem().AlignRight().AlignBottom()
                        .Column(right =>
                        {
                            right.Item()
                                .Text(title.ToUpperInvariant())
                                .FontSize(13)
                                .Bold()
                                .FontColor(Green)
                                .LetterSpacing(0.06f);

                            if (string.IsNullOrWhiteSpace(reference))
                                return;

                            right.Item().PaddingTop(2)
                                .Text(reference)
                                .FontSize(9)
                                .SemiBold()
                                .FontColor(Teal)
                                .LetterSpacing(0.04f);
                        });
                });

                column.Item().PaddingTop(5)
                    .LineHorizontal(1.6f)
                    .LineColor(Teal);
            });
        }

        /// <summary>
        /// A numbered section heading: the number in teal, the title
        /// in green, a hairline carrying across to the margin so the
        /// eye knows where one block of the form ends.
        /// </summary>
        public static void Section(
            IContainer container,
            string number,
            string title)
        {
            container.PaddingTop(4).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.AutoItem()
                        .Text(number)
                        .FontSize(10)
                        .Bold()
                        .FontColor(Teal);

                    row.AutoItem().PaddingHorizontal(6).AlignMiddle()
                        .Text("—")
                        .FontSize(9)
                        .FontColor(Rule);

                    row.RelativeItem().AlignMiddle()
                        .Text(title.ToUpperInvariant())
                        .FontSize(9.5f)
                        .Bold()
                        .FontColor(Green)
                        .LetterSpacing(0.07f);
                });

                column.Item().PaddingTop(2)
                    .LineHorizontal(0.6f)
                    .LineColor(Rule);
            });
        }

        /// <summary>
        /// A label above its value, underlined the way the paper form
        /// is. The label is small and muted so the value reads first.
        /// </summary>
        public static void Field(
            IContainer container,
            string label,
            string? value)
        {
            container.Column(column =>
            {
                column.Item()
                    .Text(label.ToUpperInvariant())
                    .FontSize(7)
                    .SemiBold()
                    .FontColor(Muted)
                    .LetterSpacing(0.08f);

                column.Item()
                    .PaddingTop(2)
                    .BorderBottom(0.8f)
                    .BorderColor(Rule)
                    .PaddingBottom(3)
                    .Text(string.IsNullOrWhiteSpace(value)
                        ? NotRecorded
                        : value)
                    .FontSize(9.5f);
            });
        }

        /// <summary>
        /// A field left empty on purpose, for someone to sign or date
        /// in ink.
        /// </summary>
        public static void Blank(IContainer container, string label)
        {
            container.Column(column =>
            {
                column.Item()
                    .Text(label.ToUpperInvariant())
                    .FontSize(7)
                    .SemiBold()
                    .FontColor(Muted)
                    .LetterSpacing(0.08f);

                column.Item()
                    .PaddingTop(2)
                    .MinHeight(17)
                    .BorderBottom(0.8f)
                    .BorderColor(Rule);
            });
        }

        /// <summary>
        /// The label/value table the forms open with. Labels sit on
        /// the warm wash, values on white, so the pair reads as one
        /// row without needing a heavy grid.
        /// </summary>
        public static void DetailTable(
            IContainer container,
            IEnumerable<(string Label, string? Value)> rows)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });

                foreach (var row in rows)
                {
                    table.Cell()
                        .Border(0.6f)
                        .BorderColor(Rule)
                        .Background(Wash)
                        .PaddingVertical(3.5f)
                        .PaddingHorizontal(6)
                        .Text(row.Label.ToUpperInvariant())
                        .FontSize(7)
                        .SemiBold()
                        .FontColor(Muted)
                        .LetterSpacing(0.06f);

                    table.Cell()
                        .Border(0.6f)
                        .BorderColor(Rule)
                        .PaddingVertical(3.5f)
                        .PaddingHorizontal(6)
                        .Text(string.IsNullOrWhiteSpace(row.Value)
                            ? NotRecorded
                            : row.Value)
                        .FontSize(9.5f);
                }
            });
        }

        /// <summary>
        /// A tick box. The one that applies is filled in the brand
        /// green with a white mark, which survives greyscale; the one
        /// that does not stays an empty outline.
        /// </summary>
        public static void TickBox(
            IContainer container,
            string label,
            bool marked)
        {
            container
                .Border(0.8f)
                .BorderColor(marked ? Green : Rule)
                .Background(marked ? Wash : Colors.White)
                .PaddingVertical(7)
                .PaddingHorizontal(8)
                .Row(row =>
                {
                    row.AutoItem().AlignMiddle()
                        .Width(12).Height(12)
                        .Background(marked ? Green : Colors.White)
                        .Border(0.8f)
                        .BorderColor(marked ? Green : Rule)
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(marked ? "X" : " ")
                        .FontSize(8)
                        .Bold()
                        .FontColor(Colors.White);

                    row.RelativeItem().PaddingLeft(8).AlignMiddle()
                        .Text(label.ToUpperInvariant())
                        .FontSize(8.5f)
                        .Bold()
                        .FontColor(marked ? Green : Muted)
                        .LetterSpacing(0.05f);
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
                .PaddingLeft(6)
                .Text(text)
                .FontSize(7.5f)
                .FontColor(Muted);
        }

        /// <summary>
        /// Company line on the left, page count on the right, over a
        /// hairline. The reference repeats here so a page separated
        /// from the others can still be identified.
        /// </summary>
        public static void Footer(
            IContainer container,
            string? reference)
        {
            container.Column(column =>
            {
                column.Item()
                    .LineHorizontal(0.6f)
                    .LineColor(Rule);

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(s => s
                            .FontSize(7)
                            .FontColor(Muted));

                        text.Span("Intellego Investment Consultants (Pvt) Ltd")
                            .SemiBold()
                            .FontColor(Green);

                        if (!string.IsNullOrWhiteSpace(reference))
                            text.Span($"  ·  {reference}");

                        text.Span(
                            $"  ·  Printed {DateTime.Now:d MMMM yyyy}");
                    });

                    row.AutoItem().Text(text =>
                    {
                        text.DefaultTextStyle(s => s
                            .FontSize(7)
                            .FontColor(Muted));

                        text.Span("Page ");
                        text.CurrentPageNumber().SemiBold();
                        text.Span(" of ");
                        text.TotalPages().SemiBold();
                    });
                });
            });
        }
    }
}
