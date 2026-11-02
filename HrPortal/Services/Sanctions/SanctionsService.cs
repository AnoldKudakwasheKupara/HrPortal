using System.Security.Cryptography;
using System.Text;
using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Sanctions
{
    /// <summary>
    /// Sanctions screening in three steps the compliance officer can
    /// watch happen: read the new document, compare it name by name
    /// with the one before it, then check staff, vendors and clients
    /// against it. Every document is kept, and the review is finalised
    /// with a report that can be checked afterwards.
    /// </summary>
    public class SanctionsService
    {
        public const string DocumentFolder = "sanctions";

        public const string ClientFolder = "sanctions-clients";

        public const string ReportFolder = "sanctions-reports";

        /// <summary>Who a decision carried forward is recorded as made by.</summary>
        public const string CarriedForwardBy = "Portal, from the earlier decision";

        /// <summary>Who a similar name ruled out by its date of birth is recorded as set aside by.</summary>
        public const string SetAsideBy = "Portal, set aside automatically";

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _files;
        private readonly CurrentUserService _currentUser;
        private readonly AuditService _audit;
        private readonly SanctionsReportPdfService _reports;
        private readonly int _threshold;

        public SanctionsService(
            ApplicationDbContext context,
            FileStorageService files,
            CurrentUserService currentUser,
            AuditService audit,
            SanctionsReportPdfService reports,
            IConfiguration configuration)
        {
            _context = context;
            _files = files;
            _currentUser = currentUser;
            _audit = audit;
            _reports = reports;
            _threshold = Math.Clamp(
                configuration.GetValue("Sanctions:NameMatchThreshold", SanctionsMatcher.DefaultThreshold),
                60,
                99);
        }

        public static string Sha256Of(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private async Task<string?> WhoAsync() => await _currentUser.GetDisplayNameAsync();

        /// <summary>
        /// Keeps the document exactly as received and reads every
        /// entry. The same file uploaded again is not kept twice: the
        /// copy already kept is used, so a retry never duplicates it.
        /// </summary>
        public async Task<(SanctionsDocument Document, bool AlreadyUploaded)> UploadAsync(IFormFile file)
        {
            byte[] bytes;

            using (var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            var sha256 = Sha256Of(bytes);

            var existing = await _context.SanctionsDocuments
                .Include(d => d.Entries)
                .Where(d => d.Sha256 == sha256)
                .OrderBy(d => d.Id)
                .FirstOrDefaultAsync();

            if (existing != null)
                return (existing, true);

            ParsedSanctionsList parsed;

            using (var stream = new MemoryStream(bytes))
                parsed = SanctionsListParser.ParsePdf(stream);

            var (_, filePath) = await _files.SaveAsync(file, DocumentFolder);

            var document = new SanctionsDocument
            {
                Title = Cap(parsed.Title, 500),
                DocumentReference = Cap(parsed.Reference, 50),
                IssuedOn = parsed.IssuedOn,
                Kind = parsed.Kind,
                ListName = Cap(parsed.ListName, 200),
                Source = Cap(parsed.Source, 100),
                PageCount = parsed.PageCount,
                EntryCount = parsed.Entries.Count,
                FileSize = bytes.LongLength,
                OriginalFileName = Path.GetFileName(file.FileName),
                FilePath = filePath,
                Sha256 = sha256,
                UploadedAt = DateTime.UtcNow,
                UploadedByName = await WhoAsync(),
                LooksLikeNotice = parsed.LooksLikeNotice,
                ReadingWarnings = parsed.Warnings.Count == 0
                    ? null
                    : Cap(string.Join("\n", parsed.Warnings), 4000)
            };

            foreach (var entry in parsed.Entries)
                document.Entries.Add(ToEntry(entry));

            _context.SanctionsDocuments.Add(document);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                _files.Delete(filePath);
                throw;
            }

            await _audit.LogAsync(
                AuditService.SanctionsUploaded,
                nameof(SanctionsDocument),
                document.Id.ToString(),
                $"Sanctions document {document.OriginalFileName} uploaded " +
                $"({document.Entries.Count} entries, SHA-256 {document.Sha256}).");

            return (document, false);
        }

        /// <summary>
        /// The document a new one is compared with when the officer
        /// asks for "the last uploaded". For a full list, the last full
        /// list of the same list before it: a notice cannot stand in for
        /// a whole list. For a notice, the last document before it that
        /// gives any of the names the notice changes.
        /// </summary>
        public async Task<SanctionsDocument?> PreviousApplicableAsync(int documentId)
        {
            var current = await _context.SanctionsDocuments
                .AsNoTracking()
                .Include(d => d.Entries)
                .FirstAsync(d => d.Id == documentId);

            if (current.Kind == SanctionsDocumentKind.FullList)
            {
                return await _context.SanctionsDocuments
                    .AsNoTracking()
                    .Where(d => d.Id < documentId &&
                                d.Kind == SanctionsDocumentKind.FullList &&
                                d.ListName == current.ListName)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();
            }

            var references = current.Entries.Select(e => e.Reference).Distinct().ToList();

            return await _context.SanctionsDocuments
                .AsNoTracking()
                .Where(d => d.Id < documentId && d.Entries.Any(e => references.Contains(e.Reference)))
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Compares a new document with an earlier one and records what
        /// was added, removed and amended, what is unchanged, and
        /// anything that does not add up. Without a previous document
        /// chosen, the last applicable one is used, and a notice's
        /// entries are each compared with their latest earlier version.
        ///
        /// Asking again for the same comparison within half an hour,
        /// as a retry does, returns the review already made rather than
        /// a second one.
        /// </summary>
        public Task<SanctionsReview> CompareAsync(int newDocumentId, int? previousDocumentId) =>
            CompareAsync(newDocumentId, previousDocumentId, previousDocumentId != null, null);

        private async Task<SanctionsReview> CompareAsync(
            int newDocumentId,
            int? previousDocumentId,
            bool chosen,
            int? corrects)
        {

            previousDocumentId ??= (await PreviousApplicableAsync(newDocumentId))?.Id;

            var since = DateTime.UtcNow.AddMinutes(-30);

            var recent = await _context.SanctionsReviews
                .Include(r => r.Changes)
                .Include(r => r.Concerns)
                .Where(r => r.NewDocumentId == newDocumentId &&
                            r.PreviousDocumentId == previousDocumentId &&
                            r.PreviousChosen == chosen &&
                            r.CorrectsReviewId == corrects &&
                            r.FinalisedAt == null &&
                            r.CreatedAt > since)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            if (recent != null)
                return recent;

            var current = await _context.SanctionsDocuments
                .Include(d => d.Entries)
                .FirstAsync(d => d.Id == newDocumentId);

            var previous = previousDocumentId == null
                ? null
                : await _context.SanctionsDocuments
                    .Include(d => d.Entries)
                    .FirstAsync(d => d.Id == previousDocumentId);

            var references = current.Entries.Select(e => e.Reference).Distinct().ToList();

            var earlier = current.Kind == SanctionsDocumentKind.FullList
                ? new Dictionary<string, EntryVersion>()
                : (await _context.SanctionsEntries
                        .AsNoTracking()
                        .Include(e => e.Document)
                        .Where(e => references.Contains(e.Reference) &&
                                    e.SanctionsDocumentId != current.Id &&
                                    (chosen
                                        ? e.SanctionsDocumentId == previousDocumentId
                                        : e.SanctionsDocumentId < current.Id))
                        .ToListAsync())
                    .GroupBy(e => e.Reference)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(e => e.SanctionsDocumentId).First() is var e
                            ? new EntryVersion(e, e.Document.Label)
                            : null!);

            var comparison = SanctionsComparer.Compare(
                current,
                current.Entries.ToList(),
                previous,
                previous?.Entries.ToList() ?? new List<SanctionsEntry>(),
                reference => earlier.GetValueOrDefault(reference));

            var review = new SanctionsReview
            {
                ReviewNumber = await NextReviewNumberAsync(),
                NewDocumentId = current.Id,
                PreviousDocumentId = previous?.Id,
                PreviousChosen = chosen,
                CorrectsReviewId = corrects,
                CreatedAt = DateTime.UtcNow,
                CreatedByName = await WhoAsync(),
                UnchangedCount = comparison.Unchanged,
                NotComparedCount = comparison.NotCompared,
                Inconclusive = comparison.Inconclusive,
                ComparisonNote = comparison.Notes.Count == 0 ? null : Cap(string.Join("\n", comparison.Notes), 4000)
            };

            var now = DateTime.UtcNow;

            void Discrepancy(string reason) =>
                review.Concerns.Add(new SanctionsConcern
                {
                    Category = SanctionsConcernCategory.Discrepancy,
                    Status = SanctionsConcernStatus.Open,
                    Reason = Cap(reason, 2000)!,
                    RaisedAt = now
                });

            foreach (var warning in Lines(current.ReadingWarnings))
                Discrepancy($"Reading {current.Label}: {warning}");

            if (previous != null && previous.Id != current.Id)
            {
                foreach (var warning in Lines(previous.ReadingWarnings))
                    Discrepancy($"Reading {previous.Label}: {warning}");
            }

            foreach (var discrepancy in comparison.Discrepancies)
                Discrepancy(discrepancy);

            foreach (var change in comparison.Changes)
            {
                review.Changes.Add(new SanctionsChange
                {
                    Type = change.Type,
                    Reference = change.Entry.Reference,
                    Name = Cap(change.Entry.Name, 500)!,
                    Differences = change.Differences.Count == 0
                        ? null
                        : string.Join("\n", change.Differences.Select(d =>
                            $"{d.Field}: {d.Before ?? "none"} → {d.After ?? "none"}")),
                    Note = Cap(change.Note, 1000),
                    Page = change.Entry.Page
                });
            }

            _context.SanctionsReviews.Add(review);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.SanctionsCompared,
                nameof(SanctionsReview),
                review.ReviewNumber,
                previous == null && current.Kind == SanctionsDocumentKind.FullList
                    ? $"{current.Label} recorded as the first document of its list."
                    : $"{current.Label} compared{(previous == null ? "" : $" with {previous.Label}")}: " +
                      $"{review.Changes.Count(c => c.Type == SanctionsChangeType.Added)} added, " +
                      $"{review.Changes.Count(c => c.Type == SanctionsChangeType.Removed)} removed, " +
                      $"{review.Changes.Count(c => c.Type == SanctionsChangeType.Changed)} amended, " +
                      $"{review.UnchangedCount} unchanged{(review.Inconclusive ? "; inconclusive" : "")}.");

            return review;
        }
        private async Task<string> NextReviewNumberAsync()
        {
            var prefix = $"SAN-{DateTime.UtcNow.Year}-";

            var last = await _context.SanctionsReviews
                .Where(r => r.ReviewNumber.StartsWith(prefix))
                .OrderByDescending(r => r.ReviewNumber)
                .Select(r => r.ReviewNumber)
                .FirstOrDefaultAsync();

            var next = last != null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;

            return $"{prefix}{next:D4}";
        }

        /// <summary>
        /// Checks every active member of staff, every active vendor and
        /// its contact, and every client on the latest client list
        /// against every name on the new document.
        ///
        /// A possible match found and cleared as a false positive in an
        /// earlier review keeps that decision only while nothing has
        /// changed: the listed entry reads the same and the person's
        /// name, ID numbers, date of birth and nationality are the same.
        /// A confirmed match is never carried forward: it is raised
        /// again, with the earlier decision noted, so it is looked at
        /// each time. Similar names the date of birth rules out are
        /// recorded as set aside, so they can be seen and reopened.
        /// </summary>
        public async Task<SanctionsReview> ScreenAsync(int reviewId)
        {
            var review = await _context.SanctionsReviews
                .Include(r => r.Concerns)
                .FirstAsync(r => r.Id == reviewId);

            var entries = await _context.SanctionsEntries
                .AsNoTracking()
                .Where(e => e.SanctionsDocumentId == review.NewDocumentId)
                .ToListAsync();

            var targets = entries
                .GroupBy(e => e.Reference)
                .Select(g => g.First())
                .Select(e => (Entry: e, Target: new ScreeningTarget(
                    e.Reference,
                    e.Name,
                    Lines(e.Aliases),
                    Lines(e.Identifiers),
                    e.IsEntity,
                    e.DateOfBirth,
                    e.Nationality)))
                .ToList();

            var subjects = new List<ScreeningSubject>();

            var staff = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.NationalId, e.DateOfBirth })
                .ToListAsync();

            subjects.AddRange(staff.Select(s => new ScreeningSubject(
                Staff, s.Id, s.Name, s.NationalId, s.DateOfBirth, IsOrganisation: false)));

            var vendors = await _context.Vendors
                .AsNoTracking()
                .Where(v => v.IsActive)
                .Select(v => new { v.Id, v.Name, v.ContactPerson })
                .ToListAsync();

            subjects.AddRange(vendors.Select(v => new ScreeningSubject(Vendor, v.Id, v.Name, null)));
            subjects.AddRange(vendors
                .Where(v => !string.IsNullOrWhiteSpace(v.ContactPerson))
                .Select(v => new ScreeningSubject(VendorContact, v.Id, v.ContactPerson!, null, IsOrganisation: false)));

            var clientList = await CurrentClientListAsync();

            var clients = clientList == null
                ? new List<SanctionsClient>()
                : await _context.SanctionsClients
                    .AsNoTracking()
                    .Where(c => c.SanctionsClientListId == clientList.Id)
                    .ToListAsync();

            subjects.AddRange(clients.Select(ClientSubject));

            var decisions = (await _context.SanctionsConcerns
                    .AsNoTracking()
                    .Where(c =>
                        c.SanctionsReviewId != review.Id &&
                        c.Category == SanctionsConcernCategory.PossibleMatch &&
                        c.ResolvedByName != CarriedForwardBy &&
                        c.ResolvedByName != SetAsideBy &&
                        (c.Status == SanctionsConcernStatus.Cleared ||
                         c.Status == SanctionsConcernStatus.Confirmed ||
                         c.Status == SanctionsConcernStatus.UnableToDetermine))
                    .Select(c => new
                    {
                        c.Review.ReviewNumber,
                        c.SubjectType,
                        c.SubjectId,
                        c.SubjectFingerprint,
                        c.Reference,
                        c.EntryFingerprint,
                        c.Status,
                        c.Resolution,
                        c.ResolvedByName,
                        c.ResolvedAt,
                        c.EscalatedTo
                    })
                    .ToListAsync())
                .GroupBy(c => $"{c.SubjectType}|{c.SubjectId}|{c.Reference}")
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ResolvedAt).First());

            var seen = review.Concerns
                .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                .Select(c => $"{c.SubjectType}|{c.SubjectId}|{c.Reference}")
                .ToHashSet();

            var now = DateTime.UtcNow;

            foreach (var subject in subjects)
            {
                var subjectFingerprint = FingerprintOf(subject);

                foreach (var (entry, target) in targets)
                {
                    var hit = SanctionsMatcher.Match(subject, target, _threshold);

                    if (hit == null)
                        continue;

                    var key = $"{subject.Type}|{subject.Id}|{target.Reference}";

                    if (!seen.Add(key))
                        continue;

                    var concern = new SanctionsConcern
                    {
                        Category = SanctionsConcernCategory.PossibleMatch,
                        Status = SanctionsConcernStatus.Open,
                        Strength = hit.Strength,
                        Reference = target.Reference,
                        ListedName = Cap(target.Name, 500),
                        EntryFingerprint = entry.Fingerprint,
                        SubjectType = subject.Type,
                        SubjectId = subject.Id,
                        SubjectName = Cap(subject.Name, 500),
                        SubjectFingerprint = subjectFingerprint,
                        Reason = Cap(hit.Reason, 2000)!,
                        Evidence = Cap(string.Join("\n", hit.Evidence), 2000),
                        Score = hit.Score,
                        RaisedAt = now
                    };

                    decisions.TryGetValue(key, out var earlier);

                    if (hit.Strength == SanctionsMatchStrength.SetAside)
                    {
                        concern.Status = SanctionsConcernStatus.Cleared;
                        concern.Resolution = Cap(hit.Reason, 4000);
                        concern.ResolvedAt = now;
                        concern.ResolvedByName = SetAsideBy;
                    }
                    else if (earlier != null &&
                             EarlierDecision(earlier.Status, earlier.SubjectFingerprint, earlier.EntryFingerprint,
                                 subjectFingerprint, entry.Fingerprint) == CarryForward.KeepCleared)
                    {
                        concern.Status = SanctionsConcernStatus.Cleared;
                        concern.Resolution = Cap(
                            $"Carried forward from {earlier.ReviewNumber}, where " +
                            $"{earlier.ResolvedByName} found it a false positive: {earlier.Resolution} " +
                            "Neither the listed entry nor this person's details have changed since.",
                            4000);
                        concern.ResolvedAt = now;
                        concern.ResolvedByName = CarriedForwardBy;
                    }
                    else if (earlier != null)
                    {
                        concern.Evidence = Cap(
                            concern.Evidence + "\n" +
                            (earlier.Status switch
                            {
                                SanctionsConcernStatus.Confirmed =>
                                    $"Earlier decision: confirmed as a match in {earlier.ReviewNumber} by " +
                                    $"{earlier.ResolvedByName}. Check what has been done about it.",
                                SanctionsConcernStatus.UnableToDetermine =>
                                    $"Earlier decision: could not be determined in {earlier.ReviewNumber} and was " +
                                    $"escalated to {earlier.EscalatedTo ?? "nobody recorded"}. Check whether it has been settled.",
                                _ =>
                                    $"Earlier decision: cleared in {earlier.ReviewNumber}, but the listed " +
                                    "entry or this person's details have changed since, so it is open again."
                            }),
                            2000);
                    }

                    review.Concerns.Add(concern);
                }
            }

            review.Screened = true;
            review.ScreenedStaff = staff.Count;
            review.ScreenedVendors = vendors.Count;
            review.ScreenedClients = clients.Count;
            review.ClientListId = clientList?.Id;

            await _context.SaveChangesAsync();

            var raised = review.Concerns.Count(c =>
                c.Category == SanctionsConcernCategory.PossibleMatch &&
                c.Strength != SanctionsMatchStrength.SetAside);

            await _audit.LogAsync(
                AuditService.SanctionsScreened,
                nameof(SanctionsReview),
                review.ReviewNumber,
                $"{staff.Count} staff, {vendors.Count} vendors and {clients.Count} clients checked against " +
                $"{targets.Count} listed names: {raised} possible match(es).");

            return review;
        }

        public enum CarryForward
        {
            /// <summary>Cleared before and nothing has changed: the decision stands.</summary>
            KeepCleared,

            /// <summary>Cleared before, but the person or the entry has changed: open again.</summary>
            Reopen,

            /// <summary>Confirmed before: raised again every time.</summary>
            ConfirmedBefore
        }

        /// <summary>
        /// What an earlier decision about the same person and listed
        /// entry means now. Only a false positive is ever carried
        /// forward, and only while both fingerprints are unchanged; a
        /// decision recorded before fingerprints were kept is not.
        /// </summary>
        public static CarryForward EarlierDecision(
            SanctionsConcernStatus status,
            string? earlierSubject,
            string? earlierEntry,
            string subject,
            string entry) =>
            status == SanctionsConcernStatus.Confirmed
                ? CarryForward.ConfirmedBefore
                : status == SanctionsConcernStatus.Cleared &&
                  earlierSubject != null && earlierSubject == subject &&
                  earlierEntry != null && earlierEntry == entry
                    ? CarryForward.KeepCleared
                    : CarryForward.Reopen;

        public const string Staff = "Staff";
        public const string Vendor = "Vendor";
        public const string VendorContact = "Vendor contact";
        public const string Client = "Client";

        public static ScreeningSubject ClientSubject(SanctionsClient c) =>
            new(
                Client,
                c.Id,
                c.Name,
                c.IdNumber,
                c.DateOfBirth,
                c.Nationality,
                new[] { c.PassportNumber, c.RegistrationNumber }.Where(i => i != null).Select(i => i!).ToList(),
                c.IsCompany);

        /// <summary>What the client list gives about a client, for comparing side by side.</summary>
        public static List<(string Label, string Value)> ClientDetails(SanctionsClient c) =>
            new()
            {
                (c.IsCompany == true ? "Company" : "Client", c.Name),
                ("Client reference", c.ClientReference ?? "Not given"),
                ("ID number", c.IdNumber ?? "Not given"),
                ("Passport number", c.PassportNumber ?? "Not given"),
                ("Registration number", c.RegistrationNumber ?? "Not given"),
                ("Date of birth", c.DateOfBirth?.ToString("d MMM yyyy") ?? "Not given"),
                ("Nationality", c.Nationality ?? "Not given"),
                ("Row of the client list", c.RowNumber == 0 ? "Not recorded" : c.RowNumber.ToString())
            };

        /// <summary>
        /// The client list screened: the latest one imported. It keeps
        /// being used for every review until another is imported, so
        /// an unchanged list never has to be uploaded again.
        /// </summary>
        public Task<SanctionsClientList?> CurrentClientListAsync() =>
            _context.SanctionsClientLists
                .AsNoTracking()
                .Where(c => c.Status == SanctionsClientListStatus.Imported)
                .OrderByDescending(c => c.UploadedAt)
                .FirstOrDefaultAsync();

        /// <summary>
        /// A hash of what identifies a person for screening, so an
        /// earlier decision about them is only kept while it is the
        /// same.
        /// </summary>
        public static string FingerprintOf(ScreeningSubject subject) =>
            Sha256Of(Encoding.UTF8.GetBytes(string.Join(
                "|",
                SanctionsListParser.Normalise(subject.Name),
                string.Join(";", new[] { subject.IdNumber }
                    .Concat(subject.OtherIds ?? Array.Empty<string>())
                    .Select(SanctionsListParser.Normalise)
                    .Where(i => i.Length > 0)
                    .OrderBy(i => i, StringComparer.Ordinal)),
                subject.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                SanctionsListParser.Normalise(subject.Nationality))));

        public const string EvidenceFolder = "sanctions-evidence";

        /// <summary>
        /// Whether an outcome can be recorded for a kind of matter, and
        /// if not, why. A confirmed match, and anything that could not
        /// be determined, must say who it was escalated to.
        /// </summary>
        public static string? CheckOutcome(
            SanctionsConcernCategory category,
            SanctionsConcernStatus outcome,
            string? resolution,
            string? escalatedTo)
        {
            if (string.IsNullOrWhiteSpace(resolution))
                return "Write down what you found.";

            var allowed = category == SanctionsConcernCategory.PossibleMatch
                ? new[]
                {
                    SanctionsConcernStatus.Cleared, SanctionsConcernStatus.Confirmed,
                    SanctionsConcernStatus.FurtherInvestigation, SanctionsConcernStatus.UnableToDetermine
                }
                : new[]
                {
                    SanctionsConcernStatus.Resolved,
                    SanctionsConcernStatus.FurtherInvestigation, SanctionsConcernStatus.UnableToDetermine
                };

            if (!allowed.Contains(outcome))
                return "Choose one of the outcomes offered.";

            if (outcome is SanctionsConcernStatus.Confirmed or SanctionsConcernStatus.UnableToDetermine &&
                string.IsNullOrWhiteSpace(escalatedTo))
            {
                return $"Say who the matter was escalated to: a {SanctionsText.Status(outcome).ToLowerInvariant()} must be escalated.";
            }

            return null;
        }

        /// <summary>
        /// Records the outcome of a matter. Each outcome is also kept as
        /// a note, so a matter first marked for further investigation
        /// and later cleared shows both steps.
        /// </summary>
        public async Task ResolveAsync(
            int concernId,
            SanctionsConcernStatus outcome,
            string resolution,
            string? escalatedTo)
        {
            var concern = await _context.SanctionsConcerns
                .Include(c => c.Review)
                .FirstAsync(c => c.Id == concernId);

            if (concern.Review.FinalisedAt != null)
                throw new InvalidOperationException("This review has been finalised.");

            if (CheckOutcome(concern.Category, outcome, resolution, escalatedTo) is { } problem)
                throw new InvalidOperationException(problem);

            var who = await WhoAsync();
            var now = DateTime.UtcNow;

            concern.Status = outcome;
            concern.Resolution = Cap(resolution.Trim(), 4000);
            concern.ResolvedAt = now;
            concern.ResolvedByName = who;

            if (!string.IsNullOrWhiteSpace(escalatedTo))
            {
                concern.EscalatedTo = Cap(escalatedTo.Trim(), 200);
                concern.EscalatedAt ??= now;
            }

            _context.SanctionsConcernNotes.Add(new SanctionsConcernNote
            {
                SanctionsConcernId = concern.Id,
                Text = Cap(
                    $"Outcome: {SanctionsText.Status(outcome)}. {concern.Resolution}" +
                    (concern.EscalatedTo == null ? "" : $" Escalated to {concern.EscalatedTo}."),
                    4000)!,
                CreatedAt = now,
                CreatedByName = who
            });

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.SanctionsMatterRecorded,
                nameof(SanctionsConcern),
                concern.Id.ToString(),
                $"Matter on {concern.Review.ReviewNumber} recorded as {SanctionsText.Status(outcome)}: {concern.Resolution}" +
                (concern.EscalatedTo == null ? "" : $" Escalated to {concern.EscalatedTo}."));
        }

        /// <summary>A comment on a matter, with a supporting file if one is given.</summary>
        public async Task AddNoteAsync(int concernId, string text, IFormFile? file)
        {
            var concern = await _context.SanctionsConcerns
                .Include(c => c.Review)
                .FirstAsync(c => c.Id == concernId);

            if (concern.Review.FinalisedAt != null)
                throw new InvalidOperationException("This review has been finalised.");

            var note = new SanctionsConcernNote
            {
                SanctionsConcernId = concern.Id,
                Text = Cap(text.Trim(), 4000)!,
                CreatedAt = DateTime.UtcNow,
                CreatedByName = await WhoAsync()
            };

            if (file != null && file.Length > 0)
            {
                if (FileStorageService.Validate(file) is { } problem)
                    throw new InvalidOperationException(problem);

                using var memory = new MemoryStream();
                await file.CopyToAsync(memory);

                var (_, path) = await _files.SaveAsync(file, EvidenceFolder);

                note.FileName = Cap(Path.GetFileName(file.FileName), 255);
                note.FilePath = path;
                note.Sha256 = Sha256Of(memory.ToArray());
            }

            _context.SanctionsConcernNotes.Add(note);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                if (note.FilePath != null)
                    _files.Delete(note.FilePath);
                throw;
            }

            await _audit.LogAsync(
                AuditService.SanctionsNoteAdded,
                nameof(SanctionsConcern),
                concern.Id.ToString(),
                $"Note on a matter in {concern.Review.ReviewNumber}" +
                (note.FileName == null ? "." : $", with {note.FileName} (SHA-256 {note.Sha256})."));
        }

        /// <summary>
        /// Opens a matter the portal closed by itself, a similar name
        /// set aside or a decision carried forward, so the officer can
        /// look into it after all.
        /// </summary>
        public async Task ReopenAsync(int concernId)
        {
            var concern = await _context.SanctionsConcerns
                .Include(c => c.Review)
                .FirstAsync(c => c.Id == concernId);

            if (concern.Review.FinalisedAt != null)
                throw new InvalidOperationException("This review has been finalised.");

            if (concern.ResolvedByName is not (SetAsideBy or CarriedForwardBy))
                throw new InvalidOperationException("Only a matter the portal closed by itself can be reopened.");

            var who = await WhoAsync();

            _context.SanctionsConcernNotes.Add(new SanctionsConcernNote
            {
                SanctionsConcernId = concern.Id,
                Text = $"Reopened to be looked into. It had been closed by the portal: {concern.Resolution}",
                CreatedAt = DateTime.UtcNow,
                CreatedByName = who
            });

            concern.Status = SanctionsConcernStatus.Open;
            concern.Resolution = null;
            concern.ResolvedAt = null;
            concern.ResolvedByName = null;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.SanctionsMatterRecorded,
                nameof(SanctionsConcern),
                concern.Id.ToString(),
                $"Matter on {concern.Review.ReviewNumber} reopened.");
        }

        /// <summary>Matters that stop a review being finalised.</summary>
        public static bool IsOutstanding(SanctionsConcern concern) =>
            concern.Status is SanctionsConcernStatus.Open or SanctionsConcernStatus.FurtherInvestigation;

        /// <summary>Why a review cannot be finalised yet, or nothing when it can.</summary>
        public static List<string> FinaliseBlockers(SanctionsReview review)
        {
            var reasons = new List<string>();

            if (review.FinalisedAt != null)
                reasons.Add("It has already been finalised.");

            if (!review.Screened)
                reasons.Add("Staff, vendors and clients have not been checked yet.");

            var open = review.Concerns.Count(c => c.Status == SanctionsConcernStatus.Open);
            var further = review.Concerns.Count(c => c.Status == SanctionsConcernStatus.FurtherInvestigation);

            if (open > 0)
                reasons.Add($"{open} matter{(open == 1 ? " has" : "s have")} no outcome yet.");

            if (further > 0)
                reasons.Add($"{further} matter{(further == 1 ? " is" : "s are")} still under further investigation.");

            return reasons;
        }

        /// <summary>
        /// Closes the review. The report is made once, here, and kept
        /// exactly as made with its SHA-256, so a copy shown later can
        /// be checked against it byte for byte.
        /// </summary>
        public async Task FinaliseAsync(int reviewId, string? note)
        {
            var review = await LoadAsync(reviewId);

            if (FinaliseBlockers(review) is { Count: > 0 } blockers)
                throw new InvalidOperationException("It cannot be finalised yet. " + string.Join(" ", blockers));

            review.FinalisedAt = DateTime.UtcNow;
            review.FinalisedByName = await WhoAsync();
            review.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            var pdf = _reports.Render(review);

            var folder = Path.Combine(_files.UploadRoot, ReportFolder);
            Directory.CreateDirectory(folder);

            var fileName = $"{review.ReviewNumber}.pdf";
            await File.WriteAllBytesAsync(Path.Combine(folder, fileName), pdf);

            review.ReportPath = $"/uploads/{ReportFolder}/{fileName}";
            review.ReportSha256 = Sha256Of(pdf);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.SanctionsFinalised,
                nameof(SanctionsReview),
                review.ReviewNumber,
                $"Sanctions review finalised and its report made. Report SHA-256 {review.ReportSha256}.");
        }

        /// <summary>
        /// Starts a new review of the same documents to correct a
        /// finalised one. The finalised review and its report are left
        /// exactly as they were; the new one says which it corrects,
        /// and false positives cleared in it carry forward as usual.
        /// </summary>
        public async Task<SanctionsReview> CorrectAsync(int reviewId)
        {
            var original = await _context.SanctionsReviews.AsNoTracking().FirstAsync(r => r.Id == reviewId);

            if (original.FinalisedAt == null)
                throw new InvalidOperationException("Only a finalised review needs correcting; this one can still be changed.");

            var correction = await CompareAsync(
                original.NewDocumentId,
                original.PreviousChosen ? original.PreviousDocumentId : null,
                original.PreviousChosen,
                original.Id);

            if (!correction.Screened)
                await ScreenAsync(correction.Id);

            await _audit.LogAsync(
                AuditService.SanctionsCompared,
                nameof(SanctionsReview),
                correction.ReviewNumber,
                $"{correction.ReviewNumber} started to correct {original.ReviewNumber}.");

            return correction;
        }

        /// <summary>The review that corrects this one, if there is one.</summary>
        public Task<SanctionsReview?> CorrectedByAsync(int reviewId) =>
            _context.SanctionsReviews
                .AsNoTracking()
                .Where(r => r.CorrectsReviewId == reviewId)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

        public Task<SanctionsReview> LoadAsync(int reviewId) =>
            _context.SanctionsReviews
                .Include(r => r.CorrectsReview)
                .Include(r => r.NewDocument)
                .Include(r => r.PreviousDocument)
                .Include(r => r.ClientList)
                .Include(r => r.Changes)
                .Include(r => r.Concerns).ThenInclude(c => c.Notes)
                .FirstAsync(r => r.Id == reviewId);

        /// <summary>
        /// Keeps a client list exactly as uploaded and imports it when
        /// its columns can be recognised. When they cannot, the list
        /// waits for the officer to say which column is which, and the
        /// client list already in use stays in use.
        /// </summary>
        public async Task<SanctionsClientList> UploadClientsAsync(IFormFile file)
        {
            byte[] bytes;

            using (var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            ClientTable table;

            using (var stream = new MemoryStream(bytes))
                table = ClientListReader.ReadTable(stream, file.FileName);

            if (table.Headings.Count == 0)
                throw new InvalidDataException("The file is empty.");

            var (_, filePath) = await _files.SaveAsync(file, ClientFolder);

            var list = new SanctionsClientList
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                FilePath = filePath,
                Sha256 = Sha256Of(bytes),
                FileSize = bytes.LongLength,
                UploadedAt = DateTime.UtcNow,
                UploadedByName = await WhoAsync(),
                Status = SanctionsClientListStatus.AwaitingColumns,
                RowsReceived = table.Rows.Count
            };

            _context.SanctionsClientLists.Add(list);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                _files.Delete(filePath);
                throw;
            }

            var map = ClientListReader.AutoMap(table.Headings);

            if (ClientListReader.HasName(map))
                await ImportClientsAsync(list, table, map);

            return list;
        }

        /// <summary>The kept file of a client list, read again.</summary>
        public async Task<ClientTable> ReadClientTableAsync(SanctionsClientList list)
        {
            var path = _files.ResolveExistingPath(list.FilePath)
                ?? throw new InvalidDataException("The uploaded file can no longer be found.");

            await using var stream = File.OpenRead(path);

            return ClientListReader.ReadTable(stream, list.OriginalFileName);
        }

        /// <summary>Imports a list waiting for its columns, with the columns the officer chose.</summary>
        public async Task<SanctionsClientList> ImportClientsAsync(int listId, IReadOnlyDictionary<ClientField, int> map)
        {
            var list = await _context.SanctionsClientLists.FirstAsync(l => l.Id == listId);

            if (list.Status != SanctionsClientListStatus.AwaitingColumns)
                throw new InvalidOperationException("This client list has already been imported.");

            if (!ClientListReader.HasName(map))
                throw new InvalidOperationException("Choose the column with the client's name: a full name, a company name, or first and last names.");

            await ImportClientsAsync(list, await ReadClientTableAsync(list), map);

            return list;
        }

        private async Task ImportClientsAsync(
            SanctionsClientList list,
            ClientTable table,
            IReadOnlyDictionary<ClientField, int> map)
        {
            var import = ClientListReader.Import(table, map);

            foreach (var row in import.Clients)
            {
                list.Clients.Add(new SanctionsClient
                {
                    RowNumber = row.RowNumber,
                    Name = Cap(row.Name, 500)!,
                    IsCompany = row.IsCompany,
                    IdNumber = row.IdNumber,
                    PassportNumber = row.PassportNumber,
                    RegistrationNumber = row.RegistrationNumber,
                    DateOfBirth = row.DateOfBirth,
                    Nationality = row.Nationality,
                    ClientReference = row.Reference
                });
            }

            list.Status = SanctionsClientListStatus.Imported;
            list.ColumnMap = string.Join("\n", map.OrderBy(m => m.Key).Select(m => $"{m.Key}={m.Value}"));
            list.RowsReceived = import.Received;
            list.RowsImported = import.Clients.Count;
            list.RowsInvalid = import.Invalid;
            list.RowsDuplicated = import.Duplicates;
            list.Problems = import.Problems.Count == 0 ? null : Cap(string.Join("\n", import.Problems), 8000);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.SanctionsClientsImported,
                nameof(SanctionsClientList),
                list.Id.ToString(),
                $"Client list {list.OriginalFileName} imported: {import.Received} rows received, " +
                $"{import.Clients.Count} imported, {import.Invalid} invalid, {import.Duplicates} duplicates " +
                $"(SHA-256 {list.Sha256}).");
        }

        public static Dictionary<ClientField, int> ParseColumnMap(string? stored) =>
            Lines(stored)
                .Select(l => l.Split('='))
                .Where(p => p.Length == 2 && Enum.TryParse<ClientField>(p[0], out _) && int.TryParse(p[1], out _))
                .ToDictionary(p => Enum.Parse<ClientField>(p[0]), p => int.Parse(p[1]));

        private static SanctionsEntry ToEntry(ParsedSanctionsEntry e)
        {
            var entry = new SanctionsEntry
            {
                Reference = e.Reference,
                IsEntity = e.IsEntity,
                Name = Cap(e.Name, 500)!,
                OriginalScriptName = Cap(e.OriginalScriptName, 500),
                Aliases = Joined(e.Aliases),
                Title = e.Title,
                Designation = e.Designation,
                DateOfBirth = e.DateOfBirth,
                PlaceOfBirth = e.PlaceOfBirth,
                Nationality = e.Nationality,
                Identifiers = Joined(e.Identifiers),
                Passports = Joined(e.Passports),
                NationalIds = Joined(e.NationalIds),
                Address = e.Address,
                ListedOn = e.ListedOn,
                OtherInformation = e.OtherInformation,
                Page = e.Page,
                AddedText = Joined(e.AddedText),
                RemovedText = Joined(e.RemovedText),
                Details = e.Details
            };

            entry.Fingerprint = FingerprintOf(entry);

            return entry;
        }

        /// <summary>
        /// A hash of the compared fields, lists in a fixed order, so two
        /// versions that read the same have the same fingerprint.
        /// </summary>
        public static string FingerprintOf(SanctionsEntry entry) =>
            Sha256Of(Encoding.UTF8.GetBytes(string.Join(
                "|",
                SanctionsComparer.Fields(entry).Select(f => f.IsList
                    ? string.Join(";", Lines(f.Value).Select(SanctionsListParser.Normalise).OrderBy(v => v, StringComparer.Ordinal))
                    : SanctionsListParser.Normalise(f.Value)))));

        public static List<string> Lines(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        private static string? Joined(List<string> values) =>
            values.Count == 0 ? null : string.Join("\n", values);

        private static string? Cap(string? value, int length) =>
            value == null || value.Length <= length ? value : value[..length];
    }
}
