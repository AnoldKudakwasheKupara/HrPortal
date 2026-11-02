using HrPortal.Entities;

namespace HrPortal.Services.Sanctions
{
    /// <summary>One field that differs between two versions of an entry.</summary>
    public sealed record FieldDifference(string Field, string? Before, string? After);

    /// <summary>The earlier version of an entry, and which document it came from.</summary>
    public sealed record EntryVersion(SanctionsEntry Entry, string DocumentLabel);

    public sealed class ComparedChange
    {
        public SanctionsChangeType Type { get; init; }

        public SanctionsEntry Entry { get; init; } = null!;

        public List<FieldDifference> Differences { get; init; } = new();

        /// <summary>How the change was worked out, when that is not obvious.</summary>
        public string? Note { get; init; }
    }

    public sealed class Comparison
    {
        public List<ComparedChange> Changes { get; } = new();

        public int Unchanged { get; set; }

        /// <summary>Names on the new document that could not be compared with anything.</summary>
        public int NotCompared { get; set; }

        /// <summary>The comparison could not be completed and must not be read as complete.</summary>
        public bool Inconclusive { get; set; }

        public List<string> Discrepancies { get; } = new();

        /// <summary>Anything the officer needs to know to read the result rightly.</summary>
        public List<string> Notes { get; } = new();
    }

    /// <summary>
    /// Works out what a new sanctions document changes.
    ///
    /// Entries are matched on their UN reference, never on names: a
    /// name spelt differently, a new alias or a new passport number is
    /// an amendment to the same entry, not a new listing.
    ///
    /// Two full lists are compared whole: what is on one and not the
    /// other was added or removed. A notice only speaks for the names
    /// it gives, so each of them is compared with its own earlier
    /// version and names it does not mention are left alone. When no
    /// earlier version has been uploaded, what changed is taken from
    /// the notice's underlining and strike-through; when that is not
    /// there either, the result says it could not be worked out.
    /// </summary>
    public static class SanctionsComparer
    {
        public static Comparison Compare(
            SanctionsDocument current,
            IReadOnlyList<SanctionsEntry> currentEntries,
            SanctionsDocument? previous,
            IReadOnlyList<SanctionsEntry> previousEntries,
            Func<string, EntryVersion?> earlierVersion)
        {
            var result = new Comparison();

            if (currentEntries.Count == 0)
            {
                result.Inconclusive = true;
                result.Discrepancies.Add(
                    $"No names could be read from {current.Label}, so nothing was compared and " +
                    "nobody was checked against it. Check the document by hand.");
                return result;
            }

            if (previous != null && previous.Id != current.Id)
            {
                if (previous.Sha256 == current.Sha256)
                {
                    result.Discrepancies.Add(
                        "The two documents are the same file. Check that the right document was received.");
                }

                if (current.IssuedOn != null && previous.IssuedOn != null && current.IssuedOn < previous.IssuedOn)
                {
                    result.Discrepancies.Add(
                        $"The new document is dated {current.IssuedOn:d MMM yyyy}, earlier than the " +
                        $"previous one ({previous.IssuedOn:d MMM yyyy}).");
                }

                if (previousEntries.Count == 0)
                {
                    result.Inconclusive = true;
                    result.NotCompared = Distinct(currentEntries).Count;
                    result.Discrepancies.Add(
                        $"No names could be read from {previous.Label}, so the new document could " +
                        "not be compared with it. Check it by hand or compare with another document.");
                    return result;
                }
            }

            if (current.Kind == SanctionsDocumentKind.FullList)
                CompareFullList(result, current, currentEntries, previous, previousEntries);
            else
                CompareNotice(result, current, currentEntries, previous, earlierVersion);

            return result;
        }

