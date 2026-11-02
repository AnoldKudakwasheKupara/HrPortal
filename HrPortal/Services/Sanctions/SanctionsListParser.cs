using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HrPortal.Entities;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace HrPortal.Services.Sanctions
{
    /// <summary>One entry as it appears in the document.</summary>
    public sealed class ParsedSanctionsEntry
    {
        public string Reference { get; set; } = string.Empty;

        public bool IsEntity { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? OriginalScriptName { get; set; }

        public List<string> Aliases { get; set; } = new();

        public string? Title { get; set; }

        public string? Designation { get; set; }

        public string? DateOfBirth { get; set; }

        public string? PlaceOfBirth { get; set; }

        public string? Nationality { get; set; }

        public List<string> Identifiers { get; set; } = new();

        public List<string> Passports { get; set; } = new();

        public List<string> NationalIds { get; set; } = new();

        public string? PassportText { get; set; }

        public string? NationalIdText { get; set; }

        public string? Address { get; set; }

        public string? ListedOn { get; set; }

        public string? OtherInformation { get; set; }

        /// <summary>The page the entry starts on, when the document has pages.</summary>
        public int? Page { get; set; }

        /// <summary>Text the document underlines in this entry: what an amendment adds.</summary>
        public List<string> AddedText { get; set; } = new();

        /// <summary>Text the document strikes through in this entry: what an amendment deletes.</summary>
        public List<string> RemovedText { get; set; } = new();

        /// <summary>The entry as it now reads, struck-through text left out, on one line.</summary>
        public string Details { get; set; } = string.Empty;
    }

    public sealed class ParsedSanctionsList
    {
        public string? Title { get; set; }

        public string? Reference { get; set; }

        public DateTime? IssuedOn { get; set; }

        public SanctionsDocumentKind Kind { get; set; }

        /// <summary>The title announces changes rather than giving the whole list.</summary>
        public bool LooksLikeNotice => Kind != SanctionsDocumentKind.FullList;

        /// <summary>Which sanctions list the document belongs to, such as the 1988 Sanctions List.</summary>
        public string? ListName { get; set; }

        /// <summary>Who issued it, as far as the document says.</summary>
        public string? Source { get; set; }

        public int PageCount { get; set; }

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
    ///
    /// Notices of amendments underline what they add and strike
    /// through what they delete. The text is put together letter by
    /// letter so each letter can be checked against the lines drawn
    /// on the page: a line just under the baseline is an underline, a
    /// line through the middle of the letters a strike-through. Web
    /// links are underlined too, so lines under a link are ignored.
    /// </summary>
    public static class SanctionsListParser
    {
        private const char AddOpen = '';
        private const char AddClose = '';
        private const char DelOpen = '';
        private const char DelClose = '';
        private const char PageMark = '';

        private static readonly Regex EntryStart = new(
            @"(?<![A-Za-z])(?<ref>[A-Z]{2,3}[ie]\.\d{3})\s+Name\s*:",
            RegexOptions.Compiled);

        private static readonly Regex Markers = new("[-]", RegexOptions.Compiled);

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

        /// <summary>The sanctions committees, by the start of their entries' references.</summary>
        private static readonly Dictionary<string, string> ListsByPrefix = new()
        {
            ["TA"] = "1988 Sanctions List",
            ["QD"] = "ISIL (Da'esh) and Al-Qaida Sanctions List",
            ["KP"] = "1718 Sanctions List",
            ["IQ"] = "1518 Sanctions List",
            ["CD"] = "1533 Sanctions List",
            ["SO"] = "2713 Sanctions List",
            ["SD"] = "1591 Sanctions List",
            ["LY"] = "1970 Sanctions List",
            ["GB"] = "2048 Sanctions List",
            ["CF"] = "2127 Sanctions List",
            ["YE"] = "2140 Sanctions List",
            ["SS"] = "2206 Sanctions List",
            ["HT"] = "2653 Sanctions List"
        };

        public const string ConsolidatedList = "UN Security Council Consolidated List";

        public static ParsedSanctionsList ParsePdf(Stream pdf)
        {
            using var document = PdfDocument.Open(pdf);

            var text = new StringBuilder();
            var blank = new List<int>();

            foreach (var page in document.GetPages())
            {
                text.Append(PageMark).Append(page.Number).Append('\n');

                if (!page.Letters.Any(l => !string.IsNullOrWhiteSpace(l.Value)))
                    blank.Add(page.Number);

                text.Append(PageText(page)).Append('\n');
            }

            var result = ParseText(text.ToString());

            result.PageCount = document.NumberOfPages;

            if (blank.Count == document.NumberOfPages)
            {
                result.Warnings.RemoveAll(w => w.StartsWith("No sanctions entries were found", StringComparison.Ordinal));
                result.Warnings.Insert(0,
                    "No text could be read from any page. The document is probably a scanned " +
                    "image, so the portal cannot read it. Check it by hand, or ask the FIU for " +
                    "a PDF with text in it.");
            }
            else if (blank.Count > 0)
            {
                result.Warnings.Add(
                    $"No text could be read on page{(blank.Count == 1 ? "" : "s")} " +
                    $"{string.Join(", ", blank)}. It may be a scanned image; any names on it were " +
                    "not read. Check it by hand.");
            }

            return result;
        }

        /// <summary>
        /// The text of one page in the order it was written, a line at
        /// a time, with underlined and struck-through letters marked.
        /// </summary>
        private static string PageText(Page page)
        {
            var bars = Bars(page);
            var text = new StringBuilder();
            var state = 0;
            Letter? previous = null;

            foreach (var letter in page.Letters)
            {
                if (previous != null)
                {
                    var size = Math.Max(1, previous.PointSize);

                    if (Math.Abs(letter.StartBaseLine.Y - previous.StartBaseLine.Y) > size * 0.5)
                    {
                        text.Append('\n');
                    }
                    else if (letter.StartBaseLine.X - previous.EndBaseLine.X > size * 0.15 &&
                             !string.IsNullOrWhiteSpace(previous.Value) &&
                             !string.IsNullOrWhiteSpace(letter.Value))
                    {
                        text.Append(' ');
                    }
                }

                var mark = string.IsNullOrWhiteSpace(letter.Value) ? state : MarkOf(letter, bars);

                if (mark != state)
                {
                    if (state == 1) text.Append(AddClose);
                    if (state == 2) text.Append(DelClose);
                    if (mark == 1) text.Append(AddOpen);
                    if (mark == 2) text.Append(DelOpen);

                    state = mark;
                }

                text.Append(letter.Value);
                previous = letter;
            }

            if (state == 1) text.Append(AddClose);
            if (state == 2) text.Append(DelClose);

            return text.ToString();
        }

        /// <summary>
        /// Thin horizontal lines drawn under or through text, leaving
        /// out lines under web links and rules that run well beyond
        /// the text above them, such as table borders.
        /// </summary>
        private static List<PdfRectangle> Bars(Page page)
        {
            var links = page.GetAnnotations()
                .Where(a => a.Type == AnnotationType.Link)
                .Select(a => a.Rectangle)
                .ToList();

            var bars = new List<PdfRectangle>();

            foreach (var path in page.Paths)
            {
                if (!path.IsFilled && !path.IsStroked)
                    continue;

                if (path.GetBoundingRectangle() is not PdfRectangle bar ||
                    bar.Height > 2.5 || bar.Width < 1)
                {
                    continue;
                }

                var middleX = (bar.Left + bar.Right) / 2;
                var middleY = (bar.Bottom + bar.Top) / 2;

                if (links.Any(l =>
                        middleX >= l.Left - 2 && middleX <= l.Right + 2 &&
                        middleY >= l.Bottom - 4 && middleY <= l.Top + 2))
                {
                    continue;
                }

                var covered = page.Letters
                    .Where(l => !string.IsNullOrWhiteSpace(l.Value) &&
                                middleY >= l.StartBaseLine.Y - l.PointSize * 0.35 &&
                                middleY <= l.StartBaseLine.Y + l.PointSize * 0.6)
                    .Sum(l => Math.Max(0, Math.Min(bar.Right, l.GlyphRectangle.Right) -
                                          Math.Max(bar.Left, l.GlyphRectangle.Left)));

                if (covered >= bar.Width * 0.5)
                    bars.Add(bar);
            }

            return bars;
        }

        /// <summary>0 for plain text, 1 for underlined, 2 for struck through.</summary>
        private static int MarkOf(Letter letter, List<PdfRectangle> bars)
        {
            var x = (letter.StartBaseLine.X + letter.EndBaseLine.X) / 2;
            var baseline = letter.StartBaseLine.Y;
            var size = Math.Max(1, letter.PointSize);

            foreach (var bar in bars)
            {
                if (x < bar.Left || x > bar.Right)
                    continue;

                var middle = (bar.Bottom + bar.Top) / 2 - baseline;

                if (middle <= size * 0.08 && middle >= -size * 0.3)
                    return 1;

                if (middle > size * 0.15 && middle < size * 0.6)
                    return 2;
            }

            return 0;
        }

        public static ParsedSanctionsList ParseText(string text)
        {
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", " ");

            var lines = new List<string>();
            var pageStarts = new List<(int At, int Page)>();
            var marked = new StringBuilder();

            foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                var line = Collapse(raw);

                if (line.Length > 1 && line[0] == PageMark &&
                    int.TryParse(line[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                {
                    pageStarts.Add((marked.Length, number));
                    continue;
                }

                line = Regex.Replace(line, $@"{AddClose}(\s*){AddOpen}", "$1");
                line = Regex.Replace(line, $@"{DelClose}(\s*){DelOpen}", "$1");

                var plainLine = Plain(line).Trim();

                if (plainLine.Length == 0 || Noise.Contains(plainLine))
                    continue;

                lines.Add(plainLine);

                if (marked.Length > 0)
                    marked.Append(' ');

                marked.Append(line);
            }

            var flatMarked = marked.ToString();
            var flat = Collapse(Plain(flatMarked));

            var result = new ParsedSanctionsList
            {
                Title = TitleOf(lines),
                Reference = Regex.Match(flat, @"\bSC/\d{4,6}\b").Value is { Length: > 0 } r ? r : null,
                IssuedOn = IssuedOn(flat),
                Source = flat.Contains("Security Council", StringComparison.OrdinalIgnoreCase)
                    ? "United Nations Security Council"
                    : null
            };

            result.Kind = KindOf(result.Title, flat);

            var starts = EntryStart.Matches(flatMarked).ToList();

            for (var i = 0; i < starts.Count; i++)
            {
                var from = starts[i].Index;
                var to = i + 1 < starts.Count ? starts[i + 1].Index : flatMarked.Length;

                var body = flatMarked[from..to];

                if (i == starts.Count - 1)
                    body = CutAtEnd(body);

                var entry = ParseEntry(starts[i].Groups["ref"].Value, body.Trim());

                entry.Page = pageStarts.LastOrDefault(p => p.At <= from) is { Page: > 0 } page
                    ? page.Page
                    : pageStarts.Count > 0 ? pageStarts[0].Page : null;

                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    result.Warnings.Add(
                        $"{entry.Reference}: the name could not be read.");
                }

                result.Entries.Add(entry);
            }

            result.ListName = ListNameOf(result.Title, result.Entries);

            foreach (var duplicate in result.Entries
                .GroupBy(e => e.Reference)
                .Where(g => g.Count() > 1))
            {
                result.Warnings.Add(
                    $"{duplicate.Key} appears {duplicate.Count()} times in the document.");
            }

            if (result.Kind == SanctionsDocumentKind.Amendments && result.Entries.Count > 0)
            {
                var unmarked = result.Entries
                    .Where(e => e.AddedText.Count == 0 && e.RemovedText.Count == 0)
                    .Select(e => e.Reference)
                    .ToList();

                if (unmarked.Count > 0)
                {
                    result.Warnings.Add(
                        $"The notice says it amends entries, but no underlined or struck-through " +
                        $"text was found in {string.Join(", ", unmarked)}. What changed is worked " +
                        "out by comparing with the earlier version instead; check it against the " +
                        "original document.");
                }
            }

            if (result.Entries.Count == 0)
            {
                result.Warnings.Add(
                    "No sanctions entries were found. The document may not be " +
                    "a UN sanctions list or notice, or it may be a scanned image.");
            }

            return result;
        }

        private static ParsedSanctionsEntry ParseEntry(string reference, string marked)
        {
            var entry = new ParsedSanctionsEntry
            {
                Reference = reference,
                IsEntity = Regex.IsMatch(reference, @"^[A-Z]{2,3}e\."),
                AddedText = Runs(marked, AddOpen, AddClose),
                RemovedText = Runs(marked, DelOpen, DelClose)
            };

            var body = Collapse(Plain(Regex.Replace(marked, $"{DelOpen}[^{DelClose}]*(?:{DelClose}|$)", " ")));

            entry.Details = body;

            var nameLabel = Regex.Match(body, @"Name\s*:\s*");
            var afterName = nameLabel.Index + nameLabel.Length;

            var fields = Fields(body);

            var nameText = body[afterName..FirstLabelAfter(body, afterName)].Trim();

            entry.Name = entry.IsEntity ? Clean(nameText) : PersonName(nameText);

            entry.OriginalScriptName = Value(fields, "Name (original script):");
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

            entry.Passports = Identifiers(entry.PassportText).Distinct().ToList();
            entry.NationalIds = Identifiers(entry.NationalIdText).Distinct().ToList();
            entry.Identifiers = entry.Passports.Concat(entry.NationalIds).Distinct().ToList();

            return entry;
        }

        /// <summary>
        /// The marked passages of an entry, leaving out list letters
        /// and punctuation that were marked on their own.
        /// </summary>
        private static List<string> Runs(string marked, char open, char close)
        {
            var runs = new List<string>();

            foreach (Match m in Regex.Matches(marked, $"{open}([^{close}]*)(?:{close}|$)"))
            {
                var run = Clean(Plain(m.Groups[1].Value));
                var meaning = Regex.Replace(run, @"^(?:[a-z]\)\s*)+", string.Empty);

                if (Regex.IsMatch(meaning, @"[\p{L}\p{N}]{2,}"))
                    runs.Add(run);
            }

            return runs;
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

        /// <summary>
        /// Whether the title announces additions, amendments or
        /// removals rather than giving the list as a whole. A title
        /// that names the consolidated list, or the list "as at" a
        /// date, is the whole list whatever else it says.
        /// </summary>
        public static SanctionsDocumentKind KindOf(string? title, string flat)
        {
            var text = (title ?? flat[..Math.Min(flat.Length, 600)]).ToUpperInvariant();

            if (Regex.IsMatch(text, @"\bCONSOLIDATED\b|\bLIST AS AT\b|\bAS AT \d"))
                return SanctionsDocumentKind.FullList;

            var adds = Regex.IsMatch(text, @"\b(ADDS|ADDITION|ADDITIONS|ADDED)\b");
            var amends = Regex.IsMatch(text, @"\b(AMENDS|AMENDMENT|AMENDMENTS|AMENDED)\b");
            var removes = Regex.IsMatch(text, @"\b(REMOVES|REMOVAL|REMOVED|DELETES|DELISTS|DELISTED|DELISTING)\b");

            return ((adds ? 1 : 0) + (amends ? 1 : 0) + (removes ? 1 : 0)) switch
            {
                0 => SanctionsDocumentKind.FullList,
                > 1 => SanctionsDocumentKind.Notice,
                _ => adds
                    ? SanctionsDocumentKind.Additions
                    : amends
                        ? SanctionsDocumentKind.Amendments
                        : SanctionsDocumentKind.Removals
            };
        }

        private static string? ListNameOf(string? title, List<ParsedSanctionsEntry> entries)
        {
            if (title != null)
            {
                if (title.Contains("Consolidated", StringComparison.OrdinalIgnoreCase))
                    return ConsolidatedList;

                var resolution = Regex.Match(title, @"\b[Rr]esolution\s+(\d{3,4})\b");

                if (resolution.Success)
                    return $"{resolution.Groups[1].Value} Sanctions List";
            }

            var prefixes = entries
                .Select(e => e.Reference[..2])
                .Distinct()
                .ToList();

            return prefixes.Count switch
            {
                0 => null,
                1 => ListsByPrefix.TryGetValue(prefixes[0], out var name) ? name : $"{prefixes[0]} Sanctions List",
                _ => ConsolidatedList
            };
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

        private static string Plain(string text) => Markers.Replace(text, string.Empty);

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
