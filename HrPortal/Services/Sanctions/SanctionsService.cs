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

        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _files;
        private readonly CurrentUserService _currentUser;
        private readonly AuditService _audit;
        private readonly SanctionsReportPdfService _reports;

        public SanctionsService(
            ApplicationDbContext context,
            FileStorageService files,
            CurrentUserService currentUser,
            AuditService audit,
            SanctionsReportPdfService reports)
        {
            _context = context;
            _files = files;
            _currentUser = currentUser;
            _audit = audit;
            _reports = reports;
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
                AuditService.Created,
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
        public async Task<SanctionsReview> CompareAsync(int newDocumentId, int? previousDocumentId)
        {
            var chosen = previousDocumentId != null;

            previousDocumentId ??= (await PreviousApplicableAsync(newDocumentId))?.Id;

            var since = DateTime.UtcNow.AddMinutes(-30);

            var recent = await _context.SanctionsReviews
                .Include(r => r.Changes)
                .Include(r => r.Concerns)
                .Where(r => r.NewDocumentId == newDocumentId &&
                            r.PreviousDocumentId == previousDocumentId &&
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
        /// Checks every member of staff, every vendor and its contact,
        /// and every client on the latest client list against every
        /// name in the new document. A person cleared or confirmed in
        /// an earlier review keeps that decision while neither they nor
        /// the listed entry has changed.
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
                .Select(e => (Entry: e, Target: new ScreeningTarget(e.Reference, e.Name, Lines(e.Aliases), Lines(e.Identifiers))))
                .ToList();

            var subjects = new List<ScreeningSubject>();

            var staff = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.NationalId })
                .ToListAsync();

            subjects.AddRange(staff.Select(s => new ScreeningSubject("Staff", s.Id, s.Name, s.NationalId)));

            var vendors = await _context.Vendors
                .AsNoTracking()
                .Select(v => new { v.Id, v.Name, v.ContactPerson })
                .ToListAsync();

            subjects.AddRange(vendors.Select(v => new ScreeningSubject("Vendor", v.Id, v.Name, null)));
            subjects.AddRange(vendors
                .Where(v => !string.IsNullOrWhiteSpace(v.ContactPerson))
                .Select(v => new ScreeningSubject("Vendor contact", v.Id, v.ContactPerson!, null)));

            var clientList = await _context.SanctionsClientLists
                .AsNoTracking()
                .OrderByDescending(c => c.UploadedAt)
                .FirstOrDefaultAsync();

            var clients = clientList == null
                ? new List<SanctionsClient>()
                : await _context.SanctionsClients
                    .AsNoTracking()
                    .Where(c => c.SanctionsClientListId == clientList.Id)
                    .ToListAsync();

            subjects.AddRange(clients.Select(c => new ScreeningSubject("Client", c.Id, c.Name, c.IdNumber)));

            var decisions = (await _context.SanctionsConcerns
                    .AsNoTracking()
                    .Where(c =>
                        c.SanctionsReviewId != review.Id &&
                        c.Category == SanctionsConcernCategory.PossibleMatch &&
                        c.ResolvedByName != CarriedForwardBy &&
                        (c.Status == SanctionsConcernStatus.Cleared ||
                         c.Status == SanctionsConcernStatus.Confirmed))
                    .Select(c => new
                    {
                        c.Review.ReviewNumber,
                        c.SubjectType,
                        c.SubjectId,
                        c.SubjectName,
                        c.Reference,
                        c.EntryFingerprint,
                        c.Status,
                        c.Resolution,
                        c.ResolvedByName,
                        c.ResolvedAt
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
                foreach (var (entry, target) in targets)
                {
                    var hit = SanctionsMatcher.Match(subject, target);

                    if (hit == null)
                        continue;

                    var key = $"{subject.Type}|{subject.Id}|{target.Reference}";

                    if (!seen.Add(key))
                        continue;

                    var concern = new SanctionsConcern
                    {
                        Category = SanctionsConcernCategory.PossibleMatch,
                        Status = SanctionsConcernStatus.Open,
                        Reference = target.Reference,
                        ListedName = target.Name,
                        EntryFingerprint = entry.Fingerprint,
                        SubjectType = subject.Type,
                        SubjectId = subject.Id,
                        SubjectName = Cap(subject.Name, 500),
                        Reason = Cap(hit.Reason, 2000)!,
                        Score = hit.Score,
                        RaisedAt = now
                    };

                    if (decisions.TryGetValue(key, out var earlier) &&
                        earlier.SubjectName == concern.SubjectName &&
                        earlier.EntryFingerprint == entry.Fingerprint)
                    {
                        concern.Status = earlier.Status;
                        concern.Resolution = Cap(
                            $"Carried forward from {earlier.ReviewNumber}, where " +
                            $"{earlier.ResolvedByName} recorded: {earlier.Resolution} " +
                            "Neither the listed entry nor this person's name has changed since.",
                            4000);
                        concern.ResolvedAt = now;
                        concern.ResolvedByName = CarriedForwardBy;
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

            return review;
        }

        public async Task ResolveAsync(int concernId, SanctionsConcernStatus outcome, string resolution)
        {
            var concern = await _context.SanctionsConcerns
                .Include(c => c.Review)
                .FirstAsync(c => c.Id == concernId);

            if (concern.Review.FinalisedAt != null)
                throw new InvalidOperationException("This review has been finalised.");

            concern.Status = outcome;
            concern.Resolution = resolution.Trim();
            concern.ResolvedAt = DateTime.UtcNow;
            concern.ResolvedByName = await WhoAsync();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(SanctionsConcern),
                concern.Id.ToString(),
                $"Matter on {concern.Review.ReviewNumber} marked {outcome}: {concern.Resolution}");
        }

        /// <summary>
        /// Closes the review. The report is made once, here, and kept
        /// exactly as made with its SHA-256, so a copy shown later can
        /// be checked against it byte for byte.
        /// </summary>
        public async Task FinaliseAsync(int reviewId, string? note)
        {
            var review = await LoadAsync(reviewId);

            if (review.FinalisedAt != null)
                throw new InvalidOperationException("This review has already been finalised.");

            if (!review.Screened)
                throw new InvalidOperationException("Staff, vendors and clients have not been checked yet.");

            if (review.Concerns.Any(c => c.Status == SanctionsConcernStatus.Open))
                throw new InvalidOperationException("Every matter must be resolved before finalising.");

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
                AuditService.Updated,
                nameof(SanctionsReview),
                review.ReviewNumber,
                $"Sanctions review finalised. Report SHA-256 {review.ReportSha256}.");
        }

        public Task<SanctionsReview> LoadAsync(int reviewId) =>
            _context.SanctionsReviews
                .Include(r => r.NewDocument)
                .Include(r => r.PreviousDocument)
                .Include(r => r.ClientList)
                .Include(r => r.Changes)
                .Include(r => r.Concerns)
                .FirstAsync(r => r.Id == reviewId);

        public async Task<SanctionsClientList> UploadClientsAsync(IFormFile file)
        {
            byte[] bytes;

            using (var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            List<ClientRow> rows;

            using (var stream = new MemoryStream(bytes))
                rows = ClientListReader.Read(stream, file.FileName);

            var (_, filePath) = await _files.SaveAsync(file, ClientFolder);

            var list = new SanctionsClientList
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                FilePath = filePath,
                Sha256 = Sha256Of(bytes),
                UploadedAt = DateTime.UtcNow,
                UploadedByName = await WhoAsync()
            };

            foreach (var row in rows)
            {
                list.Clients.Add(new SanctionsClient
                {
                    Name = Cap(row.Name, 500)!,
                    IdNumber = Cap(row.IdNumber, 100),
                    ClientReference = Cap(row.Reference, 100)
                });
            }

            _context.SanctionsClientLists.Add(list);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(SanctionsClientList),
                list.Id.ToString(),
                $"Client list {list.OriginalFileName} uploaded with {rows.Count} client(s).");

            return list;
        }

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
