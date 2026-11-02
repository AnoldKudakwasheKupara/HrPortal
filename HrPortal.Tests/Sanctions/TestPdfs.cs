using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Tests.Sanctions
{
    /// <summary>A passage of an entry and how the document marks it.</summary>
    public sealed record Span(string Text, bool Underlined = false, bool Struck = false);

    /// <summary>
    /// Sanctions documents made for the tests, laid out the way the
    /// UN prints them, with underlined and struck-through passages
    /// where a test needs them.
    /// </summary>
    public static class TestPdfs
    {
        static TestPdfs()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
        }

        public static string Fixture(string name) =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

        public static Stream Sample() =>
            File.OpenRead(Fixture("SC16494-1988-amends-five-names.pdf"));

        public static MemoryStream Document(string title, params IReadOnlyList<Span>[] entries)
        {
            var bytes = QuestPDF.Fluent.Document.Create(c => c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(40);
                p.DefaultTextStyle(t => t.FontSize(10));
                p.Content().Column(col =>
                {
                    col.Item().Text("SC/17000");
                    col.Item().Text("1 November 2026");
                    col.Item().Text(title);
                    col.Item().Text("On 1 November 2026, the Committee made the changes below.");

                    foreach (var entry in entries)
                    {
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            foreach (var span in entry)
                            {
                                var s = text.Span(span.Text);

                                if (span.Underlined)
                                    s.Underline();

                                if (span.Struck)
                                    s.Strikethrough();
                            }
                        });
                    }

                    col.Item().PaddingTop(8).Text("* *** *");
                });
            })).GeneratePdf();

            return new MemoryStream(bytes);
        }

        /// <summary>A page with a drawing but no text, as a scan would be.</summary>
        public static MemoryStream NoText() =>
            new(QuestPDF.Fluent.Document.Create(c => c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Content().Padding(50).Height(300).Background(Colors.Grey.Lighten2);
            })).GeneratePdf());

        public static Span[] Plain(string text) => new[] { new Span(text) };
    }
}