        private static void CompareFullList(
            Comparison result,
            SanctionsDocument current,
            IReadOnlyList<SanctionsEntry> currentEntries,
            SanctionsDocument? previous,
            IReadOnlyList<SanctionsEntry> previousEntries)
        {
            if (previous == null)
                return;

            var after = Distinct(currentEntries);
            var before = Distinct(previousEntries);

            if (previous.Kind != SanctionsDocumentKind.FullList)
            {
                var overlap = 0;

                foreach (var (reference, entry) in after.OrderBy(p => p.Key))
                {
                    if (!before.TryGetValue(reference, out var old))
                        continue;

                    overlap++;

                    if (previous.Kind == SanctionsDocumentKind.Removals)
                    {
                        result.Discrepancies.Add(
                            $"{reference} {entry.Name} was removed by {previous.Label}, but is on {current.Label}.");
                        continue;
                    }

                    Amended(result, old, entry, null);
                }

                result.NotCompared = after.Count - overlap;
                result.Inconclusive = result.NotCompared > 0;
                result.Notes.Add(
                    $"{previous.Label} is a notice, not the full list. Only the {before.Count} " +
                    $"name{(before.Count == 1 ? "" : "s")} it gives could be compared; " +
                    $"the other {result.NotCompared} on the new list could not. Compare two full " +
                    "lists to see everything added or removed.");
                return;
            }

            if (current.ListName != null && previous.ListName != null && current.ListName != previous.ListName)
            {
                result.Discrepancies.Add(
                    $"The documents are different lists: {previous.ListName} and {current.ListName}. " +
                    "Names on one and not the other show as added or removed.");
            }

            foreach (var (reference, entry) in after.OrderBy(p => p.Key))
            {
                if (before.TryGetValue(reference, out var old))
                    Amended(result, old, entry, null);
                else
                    result.Changes.Add(new ComparedChange { Type = SanctionsChangeType.Added, Entry = entry });
            }

            foreach (var (reference, entry) in before.OrderBy(p => p.Key))
            {
                if (!after.ContainsKey(reference))
                    result.Changes.Add(new ComparedChange { Type = SanctionsChangeType.Removed, Entry = entry });
            }
        }

        private static void CompareNotice(
            Comparison result,
            SanctionsDocument current,
            IReadOnlyList<SanctionsEntry> currentEntries,
            SanctionsDocument? previous,
            Func<string, EntryVersion?> earlierVersion)
        {
            var entries = Distinct(currentEntries);
            var alreadyTheSame = 0;

            foreach (var (reference, entry) in entries.OrderBy(p => p.Key))
            {
                var before = earlierVersion(reference);

                switch (current.Kind)
                {
                    case SanctionsDocumentKind.Removals:
                        if (before == null && previous != null)
                        {
                            result.Discrepancies.Add(
                                $"The notice removes {reference} {entry.Name}, but it is not on {previous.Label}.");
                        }

                        result.Changes.Add(new ComparedChange { Type = SanctionsChangeType.Removed, Entry = entry });
                        break;

                    case SanctionsDocumentKind.Additions when before == null:
                        result.Changes.Add(new ComparedChange { Type = SanctionsChangeType.Added, Entry = entry });
                        break;

                    case SanctionsDocumentKind.Additions:
                        result.Discrepancies.Add(
                            $"The notice adds {reference} {entry.Name}, but it is already on {before!.DocumentLabel}.");

                        if (!Amended(result, before.Entry, entry, $"Compared with the version in {before.DocumentLabel}."))
                            alreadyTheSame++;
                        break;

                    default:
                        if (before != null)
                        {
                            if (!Amended(result, before.Entry, entry, $"Compared with the version in {before.DocumentLabel}."))
                                alreadyTheSame++;
                        }
                        else if (current.Kind == SanctionsDocumentKind.Notice && MostlyUnderlined(entry))
                        {
                            result.Changes.Add(new ComparedChange
                            {
                                Type = SanctionsChangeType.Added,
                                Entry = entry,
                                Note = "The whole entry is underlined in the notice, so it is taken as a new listing."
                            });
                        }
                        else
                        {
                            FromMarkings(result, entry);
                        }
                        break;
                }
            }

            if (alreadyTheSame > 0)
            {
                result.Notes.Add(
                    $"{alreadyTheSame} name{(alreadyTheSame == 1 ? "" : "s")} the notice changes already " +
                    "read the same in the earlier version uploaded, so the change had been recorded before.");
            }

            result.Notes.Add(
                $"This is a {SanctionsText.Kind(current.Kind).ToLowerInvariant()} giving " +
                $"{entries.Count} name{(entries.Count == 1 ? "" : "s")}. Names it does not mention are not " +
                "affected by it and are not counted as removed.");
        }

        /// <summary>
        /// Records an amendment when the two versions differ; counts the
        /// entry as unchanged and returns false when they do not.
        /// </summary>
        private static bool Amended(Comparison result, SanctionsEntry old, SanctionsEntry entry, string? note)
        {
            var differences = Differences(old, entry);

            if (differences.Count == 0)
            {
                result.Unchanged++;
                return false;
            }

            result.Changes.Add(new ComparedChange
            {
                Type = SanctionsChangeType.Changed,
                Entry = entry,
                Differences = differences,
                Note = note
            });

            if (SanctionsListParser.Normalise(old.Name) != SanctionsListParser.Normalise(entry.Name) &&
                SanctionsMatcher.Match(
                    new ScreeningSubject("", null, old.Name, null),
                    new ScreeningTarget(entry.Reference, entry.Name, SanctionsService.Lines(entry.Aliases), new List<string>())) == null)
            {
                result.Discrepancies.Add(
                    $"{entry.Reference} is given as {old.Name} before but as {entry.Name} now. " +
                    "Check it is the same listed party.");
            }

            return true;
        }

