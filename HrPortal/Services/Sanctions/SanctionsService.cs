using System.Security.Cryptography;
using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Sanctions
{
    /// <summary>One entry's difference between two documents.</summary>
    public sealed record SanctionsComparisonRow(
        SanctionsChangeType Type,
        string Reference,
        string Name,
        string? Differences);

    /// <summary>
    /// Everything that happens to a sanctions document after it
    /// arrives: keeping it, reading it, applying it to the register,
    /// screening the people the company deals with against the
    /// register, and signing the review off with a report that can be
    /// checked afterwards.
    /// </summary>
    public class SanctionsService
    {
        public const string ListFolder = "sanctions";

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

        private async Task<string?> WhoAsync() =>
            await _currentUser.GetDisplayNameAsync();

        /// <summary>
        /// Keeps the document and reads it. Nothing changes in the
        /// register until the compliance officer has seen what was read
        /// and confirmed what kind of document it is.
        /// </summary>
        public async Task<SanctionsList> UploadAsync(IFormFile file)
        {
            byte[] bytes;

            using (var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            ParsedSanctionsList parsed;

            using (var stream = new MemoryStream(bytes))
                parsed = SanctionsListParser.ParsePdf(stream);

            var (_, filePath) = await _files.SaveAsync(file, ListFolder);

            var list = new SanctionsList
            {
                ReviewNumber = await NextReviewNumberAsync(),
                Title = parsed.Title,
                DocumentReference = parsed.Reference,
                IssuedOn = parsed.IssuedOn,
                Kind = parsed.KindGuess,
                Status = SanctionsReviewStatus.Pending,
                OriginalFileName = Path.GetFileName(file.FileName),
                FilePath = filePath,
                Sha256 = Sha256Of(bytes),
                UploadedAt = DateTime.UtcNow,
                UploadedByName = await WhoAsync()
            };

            foreach (var entry in parsed.Entries)
                list.Entries.Add(ToListEntry(entry));

            foreach (var warning in parsed.Warnings)
            {
                list.Concerns.Add(new SanctionsConcern
                {
                    Category = SanctionsConcernCategory.Discrepancy,
                    Status = SanctionsConcernStatus.Open,
                    Reason = warning,
                    RaisedAt = DateTime.UtcNow
                });
            }

            var sameFile = await _context.SanctionsLists
                .Where(l => l.Sha256 == list.Sha256)
                .Select(l => l.ReviewNumber)
                .FirstOrDefaultAsync();

            if (sameFile != null)
            {
                list.Concerns.Add(new SanctionsConcern
                {
                    Category = SanctionsConcernCategory.Discrepancy,
                    Status = SanctionsConcernStatus.Open,
                    Reason = $"This exact file was already uploaded as {sameFile}.",
                    RaisedAt = DateTime.UtcNow
                });
            }

            _context.SanctionsLists.Add(list);

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
                nameof(SanctionsList),
                list.ReviewNumber,
                $"Sanctions document {list.OriginalFileName} uploaded " +
                $"({list.Entries.Count} entries, SHA-256 {list.Sha256}).");

            return list;
        }

        private async Task<string> NextReviewNumberAsync()
        {
            var prefix = $"SAN-{DateTime.UtcNow.Year}-";

            var last = await _context.SanctionsLists
                .Where(l => l.ReviewNumber.StartsWith(prefix))
                .OrderByDescending(l => l.ReviewNumber)
                .Select(l => l.ReviewNumber)
                .FirstOrDefaultAsync();

            var next = last != null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;

            return $"{prefix}{next:D4}";
        }

        private static SanctionsListEntry ToListEntry(ParsedSanctionsEntry e)
        {
            var entry = new SanctionsListEntry
            {
                Reference = e.Reference,
                IsEntity = e.IsEntity,
                Name = Cap(e.Name, 500),
                Aliases = e.Aliases.Count == 0 ? null : string.Join("\n", e.Aliases),
                Title = e.Title,
                Designation = e.Designation,
                DateOfBirth = e.DateOfBirth,
                PlaceOfBirth = e.PlaceOfBirth,
                Nationality = e.Nationality,
                Identifiers = e.Identifiers.Count == 0 ? null : string.Join("\n", e.Identifiers),
                Address = e.Address,
                ListedOn = e.ListedOn,
                OtherInformation = e.OtherInformation,
                Details = e.Details
            };

            entry.Fingerprint = Fingerprint(Fields(entry));

            return entry;
        }

        /// <summary>
        /// A hash of the compared fields, so that a later document can
        /// tell at a glance whether an entry says anything different.
        /// </summary>
        private static string Fingerprint(IEnumerable<(string Label, string? Value)> fields) =>
            Sha256Of(System.Text.Encoding.UTF8.GetBytes(string.Join(
                "|",
                fields.Select(f => SanctionsListParser.Normalise(f.Value)))));

        private static string Cap(string value, int length) =>
            value.Length <= length ? value : value[..length];

        /// <summary>
        /// Brings the register up to date from a document, records
        /// what changed, raises anything that does not add up, and
        /// screens everybody against the result.
        /// </summary>
        public async Task ApplyAsync(
            int listId,
            SanctionsListKind kind,
            ISet<int> markedRemoved)
        {
            var list = await _context.SanctionsLists
                .Include(l => l.Entries)
                .Include(l => l.Concerns)
                .FirstAsync(l => l.Id == listId);

            if (list.Status != SanctionsReviewStatus.Pending)
                throw new InvalidOperationException("This document has already been applied.");

            list.Kind = kind;

            foreach (var entry in list.Entries)
                entry.MarkedRemoved = kind == SanctionsListKind.Removals || markedRemoved.Contains(entry.Id);

            var register = await _context.SanctionsRegister
                .ToDictionaryAsync(r => r.Reference);

            var now = DateTime.UtcNow;

            void Discrepancy(string reason, SanctionsListEntry? entry = null) =>
                list.Concerns.Add(new SanctionsConcern
                {
                    Category = SanctionsConcernCategory.Discrepancy,
                    Status = SanctionsConcernStatus.Open,
                    Reference = entry?.Reference,
                    ListedName = entry?.Name,
                    Reason = reason,
                    RaisedAt = now
                });

            void Change(SanctionsChangeType type, string reference, string name, string? diff = null) =>
                list.Changes.Add(new SanctionsChange
                {
                    Type = type,
                    Reference = reference,
                    Name = name,
                    Differences = diff
                });

            var latestApplied = await _context.SanctionsLists
                .Where(l => l.Id != list.Id && l.Status != SanctionsReviewStatus.Pending && l.IssuedOn != null)
                .OrderByDescending(l => l.IssuedOn)
                .Select(l => new { l.ReviewNumber, l.IssuedOn })
                .FirstOrDefaultAsync();

            if (latestApplied != null && list.IssuedOn != null && list.IssuedOn < latestApplied.IssuedOn)
            {
                Discrepancy(
                    $"This document is dated {list.IssuedOn:d MMM yyyy}, before " +
                    $"{latestApplied.ReviewNumber} ({latestApplied.IssuedOn:d MMM yyyy}), " +
                    "which has already been applied. Applying an older document may " +
                    "undo later changes; check the register.");
            }

            var registerWasEmpty = register.Values.All(r => !r.IsActive);

            foreach (var entry in list.Entries)
            {
                register.TryGetValue(entry.Reference, out var current);

                if (entry.MarkedRemoved)
                {
                    if (current is { IsActive: true })
                    {
                        current.IsActive = false;
                        current.LastListId = list.Id;
                        current.UpdatedAt = now;
                        Change(SanctionsChangeType.Removed, entry.Reference, entry.Name);
                    }
                    else
                    {
                        Discrepancy(
                            $"{entry.Reference} is removed by this document but was not on " +
                            "the register.",
                            entry);
                    }

                    continue;
                }

                if (current is not { IsActive: true })
                {
                    if (current == null)
                    {
                        current = new SanctionsRegisterEntry
                        {
                            Reference = entry.Reference,
                            FirstListId = list.Id
                        };

                        _context.SanctionsRegister.Add(current);
                        register[entry.Reference] = current;
                    }

                    Copy(entry, current, list.Id, now);
                    Change(SanctionsChangeType.Added, entry.Reference, entry.Name);

                    if (kind == SanctionsListKind.Changes &&
                        LooksLikeAmendment(list.Title) &&
                        !registerWasEmpty)
                    {
                        Discrepancy(
                            $"{entry.Reference} is amended by this document but was not on the " +
                            "register. It has been added; check it against the full " +
                            "consolidated list.",
                            entry);
                    }

                    continue;
                }

                if (current.Fingerprint != entry.Fingerprint)
                {
                    var diff = Differences(current, entry);

                    Copy(entry, current, list.Id, now);
                    Change(SanctionsChangeType.Amended, entry.Reference, entry.Name, diff);
                }
            }

            if (registerWasEmpty && kind != SanctionsListKind.FullList)
            {
                Discrepancy(
                    "The register was empty before this document, so it now holds only the " +
                    $"{Count(list.Entries.Count(e => !e.MarkedRemoved), "entry", "entries")} named here, not " +
                    "everybody on the sanctions list. Screening is only as complete as the " +
                    "register: upload the full UN consolidated list (or the FIU's full list) " +
                    "to establish it, after which notices like this one update it.");
            }

            if (kind == SanctionsListKind.FullList)
            {
                var inDocument = list.Entries.Select(e => e.Reference).ToHashSet();

                foreach (var gone in register.Values
                    .Where(r => r.IsActive && !inDocument.Contains(r.Reference)))
                {
                    gone.IsActive = false;
                    gone.LastListId = list.Id;
                    gone.UpdatedAt = now;
                    Change(SanctionsChangeType.Removed, gone.Reference, gone.Name);
                }
            }

            list.Status = SanctionsReviewStatus.InReview;
            list.AppliedAt = now;
            list.AppliedByName = await WhoAsync();

            await ScreenAsync(list, register.Values);

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(SanctionsList),
                list.ReviewNumber,
                $"Sanctions document applied as {kind}: " +
                $"{list.Changes.Count(c => c.Type == SanctionsChangeType.Added)} added, " +
                $"{list.Changes.Count(c => c.Type == SanctionsChangeType.Amended)} amended, " +
                $"{list.Changes.Count(c => c.Type == SanctionsChangeType.Removed)} removed; " +
                $"{list.Concerns.Count} matter(s) raised.");
        }

        private static string Count(int n, string one, string many) =>
            $"{n} {(n == 1 ? one : many)}";

        private static bool LooksLikeAmendment(string? title) =>
            title != null && title.Contains("amend", StringComparison.OrdinalIgnoreCase);

        private static void Copy(
            SanctionsListEntry from,
            SanctionsRegisterEntry to,
            int listId,
            DateTime now)
        {
            to.IsActive = true;
            to.IsEntity = from.IsEntity;
            to.Name = from.Name;
            to.Aliases = from.Aliases;
            to.DateOfBirth = from.DateOfBirth;
            to.Nationality = from.Nationality;
            to.Identifiers = from.Identifiers;
            to.Address = from.Address;
            to.Designation = from.Designation;
            to.Title = from.Title;
            to.PlaceOfBirth = from.PlaceOfBirth;
            to.ListedOn = from.ListedOn;
            to.OtherInformation = from.OtherInformation;
            to.Details = from.Details;
            to.Fingerprint = from.Fingerprint;
            to.LastListId = listId;
            to.UpdatedAt = now;
        }

        /// <summary>
        /// What changed in an entry, field by field, in words.
        /// </summary>
        /// <summary>
        /// The parts of an entry that are compared, by label. The name
        /// in original script is left out: it does not survive being
        /// read out of a PDF reliably, so it would show changes that
        /// are not there.
        /// </summary>
        private static (string Label, string? Value)[] Fields(SanctionsListEntry e) =>
            Fields(e.Name, e.Aliases, e.Title, e.Designation, e.DateOfBirth, e.PlaceOfBirth,
                   e.Nationality, e.Identifiers, e.Address, e.ListedOn, e.OtherInformation);

        private static (string Label, string? Value)[] Fields(SanctionsRegisterEntry e) =>
            Fields(e.Name, e.Aliases, e.Title, e.Designation, e.DateOfBirth, e.PlaceOfBirth,
                   e.Nationality, e.Identifiers, e.Address, e.ListedOn, e.OtherInformation);

        private static (string Label, string? Value)[] Fields(
            string name, string? aliases, string? title, string? designation,
            string? dob, string? pob, string? nationality, string? identifiers,
            string? address, string? listedOn, string? other) =>
            new (string, string?)[]
            {
                ("Name", name),
                ("Aliases", aliases),
                ("Title", title),
                ("Designation", designation),
                ("Date of birth", dob),
                ("Place of birth", pob),
                ("Nationality", nationality),
                ("Passport and ID numbers", identifiers),
                ("Address", address),
                ("Listed on and amended", listedOn),
                ("Other information", other)
            };

        /// <summary>
        /// What changed in an entry, field by field, in words. Fields
        /// are compared the way names are: ignoring case, punctuation
        /// and how the PDF happened to break the lines.
        /// </summary>
        public static string Differences(
            IReadOnlyList<(string Label, string? Value)> before,
            IReadOnlyList<(string Label, string? Value)> after)
        {
            var lines = new List<string>();

            for (var i = 0; i < before.Count; i++)
            {
                var a = before[i].Value;
                var b = after[i].Value;

                if (SanctionsListParser.Normalise(a) != SanctionsListParser.Normalise(b))
                    lines.Add($"{before[i].Label}: {Flat(a) ?? "none"} → {Flat(b) ?? "none"}");
            }

            return lines.Count == 0 ? "No difference in the details." : string.Join("\n", lines);
        }

        private static string Differences(SanctionsRegisterEntry before, SanctionsListEntry after) =>
            Differences(Fields(before), Fields(after));

        private static string? Flat(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : string.Join("; ", value.Split('\n', StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Checks every member of staff, every vendor and its contact,
        /// and every client on the latest client list against the
        /// whole register. A matter already raised for the same person
        /// and entry on this review is not raised twice.
        /// </summary>
        public async Task<int> ScreenAsync(
            SanctionsList list,
            IEnumerable<SanctionsRegisterEntry>? register = null)
        {
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

            subjects.AddRange(clients.Select(c => new ScreeningSubject(
                "Client",
                c.Id,
                c.ClientReference == null ? c.Name : $"{c.Name}",
                c.IdNumber)));

            register ??= await _context.SanctionsRegister
                .AsNoTracking()
                .ToListAsync();

            var targets = register
                .Where(r => r.IsActive)
                .Select(r => new ScreeningTarget(
                    r.Reference,
                    r.Name,
                    Split(r.Aliases),
                    Split(r.Identifiers)))
                .ToList();

            var existing = await _context.SanctionsConcerns
                .Where(c => c.SanctionsListId == list.Id && c.Category == SanctionsConcernCategory.PossibleMatch)
                .Select(c => new { c.SubjectType, c.SubjectId, c.Reference })
                .ToListAsync();

            var seen = existing
                .Select(e => $"{e.SubjectType}|{e.SubjectId}|{e.Reference}")
                .ToHashSet();

            var lastChanged = register
                .Where(r => r.IsActive)
                .ToDictionary(r => r.Reference, r => r.LastListId);

            var decisions = (await _context.SanctionsConcerns
                    .AsNoTracking()
                    .Where(c =>
                        c.SanctionsListId != list.Id &&
                        c.Category == SanctionsConcernCategory.PossibleMatch &&
                        c.ResolvedByName != CarriedForwardBy &&
                        (c.Status == SanctionsConcernStatus.Cleared ||
                         c.Status == SanctionsConcernStatus.Confirmed))
                    .Select(c => new
                    {
                        c.SanctionsListId,
                        c.List.ReviewNumber,
                        c.SubjectType,
                        c.SubjectId,
                        c.SubjectName,
                        c.Reference,
                        c.Status,
                        c.Resolution,
                        c.ResolvedByName,
                        c.ResolvedAt
                    })
                    .ToListAsync())
                .GroupBy(c => $"{c.SubjectType}|{c.SubjectId}|{c.Reference}")
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ResolvedAt).First());

            var raised = 0;
            var now = DateTime.UtcNow;

            foreach (var subject in subjects)
            {
                foreach (var target in targets)
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
                        SubjectType = subject.Type,
                        SubjectId = subject.Id,
                        SubjectName = subject.Name,
                        Reason = hit.Reason,
                        Score = hit.Score,
                        RaisedAt = now
                    };

                    if (decisions.TryGetValue(key, out var earlier) &&
                        earlier.SubjectName == subject.Name &&
                        lastChanged.TryGetValue(target.Reference, out var changedIn) &&
                        changedIn <= earlier.SanctionsListId)
                    {
                        concern.Status = earlier.Status;
                        concern.Resolution =
                            $"Carried forward from {earlier.ReviewNumber}, where " +
                            $"{earlier.ResolvedByName} recorded: {earlier.Resolution} " +
                            "Neither the listed entry nor this person's name has changed since.";
                        concern.ResolvedAt = now;
                        concern.ResolvedByName = CarriedForwardBy;
                    }
                    else
                    {
                        raised++;
                    }

                    list.Concerns.Add(concern);
                }
            }

            list.ScreenedStaff = staff.Count;
            list.ScreenedVendors = vendors.Count;
            list.ScreenedClients = clients.Count;
            list.ClientListId = clientList?.Id;
            list.RegisterSizeAfter = targets.Count;

            await _context.SaveChangesAsync();

            return raised;
        }

        private static List<string> Split(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        public async Task ResolveAsync(
            int concernId,
            SanctionsConcernStatus outcome,
            string resolution)
        {
            var concern = await _context.SanctionsConcerns
                .Include(c => c.List)
                .FirstAsync(c => c.Id == concernId);

            if (concern.List.Status == SanctionsReviewStatus.SignedOff)
                throw new InvalidOperationException("The review has been signed off.");

            concern.Status = outcome;
            concern.Resolution = resolution.Trim();
            concern.ResolvedAt = DateTime.UtcNow;
            concern.ResolvedByName = await WhoAsync();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(SanctionsConcern),
                concern.Id.ToString(),
                $"Matter on {concern.List.ReviewNumber} marked {outcome}: {concern.Resolution}");
        }

        /// <summary>
        /// Closes the review. The report is made once, here, and kept
        /// exactly as made with its SHA-256, so a copy shown later can
        /// be checked against it byte for byte.
        /// </summary>
        public async Task SignOffAsync(int listId, string? note)
        {
            var list = await LoadForReportAsync(listId);

            if (list.Status != SanctionsReviewStatus.InReview)
                throw new InvalidOperationException("Only a review in progress can be signed off.");

            if (list.Concerns.Any(c => c.Status == SanctionsConcernStatus.Open))
                throw new InvalidOperationException("Every matter must be resolved before signing off.");

            list.Status = SanctionsReviewStatus.SignedOff;
            list.SignedOffAt = DateTime.UtcNow;
            list.SignedOffByName = await WhoAsync();
            list.SignOffNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            var pdf = _reports.Render(list);

            var folder = Path.Combine(_files.UploadRoot, ReportFolder);
            Directory.CreateDirectory(folder);

            var fileName = $"{list.ReviewNumber}.pdf";
            await File.WriteAllBytesAsync(Path.Combine(folder, fileName), pdf);

            list.ReportPath = $"/uploads/{ReportFolder}/{fileName}";
            list.ReportSha256 = Sha256Of(pdf);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(SanctionsList),
                list.ReviewNumber,
                $"Sanctions review signed off. Report SHA-256 {list.ReportSha256}.");
        }

        public Task<SanctionsList> LoadForReportAsync(int listId) =>
            _context.SanctionsLists
                .Include(l => l.Entries)
                .Include(l => l.Changes)
                .Include(l => l.Concerns)
                .Include(l => l.ClientList)
                .FirstAsync(l => l.Id == listId);

        /// <summary>
        /// Two stored documents side by side, by reference: what the
        /// later one has that the earlier does not, what it no longer
        /// has, and what reads differently.
        /// </summary>
        public async Task<List<SanctionsComparisonRow>> CompareAsync(int earlierId, int laterId)
        {
            var earlier = await _context.SanctionsListEntries
                .AsNoTracking()
                .Where(e => e.SanctionsListId == earlierId)
                .ToListAsync();

            var later = await _context.SanctionsListEntries
                .AsNoTracking()
                .Where(e => e.SanctionsListId == laterId)
                .ToListAsync();

            var before = earlier.GroupBy(e => e.Reference).ToDictionary(g => g.Key, g => g.First());
            var after = later.GroupBy(e => e.Reference).ToDictionary(g => g.Key, g => g.First());

            var rows = new List<SanctionsComparisonRow>();

            foreach (var (reference, entry) in after)
            {
                if (!before.TryGetValue(reference, out var old))
                {
                    rows.Add(new SanctionsComparisonRow(SanctionsChangeType.Added, reference, entry.Name, null));
                }
                else if (old.Fingerprint != entry.Fingerprint)
                {
                    rows.Add(new SanctionsComparisonRow(
                        SanctionsChangeType.Amended,
                        reference,
                        entry.Name,
                        Differences(Fields(old), Fields(entry))));
                }
            }

            foreach (var (reference, entry) in before)
            {
                if (!after.ContainsKey(reference))
                    rows.Add(new SanctionsComparisonRow(SanctionsChangeType.Removed, reference, entry.Name, null));
            }

            return rows.OrderBy(r => r.Type).ThenBy(r => r.Reference).ToList();
        }

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
                    Name = Cap(row.Name, 500),
                    IdNumber = row.IdNumber == null ? null : Cap(row.IdNumber, 100),
                    ClientReference = row.Reference == null ? null : Cap(row.Reference, 100)
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
    }
}
