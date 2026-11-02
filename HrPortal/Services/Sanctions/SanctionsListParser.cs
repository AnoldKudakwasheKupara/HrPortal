using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HrPortal.Services.Sanctions
{
    /// <summary>What kind of document an upload is.</summary>
    public enum SanctionsListKind
    {
        /// <summary>
        /// The whole list. Whatever is not in it has been removed.
        /// </summary>
        FullList,

        /// <summary>
        /// A notice of entries added or amended. Anything not
        /// mentioned is unchanged.
        /// </summary>
        Changes,

        /// <summary>A notice of entries taken off the list.</summary>
        Removals
    }

    /// <summary>One entry as it appears in the document.</summary>
    public sealed class ParsedSanctionsEntry
    {
        public string Reference { get; set; } = string.Empty;

        public bool IsEntity { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<string> Aliases { get; set; } = new();

        public string? Title { get; set; }

        public string? Designation { get; set; }

        public string? DateOfBirth { get; set; }

        public string? PlaceOfBirth { get; set; }

        public string? Nationality { get; set; }

        public List<string> Identifiers { get; set; } = new();

        public string? PassportText { get; set; }

        public string? NationalIdText { get; set; }

        public string? Address { get; set; }

        public string? ListedOn { get; set; }

        public string? OtherInformation { get; set; }

        /// <summary>The entry as printed, on one line.</summary>
        public string Details { get; set; } = string.Empty;
    }

    public sealed class ParsedSanctionsList
    {
        public string? Title { get; set; }

        public string? Reference { get; set; }

        public DateTime? IssuedOn { get; set; }

        public SanctionsListKind KindGuess { get; set; }

        public List<ParsedSanctionsEntry> Entries { get; set; } = new();

        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Reads a United Nations sanctions document: the Security
    /// Council press releases the FIU forwards, and the consolidated
    /// list itself, which print each entry the same way, starting
    /// with its permanent reference (TAi.007, QDe.004 and so on).
    ///
    /// The reference is what identifies an entry from one document to
    /// the next. Names are spelt differently between documents and
    /// even within one, so they are never used to decide that two
    /// entries are the same.
    /// </summary>
    public static class SanctionsListParser
    {
        private static readonly Regex EntryStart = new(
            @"(?<![A-Za-z])(?<ref>[A-Z]{2,3}[ie]\.\d{3})\s+Name\s*:",
            RegexOptions.Compiled);

        /// <summary>
        /// Lines the web page prints around the content, which turn
        /// up in the middle of an entry when it crosses a page.
        /// </summary>
        private static readonly string[] Noise =
        {
            "Security Council",
            "UN Meetings Coverage and Press Releases",
            "English ⌄",
            "English",
            "!",
            "Press Release"
        };

        /// <summary>Where the entries end and the closing notes begin.</summary>
        private static readonly string[] EndMarkers =
        {
            "The 1988 Sanctions List is updated",
            "Sanctions List is updated regularly",
            "The United Nations Security Council Consolidated List",
            "* *** *",
            "For information media"
        };

        private static readonly string[] Labels =
        {
            "Name (original script):",
            "Title:",
            "Designation:",
            "DOB:",
            "POB:",
            "Good quality a.k.a.:",
            "Low quality a.k.a.:",
            "A.k.a.:",
            "F.k.a.:",
            "Nationality:",
            "Passport no:",
            "National identification no:",
            "Address:",
            "Listed on:",
            "Review concluded on:",
            "Other information:",
            "INTERPOL-UN Security Council Special Notice"
        };

        public static ParsedSanctionsList ParsePdf(Stream pdf)
        {
            using var document = PdfDocument.Open(pdf);

            var text = new StringBuilder();

            foreach (var page in document.GetPages())
                text.AppendLine(ContentOrderTextExtractor.GetText(page));

            return ParseText(text.ToString());
        }

        public static ParsedSanctionsList ParseText(string text)
        {
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", " ");

            var lines = text
                .Replace("\r", string.Empty)
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !Noise.Contains(l))
                .ToList();

            var flat = Collapse(string.Join(" ", lines));

            var result = new ParsedSanctionsList
            {
                Title = TitleOf(lines),
                Reference = Regex.Match(flat, @"\bSC/\d{4,6}\b").Value is { Length: > 0 } r ? r : null,
                IssuedOn = IssuedOn(flat)
            };

            result.KindGuess = Guess(result.Title, flat);

            var starts = EntryStart.Matches(flat).ToList();

            for (var i = 0; i < starts.Count; i++)
            {
                var from = starts[i].Index;
                var to = i + 1 < starts.Count ? starts[i + 1].Index : flat.Length;

                var body = flat[from..to];

                if (i == starts.Count - 1)
                    body = CutAtEnd(body);

                var entry = ParseEntry(starts[i].Groups["ref"].Value, body.Trim());

                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    result.Warnings.Add(
                        $"{entry.Reference}: the name could not be read.");
                }

                result.Entries.Add(entry);
            }

            foreach (var duplicate in result.Entries
                .GroupBy(e => e.Reference)
                .Where(g => g.Count() > 1))
            {
                result.Warnings.Add(
                    $"{duplicate.Key} appears {duplicate.Count()} times in the document.");
            }

            if (result.Entries.Count == 0)
            {
                result.Warnings.Add(
                    "No sanctions entries were found. The document may not be " +
                    "a UN sanctions list or notice, or it may be a scanned image.");
            }

            return result;
        }

        private static ParsedSanctionsEntry ParseEntry(string reference, string body)
        {
            var entry = new ParsedSanctionsEntry
            {
                Reference = reference,
                IsEntity = Regex.IsMatch(reference, @"^[A-Z]{2,3}e\."),
                Details = body
            };

            var nameLabel = Regex.Match(body, @"Name\s*:\s*");
            var afterName = nameLabel.Index + nameLabel.Length;

            var fields = Fields(body);

            var nameText = body[afterName..FirstLabelAfter(body, afterName)].Trim();

            entry.Name = entry.IsEntity ? Clean(nameText) : PersonName(nameText);

            entry.Title = Value(fields, "Title:");
            entry.Designation = Value(fields, "Designation:");
            entry.DateOfBirth = Value(fields, "DOB:");
            entry.PlaceOfBirth = Value(fields, "POB:");
            entry.Nationality = Value(fields, "Nationality:");
            entry.PassportText = Value(fields, "Passport no:");
            entry.NationalIdText = Value(fields, "National identification no:");
            entry.Address = Value(fields, "Address:");
            entry.ListedOn = Value(fields, "Listed on:");
            entry.OtherInformation = Value(fields, "Other information:");

            foreach (var label in new[] { "Good quality a.k.a.:", "Low quality a.k.a.:", "A.k.a.:", "F.k.a.:" })
                entry.Aliases.AddRange(Aliases(Value(fields, label)));

            entry.Aliases = entry.Aliases
                .Where(a => !string.Equals(Normalise(a), Normalise(entry.Name), StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            entry.Identifiers = Identifiers(entry.PassportText)
                .Concat(Identifiers(entry.NationalIdText))
                .Distinct()
                .ToList();

            return entry;
        }

        /// <summary>
        /// An individual's name is printed in numbered parts, any of
        /// which may be "na".
        /// </summary>
        private static string PersonName(string text)
        {
            var parts = Regex.Split(text, @"\s*\b[1-4]\s*:\s*")
                .Select(p => p.Trim())
                .Where(p => p.Length > 0 && !p.Equals("na", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Clean(string.Join(" ", parts));
        }

        private static Dictionary<string, string> Fields(string body)
        {
            var found = new List<(int Index, string Label)>();

            foreach (var label in Labels)
            {
                var at = body.IndexOf(label, StringComparison.OrdinalIgnoreCase);

                if (at >= 0)
                    found.Add((at, label));
            }

            found = found.OrderBy(f => f.Index).ToList();

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < found.Count; i++)
            {
                var start = found[i].Index + found[i].Label.Length;
                var end = i + 1 < found.Count ? found[i + 1].Index : body.Length;

                if (end > start && !fields.ContainsKey(found[i].Label))
                    fields[found[i].Label] = body[start..end].Trim();
            }

            return fields;
        }

        private static int FirstLabelAfter(string body, int from)
        {
            var next = Labels
                .Select(l => body.IndexOf(l, from, StringComparison.OrdinalIgnoreCase))
                .Where(i => i >= 0)
                .DefaultIfEmpty(body.Length)
                .Min();

            return next;
        }

        private static string? Value(Dictionary<string, string> fields, string label)
        {
            if (!fields.TryGetValue(label, out var value))
                return null;

            value = value.Trim().TrimEnd('.').Trim();

            return value.Length == 0 || value.Equals("na", StringComparison.OrdinalIgnoreCase)
                ? null
                : value;
        }

        /// <summary>
        /// Aliases come as a) ... b) ..., each possibly followed by a
        /// remark: "(previously listed as)", ", born in ...", or the
        /// name in original script.
        /// </summary>
        private static IEnumerable<string> Aliases(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            foreach (var part in Regex.Split(text, @"(?:^|\s)[a-z]\)\s+"))
            {
                var alias = Regex.Replace(part, @"\([^)]*\)", " ");
                alias = Regex.Replace(alias, @",\s*born\b.*$", string.Empty, RegexOptions.IgnoreCase);
                alias = Clean(alias);

                if (alias.Length > 1 && !alias.Equals("na", StringComparison.OrdinalIgnoreCase))
                    yield return alias;
            }
        }

        /// <summary>
        /// Passport and identity numbers: a run of letters and digits
        /// with at least five digits in it, which leaves out dates.
        /// </summary>
        public static IEnumerable<string> Identifiers(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            foreach (Match m in Regex.Matches(text, @"\b[A-Z0-9][A-Z0-9\-/]{4,}[A-Z0-9]\b"))
            {
                var value = Regex.Replace(m.Value, @"[^A-Z0-9]", string.Empty);

                if (value.Count(char.IsDigit) >= 5 && value.Length >= 6)
                    yield return value;
            }
        }

        private static string? TitleOf(List<string> lines)
        {
            var start = lines.FindIndex(l =>
                l.StartsWith("Security Council", StringComparison.OrdinalIgnoreCase) &&
                l.Length > "Security Council".Length + 5);

            if (start < 0)
                return lines.FirstOrDefault(l => l.Length > 20);

            var title = new List<string>();

            for (var i = start; i < lines.Count && title.Count < 4; i++)
            {
                if (lines[i].StartsWith("On ", StringComparison.Ordinal))
                    break;

                title.Add(lines[i]);
            }

            return Clean(string.Join(" ", title));
        }

        private static DateTime? IssuedOn(string flat)
        {
            var match = Regex.Match(
                flat,
                @"\b(\d{1,2})\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{4})\b");

            return match.Success &&
                   DateTime.TryParseExact(
                       match.Value,
                       "d MMMM yyyy",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out var date)
                ? date
                : null;
        }

        private static SanctionsListKind Guess(string? title, string flat)
        {
            var text = (title ?? flat[..Math.Min(flat.Length, 600)]).ToUpperInvariant();

            if (Regex.IsMatch(text, @"\b(REMOVES|DELETES|DELISTS|REMOVAL)\b") &&
                !Regex.IsMatch(text, @"\b(ADDS|AMENDS)\b"))
            {
                return SanctionsListKind.Removals;
            }

            if (Regex.IsMatch(text, @"\b(ADDS|AMENDS|AMENDMENT|AMENDMENTS|ADDITION)\b"))
                return SanctionsListKind.Changes;

            return SanctionsListKind.FullList;
        }

        private static string CutAtEnd(string body)
        {
            var end = EndMarkers
                .Select(m => body.IndexOf(m, StringComparison.OrdinalIgnoreCase))
                .Where(i => i > 0)
                .DefaultIfEmpty(body.Length)
                .Min();

            return body[..end];
        }

        private static string Collapse(string text) =>
            Regex.Replace(text, @"\s+", " ").Trim();

        private static string Clean(string text) =>
            Collapse(text).Trim(' ', ',', ';', '.');

        /// <summary>
        /// A name reduced to what matters for comparing: capitals,
        /// no accents, no punctuation, single spaces.
        /// </summary>
        public static string Normalise(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var decomposed = name.Normalize(NormalizationForm.FormD);

            var letters = new StringBuilder(decomposed.Length);

            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                letters.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
            }

            return Collapse(letters.ToString());
        }
    }
}