        /// <summary>
        /// With no earlier version to compare against, the notice's own
        /// markings are all there is to say what changed.
        /// </summary>
        private static void FromMarkings(Comparison result, SanctionsEntry entry)
        {
            var added = SanctionsService.Lines(entry.AddedText);
            var removed = SanctionsService.Lines(entry.RemovedText);

            if (added.Count == 0 && removed.Count == 0)
            {
                result.Inconclusive = true;
                result.Changes.Add(new ComparedChange
                {
                    Type = SanctionsChangeType.Changed,
                    Entry = entry,
                    Note = "No earlier version of this entry has been uploaded and the notice marks " +
                           "nothing, so what changed could not be worked out."
                });
                result.Discrepancies.Add(
                    $"{entry.Reference} {entry.Name}: what the notice changes could not be worked out. " +
                    "Check the original document.");
                return;
            }

            result.Changes.Add(new ComparedChange
            {
                Type = SanctionsChangeType.Changed,
                Entry = entry,
                Differences = removed.Select(r => new FieldDifference("Struck through", r, null))
                    .Concat(added.Select(a => new FieldDifference("Underlined", null, a)))
                    .ToList(),
                Note = "No earlier version of this entry has been uploaded, so what changed is taken " +
                       "from the notice's underlining and strike-through."
            });
        }

        private static bool MostlyUnderlined(SanctionsEntry entry) =>
            entry.Details.Length > 0 &&
            SanctionsService.Lines(entry.AddedText).Sum(a => a.Length) >= entry.Details.Length * 0.7;

        /// <summary>
        /// The parts of an entry that are compared, by label. The name
        /// in original script is left out: it does not survive being
        /// read out of a PDF reliably, so it would show changes that
        /// are not there. Lists (aliases, passports, ID numbers) are
        /// compared item by item, whatever order they come in.
        /// </summary>
        public static (string Label, string? Value, bool IsList)[] Fields(SanctionsEntry e) =>
            new (string, string?, bool)[]
            {
                ("Name", e.Name, false),
                ("Aliases", e.Aliases, true),
                ("Title", e.Title, false),
                ("Designation", e.Designation, false),
                ("Date of birth", e.DateOfBirth, false),
                ("Place of birth", e.PlaceOfBirth, false),
                ("Nationality", e.Nationality, false),
                ("Passport numbers", e.Passports, true),
                ("National ID numbers", e.NationalIds, true),
                ("Address", e.Address, false),
                ("Listed on and amended", e.ListedOn, false),
                ("Other information", e.OtherInformation, false)
            };

        /// <summary>
        /// What differs, field by field, ignoring case, punctuation and
        /// how the PDF happened to break lines. For a list, before is
        /// what is no longer given and after is what is newly given.
        /// </summary>
        public static List<FieldDifference> Differences(SanctionsEntry before, SanctionsEntry after)
        {
            var a = Fields(before);
            var b = Fields(after);
            var differences = new List<FieldDifference>();

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i].IsList)
                {
                    var old = Items(a[i].Value);
                    var now = Items(b[i].Value);

                    var gone = old.Where(o => !now.ContainsKey(o.Key)).Select(o => o.Value).ToList();
                    var come = now.Where(n => !old.ContainsKey(n.Key)).Select(n => n.Value).ToList();

                    if (gone.Count > 0 || come.Count > 0)
                    {
                        differences.Add(new FieldDifference(
                            a[i].Label,
                            gone.Count == 0 ? null : string.Join("; ", gone),
                            come.Count == 0 ? null : string.Join("; ", come)));
                    }
                }
                else if (SanctionsListParser.Normalise(a[i].Value) != SanctionsListParser.Normalise(b[i].Value))
                {
                    differences.Add(new FieldDifference(a[i].Label, Flat(a[i].Value), Flat(b[i].Value)));
                }
            }

            return differences;
        }

        private static Dictionary<string, string> Items(string? value) =>
            SanctionsService.Lines(value)
                .GroupBy(SanctionsListParser.Normalise)
                .Where(g => g.Key.Length > 0)
                .ToDictionary(g => g.Key, g => g.First());

        private static string? Flat(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : string.Join("; ", SanctionsService.Lines(value));

        private static Dictionary<string, SanctionsEntry> Distinct(IReadOnlyList<SanctionsEntry> entries) =>
            entries.GroupBy(e => e.Reference).ToDictionary(g => g.Key, g => g.First());
    }
}
