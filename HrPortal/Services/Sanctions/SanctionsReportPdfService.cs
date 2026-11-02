using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services.Sanctions
{
    /// <summary>
    /// The record of one sanctions review: the document it was based
    /// on and its SHA-256, what changed on the register, who was
    /// screened, every matter raised and how it was resolved, and who
    /// signed it off. It is made once, at sign-off, and kept with its
    /// own SHA-256 so that any copy can later be checked against it.
    /// </summary>
    public class SanctionsReportPdfService
    {
        private const string Title = "Sanctions Screening Report";

        private readonly BrandAssets _brand;

        public SanctionsReportPdfService(BrandAssets brand)
        {
            _brand = brand;
        }

        public byte[] Render(SanctionsList list) =>
            Build(list).GeneratePdf();

        public IDocument Build(SanctionsList list) =>
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    DocumentStyle.Apply(page);

                    page.Header().Element(e => DocumentStyle.Head(
                        e, _brand, Title, list.ReviewNumber));

                    page.Content().Element(e => Content(e, list));

                    page.Footer().Element(e => DocumentStyle.Footer(e, list.ReviewNumber));
                });
            });

        private static string Stamp(DateTime? at) => DocumentStyle.Stamp(at) ?? "Not recorded";

        private static void Content(IContainer container, SanctionsList list)
        {
            var matches = list.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                .OrderByDescending(c => c.Score)
                .ToList();

            var discrepancies = list.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.Discrepancy)
                .OrderBy(c => c.RaisedAt)
                .ToList();

            var changes = list.Changes
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Reference)
                .ToList();

            DocumentStyle.Gutters(container)
                .PaddingTop(14)
                .Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "01", "The document", "What was received and exactly which file"));

                    column.Item().Element(e => DocumentStyle.DetailTable(e, new (string, string?)[]
                    {
                        ("Title", list.Title),
                        ("Document reference", list.DocumentReference),
                        ("Dated", list.IssuedOn?.ToString("d MMMM yyyy")),
                        ("Treated as", list.Kind switch
                        {
                            Sanctions.SanctionsListKind.FullList => "Full list (replaces the register)",
                            Sanctions.SanctionsListKind.Removals => "Removals",
                            _ => "Additions and amendments"
                        }),
                        ("File", list.OriginalFileName),
                        ("SHA-256 of the file", list.Sha256),
                        ("Uploaded", $"{Stamp(list.UploadedAt)} by {list.UploadedByName}"),
                        ("Applied", $"{Stamp(list.AppliedAt)} by {list.AppliedByName}")
                    }));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "02", "Changes to the register", "Compared with the register before this document"));

                    column.Item().Element(e => DocumentStyle.Tally(e, new[]
                    {
                        ("Added", changes.Count(c => c.Type == SanctionsChangeType.Added).ToString()),
                        ("Amended", changes.Count(c => c.Type == SanctionsChangeType.Amended).ToString()),
                        ("Removed", changes.Count(c => c.Type == SanctionsChangeType.Removed).ToString()),
                        ("On the register", list.RegisterSizeAfter.ToString())
                    }));

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        new[]
                        {
                            new ListColumn("Change", 1.1f),
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
                        "Nothing on the register changed."));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "03", "Screening", "Everybody the company deals with, checked against the whole register"));

                    column.Item().Element(e => DocumentStyle.Tally(e, new[]
                    {
                        ("Staff", list.ScreenedStaff.ToString()),
                        ("Vendors", list.ScreenedVendors.ToString()),
                        ("Clients", list.ScreenedClients.ToString()),
                        ("Possible matches", matches.Count.ToString())
                    }));

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        list.ClientList == null
                            ? "No client list had been uploaded, so no clients were screened."
                            : $"Clients from {list.ClientList.OriginalFileName}, uploaded " +
                              $"{Stamp(list.ClientList.UploadedAt)} (SHA-256 {list.ClientList.Sha256})."));

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
                        "Nobody the company deals with matched anyone on the register."));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "04", "Discrepancies", "Anything in the document that did not add up"));

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
                        e, "05", "Sign-off", "Every matter was resolved before this was signed"));

                    column.Item().Element(e => DocumentStyle.DetailTable(e, new (string, string?)[]
                    {
                        ("Signed off by", list.SignedOffByName),
                        ("Signed off", Stamp(list.SignedOffAt)),
                        ("Note", list.SignOffNote ?? "None")
                    }));

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        "The portal keeps this report exactly as it was made, with its SHA-256. " +
                        "To confirm a copy is genuine and unaltered, upload it under Sanctions " +
                        "screening → Verify a report."));
                });
        }

        private static string Outcome(SanctionsConcern concern) =>
            concern.Status == SanctionsConcernStatus.Open
                ? "Open"
                : $"{concern.Status}: {concern.Resolution}\n" +
                  $"{concern.ResolvedByName}, {DocumentStyle.Stamp(concern.ResolvedAt)}";
    }
}
