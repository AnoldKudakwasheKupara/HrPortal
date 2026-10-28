using HrPortal.Entities;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    public class WeeklyReportPdfService
    {
        private readonly WeeklyReportBranding _branding;

        private readonly BrandAssets _brand;

        public WeeklyReportPdfService(
            IOptions<WeeklyReportBranding> branding,
            BrandAssets brand)
        {
            _branding = branding.Value;
            _brand = brand;
        }

        public byte[] Render(WeeklyReport report) =>
            Build(report).GeneratePdf();

        /// <summary>
        /// The document itself, kept separate from how it is
        /// rendered so it can also be exported as images for a
        /// visual check.
        /// </summary>
        public IDocument Build(WeeklyReport report)
        {
            var issued = report.PeriodEnd;

            var sections = report.Sections
                .OrderBy(s => s.Order)
                .ToList();

            var priorities = report.Priorities
                .OrderBy(p => p.Order)
                .ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page, 10);

                    page.Header().Element(e => DocumentStyle.Head(
                        e,
                        _brand,
                        _branding.DocumentTitle,
                        $"{report.PeriodStart:d MMM} – " +
                        $"{report.PeriodEnd:d MMM yyyy}"));

                    page.Content().Element(e =>
                        Content(e, report, sections, priorities, issued));

                    page.Footer().Element(Footer);
                });
            });
        }

        private void Footer(IContainer container)
        {
            DocumentStyle.Gutters(container)
                .PaddingBottom(16)
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(DocumentStyle.Rule)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"{_branding.OrganisationName} | " +
                              $"{_branding.DepartmentName}")
                        .FontSize(8)
                        .FontColor(DocumentStyle.Muted);

                    row.ConstantItem(80).AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(t => t
                            .FontSize(8)
                            .FontColor(DocumentStyle.Muted));

                        text.Span("Page ");
                        text.CurrentPageNumber();
                    });
                });
        }

        private void Content(
            IContainer container,
            WeeklyReport report,
            List<WeeklyReportSection> sections,
            List<WeeklyReportPriority> priorities,
            DateTime issued)
        {
            DocumentStyle.Gutters(container).Column(column =>
            {
                column.Spacing(14);

                column.Item().Element(TitleBlock);

                column.Item().Element(e => PeriodStrip(e, report));

                if (!string.IsNullOrWhiteSpace(report.ExecutiveOverview))
                {
                    column.Item().Element(e => Heading(e, "Executive Overview"));

                    column.Item()
                        .Text(report.ExecutiveOverview.Trim())
                        .LineHeight(1.4f)
                        .Justify();
                }

                foreach (var section in sections)
                {
                    if (string.IsNullOrWhiteSpace(section.Body) &&
                        string.IsNullOrWhiteSpace(section.Workstream))
                    {
                        continue;
                    }

                    column.Item().Element(e => Section(e, section));
                }

                if (priorities.Any())
                {
                    column.Item().Element(e => Heading(
                        e,
                        $"{NextNumber(sections):00}. " +
                        "Priorities for the Next Reporting Period",
                        numbered: true));

                    column.Item().Element(e => Bullets(
                        e,
                        priorities.Select(p => p.Text).ToList()));
                }

                column.Item().PaddingTop(16).AlignCenter()
                    .Text($"END OF REPORT | {_branding.DepartmentName} " +
                          $"— {_branding.OrganisationName} " +
                          $"| {issued:dd MMMM yyyy}")
                    .FontSize(8)
                    .FontColor(DocumentStyle.Muted);
            });
        }

        private static int NextNumber(List<WeeklyReportSection> sections) =>
            sections.Where(Printed)
                .Select(s => s.Order)
                .DefaultIfEmpty(0)
                .Max() + 1;

        private static bool Printed(WeeklyReportSection s) =>
            !string.IsNullOrWhiteSpace(s.Body) ||
            !string.IsNullOrWhiteSpace(s.Workstream);

        private void TitleBlock(IContainer container)
        {
            container.Column(column =>
            {
                column.Item()
                    .Text(_branding.DepartmentName.ToUpperInvariant())
                    .FontSize(26)
                    .Bold()
                    .FontColor(DocumentStyle.Ink)
                    .LineHeight(1.1f);

                column.Item().PaddingTop(6)
                    .Text(_branding.Subtitle)
                    .FontSize(10)
                    .FontColor(DocumentStyle.Muted)
                    .LetterSpacing(0.1f);
            });
        }

        private void PeriodStrip(IContainer container, WeeklyReport report)
        {
            container.Column(column =>
            {
                column.Item()
                    .Text($"Prepared by: {report.PreparedBy}")
                    .FontSize(9)
                    .FontColor(DocumentStyle.Muted);

                column.Item().PaddingTop(2)
                    .Text($"{_branding.OrganisationName} | " +
                          $"{_branding.Classification}")
                    .FontSize(9)
                    .FontColor(DocumentStyle.Muted);

                column.Item().PaddingTop(10)
                    .BorderTop(2)
                    .BorderColor(DocumentStyle.Green)
                    .PaddingTop(6)
                    .Text($"Reporting Period: " +
                          $"{report.PeriodStart:dd MMMM} – " +
                          $"{report.PeriodEnd:dd MMMM yyyy}")
                    .FontSize(10)
                    .SemiBold();
            });
        }

        private void Heading(
            IContainer container,
            string text,
            bool numbered = false)
        {
            container
                .PaddingTop(numbered ? 6 : 2)
                .Text(text)
                .FontSize(13)
                .SemiBold()
                .FontColor(DocumentStyle.Green);
        }

        private void Section(IContainer container, WeeklyReportSection section)
        {
            container.Column(column =>
            {
                column.Spacing(6);

                column.Item().Element(e => Heading(
                    e,
                    $"{section.Order:00}. {section.Heading}",
                    numbered: true));

                if (!string.IsNullOrWhiteSpace(section.Workstream))
                {
                    var status = StatusLabel(section.WorkstreamStatus);

                    column.Item().Text(text =>
                    {
                        text.DefaultTextStyle(t => t.FontSize(10).SemiBold());
                        text.Span(section.Workstream);

                        if (status != null)
                        {
                            text.Span($": {status}")
                                .FontColor(DocumentStyle.Green);
                        }
                    });
                }

                if (!string.IsNullOrWhiteSpace(section.Body))
                {
                    column.Item().Element(e => Bullets(e, Lines(section.Body)));
                }
            });
        }

        private static void Bullets(IContainer container, List<string> lines)
        {
            container.Column(column =>
            {
                column.Spacing(5);

                foreach (var line in lines)
                {
                    column.Item().PaddingLeft(10).Row(row =>
                    {
                        row.ConstantItem(12).PaddingTop(5).AlignLeft()
                            .Width(3.5f).Height(3.5f)
                            .Background(DocumentStyle.Teal);

                        row.RelativeItem()
                            .Text(line)
                            .LineHeight(1.35f)
                            .Justify();
                    });
                }
            });
        }

        public static List<string> Lines(string? body) =>
            (body ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

        public static string? StatusLabel(WorkstreamStatus status) => status switch
        {
            WorkstreamStatus.None => null,
            WorkstreamStatus.MonitoringOngoing => "Monitoring Ongoing",
            WorkstreamStatus.ActiveMonitoringOngoing =>
                "Active / Monitoring Ongoing",
            WorkstreamStatus.InProgress => "In Progress",
            WorkstreamStatus.OnHold => "On Hold",
            _ => status.ToString()
        };
    }
}
