using HrPortal.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HrPortal.Services.Sanctions
{
    /// <summary>
    /// The record of one sanctions review: what it found in plain
    /// words, exactly which documents were compared, every difference
    /// field by field, every discrepancy, who was checked and every
    /// match with its evidence and outcome, what could not be settled
    /// and the limits of what was checked, and who finalised it. It is
    /// made once, at finalising, and kept with its own SHA-256 so any
    /// copy can later be checked against it.
    /// </summary>
    public class SanctionsReportPdfService
    {
        private const string Title = "Sanctions Screening Report";

        public static string Organisation => Brand.Current.FullName;

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
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"{Title} {review.ReviewNumber}",
                Subject = review.ReviewNumber,
                Author = Organisation,
                Keywords = $"sanctions-review:{review.ReviewNumber}"
            });

        private static string Stamp(DateTime? at) => DocumentStyle.Stamp(at) ?? "Not recorded";

        private static (string, string?)[] DocumentRows(string which, SanctionsDocument? d, bool isNotice) =>
            d == null
                ? new (string, string?)[]
                {
                    (which, isNotice
                        ? "Each name in the notice was compared with its latest earlier version uploaded."
                        : "None. This was the first full list of its kind uploaded.")
                }
                : new (string, string?)[]
                {
                    (which, d.Title ?? d.OriginalFileName),
                    ("Issued by", d.Source ?? "Not stated in the document"),
                    ("List", d.ListName ?? "Not stated in the document"),
                    ("Kind", SanctionsText.Kind(d.Kind)),
                    ("Reference and date", $"{d.DocumentReference ?? "No reference"}, " +
                                            (d.IssuedOn?.ToString("d MMMM yyyy") ?? "undated")),
                    ("Names read", $"{d.EntryCount}" + (d.PageCount > 0 ? $" from {d.PageCount} pages" : "")),
                    ("File", d.OriginalFileName + (d.FileSize > 0 ? $" ({SanctionsText.Size(d.FileSize)})" : "")),
                    ("SHA-256", d.Sha256),
                    ("Uploaded", $"{Stamp(d.UploadedAt)} by {d.UploadedByName}")
                };

        private static void Content(IContainer container, SanctionsReview review)
        {
            var isNotice = review.NewDocument.Kind != SanctionsDocumentKind.FullList;

            var matches = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch && c.Strength != SanctionsMatchStrength.SetAside)
                .OrderBy(c => c.Strength)
                .ThenByDescending(c => c.Score)
                .ToList();

            var setAside = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch && c.Strength == SanctionsMatchStrength.SetAside)
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

            var limitations = Limitations(review, isNotice);
            var unsettled = review.Concerns.Where(c => c.Status == SanctionsConcernStatus.UnableToDetermine).ToList();

            DocumentStyle.Gutters(container)
                .PaddingTop(14)
                .Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "01", "Findings", "What this review found"));

                    column.Item().Element(e => DocumentStyle.DetailTable(e, new (string, string?)[]
                    {
                        ("Organisation", Organisation),
                        ("Review", review.ReviewNumber +
                                   (review.CorrectsReview == null ? "" : $", correcting {review.CorrectsReview.ReviewNumber}")),
                        ("Started", $"{Stamp(review.CreatedAt)} by {review.CreatedByName}"),
                        ("Final status", unsettled.Count == 0
                            ? "Finalised. Every matter has an outcome."
                            : $"Finalised with {unsettled.Count} matter{(unsettled.Count == 1 ? "" : "s")} " +
                              "that could not be determined and were escalated (see section 08).")
                    }));

                    foreach (var sentence in SanctionsFindings.Sentences(review))
                        column.Item().Text(sentence).FontSize(10);

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "02", "Documents compared", "Exactly which files were used"));

                    column.Item().Element(e => DocumentStyle.DetailTable(
                        e,
                        DocumentRows("New document", review.NewDocument, isNotice)
                            .Concat(DocumentRows("Compared with", review.PreviousDocument, isNotice))));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "03", "Differences", "Names added, removed or amended, field by field"));

                    column.Item().Element(e => DocumentStyle.Tally(e, new[]
                    {
                        ("Added", changes.Count(c => c.Type == SanctionsChangeType.Added).ToString()),
                        ("Removed", changes.Count(c => c.Type == SanctionsChangeType.Removed).ToString()),
                        ("Amended", changes.Count(c => c.Type == SanctionsChangeType.Changed).ToString()),
                        ("Unchanged", review.UnchangedCount.ToString())
                    }));

                    foreach (var note in SanctionsService.Lines(review.ComparisonNote))
                        column.Item().Element(e => DocumentStyle.Note(e, note));

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        new[]
                        {
                            new ListColumn("Entry", 2.4f),
                            new ListColumn("Field", 1.5f),
                            new ListColumn("Previous information", 2.6f),
                            new ListColumn("New information", 2.6f)
                        },
                        changes
                            .SelectMany(c =>
                            {
                                var entry = $"{SanctionsText.ChangeType(c.Type)}: {c.Reference} {c.Name}" +
                                            (c.Page == null ? "" : $" (page {c.Page})");

                                var rows = SanctionsText.Differences(c.Differences);

                                if (rows.Count == 0)
                                {
                                    return new[]
                                    {
                                        (IReadOnlyList<string?>)new[]
                                        {
                                            entry,
                                            c.Type == SanctionsChangeType.Changed ? "Not worked out" : "Whole entry",
                                            c.Type == SanctionsChangeType.Added ? "Not listed" : c.Note,
                                            c.Type == SanctionsChangeType.Removed ? "No longer listed" : c.Note
                                        }
                                    };
                                }

                                return rows.Select((r, i) => (IReadOnlyList<string?>)new[]
                                {
                                    i == 0 ? entry + (c.Note == null ? "" : $"\n{c.Note}") : "",
                                    r.Field,
                                    r.Before ?? "—",
                                    r.After ?? "—"
                                });
                            })
                            .ToList(),
                        review.PreviousDocument == null && !isNotice
                            ? "Nothing to compare: this was the first full list of its kind."
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
                        e, "05", "Who was checked",
                        isNotice ? "Against the names this notice gives" : "Against every name on the new document"));

                    column.Item().Element(e => DocumentStyle.Tally(e, new[]
                    {
                        ("Staff", review.ScreenedStaff.ToString()),
                        ("Vendors", review.ScreenedVendors.ToString()),
                        ("Clients", review.ScreenedClients.ToString()),
                        ("Possible matches", matches.Count.ToString())
                    }));

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        "Active staff and active vendors with their contact people, as held in the portal. " +
                        (review.ClientList == null
                            ? "No client list had been imported, so no clients were checked."
                            : $"Clients from {review.ClientList.OriginalFileName}, uploaded " +
                              $"{Stamp(review.ClientList.UploadedAt)} (SHA-256 {review.ClientList.Sha256}): " +
                              $"{review.ClientList.RowsReceived} rows received, {review.ClientList.RowsImported} imported, " +
                              $"{review.ClientList.RowsInvalid} invalid, {review.ClientList.RowsDuplicated} duplicates.")));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "06", "Matches and outcomes", "Exact and potential matches, why, and what was decided"));

                    column.Item().Element(e => DocumentStyle.ListTable(
                        e,
                        new[]
                        {
                            new ListColumn("Match", 2.2f),
                            new ListColumn("Why it was flagged", 3.2f),
                            new ListColumn("Outcome", 3)
                        },
                        matches
                            .Select(c => (IReadOnlyList<string?>)new[]
                            {
                                $"{SanctionsText.Strength(c)}\n{c.SubjectType}: {c.SubjectName}\nListed: {c.Reference} {c.ListedName}",
                                c.Reason + (c.Evidence == null ? "" : "\n" + string.Join("\n", SanctionsService.Lines(c.Evidence))),
                                Outcome(c) + Notes(c)
                            })
                            .ToList(),
                        "Nobody the company deals with matches a name checked."));

                    if (setAside.Count > 0)
                    {
                        column.Item().Element(DocumentStyle.Divider);

                        column.Item().Element(e => DocumentStyle.Section(
                            e, "07", "Set aside", "Similar names ruled out by the date of birth, kept on record"));

                        column.Item().Element(e => DocumentStyle.ListTable(
                            e,
                            new[]
                            {
                                new ListColumn("Who", 2.5f),
                                new ListColumn("Listed", 2.5f),
                                new ListColumn("Why set aside", 4)
                            },
                            setAside
                                .Select(c => (IReadOnlyList<string?>)new[]
                                {
                                    $"{c.SubjectType}: {c.SubjectName}",
                                    $"{c.Reference} {c.ListedName}",
                                    c.Reason
                                })
                                .ToList(),
                            ""));
                    }

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "08", "Outstanding matters and limitations", "What this review does not settle"));

                    foreach (var c in unsettled)
                    {
                        column.Item().Element(e => DocumentStyle.Callout(
                            e,
                            $"Unable to determine: {(c.Category == SanctionsConcernCategory.PossibleMatch ? $"{c.SubjectType} {c.SubjectName} and {c.Reference} {c.ListedName}" : c.Reason)}. " +
                            $"{c.Resolution} Escalated to {c.EscalatedTo ?? "nobody recorded"}."));
                    }

                    foreach (var limitation in limitations)
                        column.Item().Element(e => DocumentStyle.Note(e, limitation));

                    column.Item().Element(DocumentStyle.Divider);

                    column.Item().Element(e => DocumentStyle.Section(
                        e, "09", "Finalised", "Every matter had an outcome before this was made"));

                    column.Item().Element(e => DocumentStyle.DetailTable(e, new (string, string?)[]
                    {
                        ("Finalised by", review.FinalisedByName),
                        ("Finalised", Stamp(review.FinalisedAt)),
                        ("Corrects", review.CorrectsReview == null
                            ? "Nothing; this is an original review"
                            : $"{review.CorrectsReview.ReviewNumber}, whose report (SHA-256 {review.CorrectsReview.ReportSha256}) is kept unchanged"),
                        ("Note", review.Note ?? "None")
                    }));

                    column.Item().Element(e => DocumentStyle.Note(
                        e,
                        "The portal keeps this report exactly as it was made, with its SHA-256 fingerprint. " +
                        "To confirm a copy is genuine and unaltered, upload it under Sanctions Screening → " +
                        "Verify a report: the check is made against the fingerprint the portal holds, not " +
                        "against anything printed here."));
                });
        }

        /// <summary>The limits of what a review checked, said plainly.</summary>
        public static List<string> Limitations(SanctionsReview review, bool isNotice)
        {
            var limits = new List<string>
            {
                $"Screening covers only the names in the document{(review.PreviousDocument == null ? "" : "s")} " +
                $"listed in section 02, and only the staff, vendors and clients held on " +
                $"{review.CreatedAt.ToLocalTime():d MMMM yyyy}. It is not a finding that anybody has no " +
                "sanctions exposure beyond that.",
                "A match is decision support, not proof: every exact and potential match was decided by " +
                "the compliance officer named in section 09."
            };

            if (isNotice)
            {
                limits.Add(
                    "The new document is a notice, not the whole list. People were checked against the names " +
                    "it gives, not against the full list.");
            }

            if (review.Inconclusive)
            {
                limits.Add(review.NotComparedCount > 0
                    ? $"The comparison is incomplete: {review.NotComparedCount} names could not be compared with anything."
                    : "Part of the comparison could not be worked out; see the discrepancies in section 04.");
            }

            if (review.ClientListId == null)
                limits.Add("No client list had been imported, so no clients were checked.");

            if (!string.IsNullOrWhiteSpace(review.NewDocument.ReadingWarnings))
                limits.Add("The portal noted problems reading the new document; see section 04.");

            return limits;
        }

        private static string Notes(SanctionsConcern concern) =>
            concern.Notes.Count(n => !n.Text.StartsWith("Outcome:", StringComparison.Ordinal)) is var n and > 0
                ? $"\n{n} note{(n == 1 ? "" : "s")}" +
                  (concern.Notes.Any(x => x.FileName != null)
                      ? $", with {string.Join(", ", concern.Notes.Where(x => x.FileName != null).Select(x => x.FileName))}"
                      : "")
                : "";

        private static string Outcome(SanctionsConcern concern) =>
            concern.Status == SanctionsConcernStatus.Open
                ? "Open"
                : $"{SanctionsText.Outcome(concern)}: {concern.Resolution}" +
                  (concern.EscalatedTo == null ? "" : $" Escalated to {concern.EscalatedTo}.") +
                  $"\n{concern.ResolvedByName}, {DocumentStyle.Stamp(concern.ResolvedAt)}";
    }
}
