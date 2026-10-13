using HrPortal.Entities;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HrPortal.Services
{
    /// <summary>
    /// Renders a weekly report to PDF in the department's established
    /// layout: title block, numbered sections with their workstream
    /// status, bullet lists, and the priorities for the next period.
    /// </summary>
    public class WeeklyReportPdfService
    {
        private readonly WeeklyReportBranding _branding;

        private const string Ink = "#1f2933";
        private const string Muted = "#6b7280";
        private const string Rule = "#d9dee5";
        private const string Accent = "#1a3a6b";

        public WeeklyReportPdfService(IOptions<WeeklyReportBranding> branding)
        {
            _branding = branding.Value;
        }

        public byte[] Render(WeeklyReport report)
        {
            // The report is dated by the period it covers, not by the
            // day it happened to be issued, so a late or reopened
            // report still reads as that week's report.
            var issued = report.PeriodEnd;

            var sections = report.Sections
                .OrderBy(s => s.Order)
                .ToList();

            var priorities = report.Priorities
                .OrderBy(p => p.Order)
                .ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(t => t
                        .FontSize(10)
                        .FontColor(Ink)
                        // Bundled with QuestPDF, so the document
                        // renders the same on any machine the portal
                        // happens to be running on.
                        .FontFamily(Fonts.Lato));

                    page.Header().Element(e => Header(e, issued));

                    page.Content().Element(e =>
                        Content(e, report, sections, priorities, issued));

                    page.Footer().Element(Footer);
                });
            });

            return document.GeneratePdf();
        }

        private void Header(IContainer container, DateTime issued)
        {
            container
                .PaddingBottom(14)
                .BorderBottom(1)
                .BorderColor(Rule)
                .PaddingBottom(6)
                .AlignRight()
                .Text($"{_branding.DepartmentName.ToUpperInvariant()} REPORT " +
                      $"| {issued:d MMMM yyyy}")
                .FontSize(8)
                .FontColor(Muted)
                .LetterSpacing(0.05f);
        }

        private void Footer(IContainer container)
        {
            container
                .PaddingTop(10)
                .BorderTop(1)
                .BorderColor(Rule)
                .PaddingTop(6)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"{_branding.OrganisationName} | " +
                              $"{_branding.DepartmentName}")
                        .FontSize(8)
                        .FontColor(Muted);

                    row.ConstantItem(80).AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(t => t
                            .FontSize(8)
                            .FontColor(Muted));

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
            container.Column(column =>
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
                        // An untouched section is left out rather than
                        // printed empty.
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
                    .FontColor(Muted);
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
                    .FontSize(11)
                    .FontColor(Accent)
                    .SemiBold()
                    .LetterSpacing(0.2f);

                column.Item().PaddingTop(2)
                    .Text(_branding.DocumentTitle)
                    .FontSize(28)
                    .Bold()
                    .FontColor(Ink)
                    .LineHeight(1.1f);

                column.Item().PaddingTop(6)
                    .Text(_branding.Subtitle)
                    .FontSize(10)
                    .FontColor(Muted)
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
                    .FontColor(Muted);

                column.Item().PaddingTop(2)
                    .Text($"{_branding.OrganisationName} | " +
                          $"{_branding.Classification}")
                    .FontSize(9)
                    .FontColor(Muted);

                column.Item().PaddingTop(10)
                    .BorderTop(2)
                    .BorderColor(Accent)
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
                .FontColor(Accent);
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
                                .FontColor(Accent);
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
                        row.ConstantItem(12)
                            .Text("•")
                            .FontColor(Accent);

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

        /// <summary>
        /// The printable form of a workstream status, or null when the
        /// section is a plain list with no status against it.
        /// </summary>
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
