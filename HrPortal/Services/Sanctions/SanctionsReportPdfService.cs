using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services.Sanctions
{
    /// <summary>
    /// The record of one sanctions review: what it found in plain
    /// words, the two documents compared and their SHA-256, every
    /// difference, every discrepancy, who was checked and every match
    /// with how it was resolved, and who finalised it. It is made once,
    /// at finalising, and kept with its own SHA-256 so any copy can
    /// later be checked against it.
    /// </summary>
    public class SanctionsReportPdfService
    {
        private const string Title = "Sanctions Screening Report";

        private readonly BrandAssets _brand;

        public SanctionsReportPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(SanctionsReview review) =>
            Build(review).GeneratePdf();

        public IDocument Build(SanctionsReview review) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page);

                    page.Header().Element(e => DocumentStyle.Head(
                        e, _brand, Title, review.ReviewNumber));

                    page.Content().Element(e => Content(e, review));

                    page.Footer().Element(e => DocumentStyle.Footer(e, review.ReviewNumber));
                });
            });

        private static string Stamp(DateTime? at) => DocumentStyle.Stamp(at) ?? "Not recorded";

        private static (string, string?)[] DocumentRows(string which, SanctionsDocument? d) =>
            d == null
                ? new (string, string?)[] { (which, "None. This was the first document.") }
                : new (string, string?)[]
                {
                    (which, d.Title ?? d.OriginalFileName),
                    ("Reference and date", $"{d.DocumentReference ?? "No reference"}, " +
                                            (d.IssuedOn?.ToString("d MMMM yyyy") ?? "undated")),
                    ("File", d.OriginalFileName),
                    ("SHA-256", d.Sha256),
                    ("Uploaded", $"{Stamp(d.UploadedAt)} by {d.UploadedByName}")
                };

        private static void Content(IContainer container, SanctionsReview review)
        {
            var matches = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                .OrderByDescending(c => c.Score)
                .ToList();

            var discrepancies = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.Discrepancy)
                .OrderBy(c => c.RaisedAt)
                .ToList();

            var changes = review.Changes
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Reference)
                .ToList();

            DocumentStyle.Gutters(container)
                .PaddingTop(14)
                .Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "01", "Findings", "What this review found"));

                    foreach (var sentence in SanctionsFindings.Sentences(review))
                        column.Item().Text(sentence).FontSize(10);

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "02", "Documents compared", "Exactly which files were used"));

                    column.Item().Element(e => DocumentStyle.DetailTable(
                        e,
                        DocumentRows("New document", review.NewDocument)
                            .Concat(DocumentRows("Compared with", review.PreviousDocument))));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "03", "Differences", "Names added, removed or changed"));

                    column.Item().Element(e => DocumentStyle.Tally(e, new[]
                    {
                        ("Added", changes.Count(c => c.Type == SanctionsChangeType.Added).ToString()),
                        ("Removed", changes.Count(c => c.Type == SanctionsChangeType.Removed).ToString()),
                        ("Changed", changes.Count(c => c.Type == SanctionsChangeType.Changed).ToString())
                    }));

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        new[]
                        {
                            new ListColumn("Difference", 1.1f),
                            new ListColumn("Reference", 1.4f),
                            new ListColumn("Name", 2.5f),
                            new ListColumn("What changed", 4)
                        },
                        changes
                            .Select(c => (IReadOnlyList<string?>)new[]
                            {
                                c.Type.ToString(), c.Reference, c.Name, c.Differences
                            })
                            .ToList(),
                        review.PreviousDocument == null
                            ? "Nothing to compare: this was the first document."
                            : "No differences."));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "04", "Discrepancies", "Anything in the documents that did not add up"));

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        new[]
                        {
                            new ListColumn("Matter", 4),
                            new ListColumn("Outcome", 3)
                        },
                        discrepancies
                            .Select(c => (IReadOnlyList<string?>)new[] { c.Reason, Outcome(c) })
                            .ToList(),
                        "No discrepancies were found."));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "05", "Staff, vendors and clients", "Checked against every name on the new document"));

                    column.Item().Element(e => DocumentStyle.Tally(e, new[]
                    {
                        ("Staff", review.ScreenedStaff.ToString()),
                        ("Vendors", review.ScreenedVendors.ToString()),
                        ("Clients", review.ScreenedClients.ToString()),
                        ("Possible matches", matches.Count.ToString())
                    }));

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        review.ClientList == null
                            ? "No client list had been uploaded, so no clients were checked."
                            : $"Clients from {review.ClientList.OriginalFileName}, uploaded " +
                              $"{Stamp(review.ClientList.UploadedAt)} (SHA-256 {review.ClientList.Sha256})."));

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        new[]
                        {
                            new ListColumn("Listed party", 2),
                            new ListColumn("Who", 2),
                            new ListColumn("Why raised", 2.5f),
                            new ListColumn("Outcome", 3)
                        },
                        matches
                            .Select(c => (IReadOnlyList<string?>)new[]
                            {
                                $"{c.Reference}\n{c.ListedName}",
                                $"{c.SubjectType}\n{c.SubjectName}",
                                $"{c.Reason}\nScore {c.Score}",
                                Outcome(c)
                            })
                            .ToList(),
                        "Nobody the company deals with is on the new document."));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "06", "Finalised", "Every matter was resolved before this was made"));

                    column.Item().Element(e => DocumentStyle.DetailTable(e, new (string, string?)[]
                    {
                        ("Finalised by", review.FinalisedByName),
                        ("Finalised", Stamp(review.FinalisedAt)),
                        ("Note", review.Note ?? "None")
                    }));

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        "The portal keeps this report exactly as it was made, with its SHA-256. " +
                        "To confirm a copy is genuine and unaltered, upload it under Sanctions " +
                        "Screening → Verify a report."));
                });
        }

        private static string Outcome(SanctionsConcern concern) =>
            concern.Status == SanctionsConcernStatus.Open
                ? "Open"
                : $"{concern.Status}: {concern.Resolution}\n" +
                  $"{concern.ResolvedByName}, {DocumentStyle.Stamp(concern.ResolvedAt)}";
    }
}
