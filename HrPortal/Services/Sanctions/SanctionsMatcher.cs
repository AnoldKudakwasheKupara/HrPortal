using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HrPortal.Entities;

namespace HrPortal.Services.Sanctions
{
    /// <summary>
    /// A person or company the company deals with. IsOrganisation is
    /// false for a person, true for a company and null when it is not
    /// known, as with a vendor that may be a sole trader.
    /// </summary>
    public sealed record ScreeningSubject(
        string Type,
        int? Id,
        string Name,
        string? IdNumber,
        DateTime? DateOfBirth = null,
        string? Nationality = null,
        IReadOnlyList<string>? OtherIds = null,
        bool? IsOrganisation = null);

    /// <summary>One listed party, in the form screening needs.</summary>
    public sealed record ScreeningTarget(
        string Reference,
        string Name,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> Identifiers,
        bool IsEntity = false,
        string? DateOfBirth = null,
        string? Nationality = null);

    /// <summary>Whether a supporting detail agrees, disagrees or cannot be checked.</summary>
    public enum Agreement
    {
        Same,
        Consistent,
        Different,
        NotAvailable
    }

    public sealed record ScreeningHit(
        SanctionsMatchStrength Strength,
        int Score,
        string Reason,
        IReadOnlyList<string> Evidence);

    /// <summary>
    /// Decides whether somebody resembles a listed party, and says why.
    ///
    /// An identical passport or identity number is an exact match.
    /// Otherwise names are compared with every name and alias the
    /// entry gives, ignoring order, accents, punctuation, honorifics
    /// and, for companies, endings such as Ltd or (Pvt); allowing for
    /// the different spellings a transliterated name comes in; and
    /// requiring each word of the shorter name to closely match a word
    /// of the longer one.
    ///
    /// A name match is then weighed against the date of birth and
    /// nationality where both sides give them. The same name with an
    /// agreeing date of birth or nationality and nothing against it is
    /// an exact match. A similar but not identical name whose date of
    /// birth clearly differs is set aside: recorded, shown, and open to
    /// being looked into, but not raised. Everything else is a
    /// potential match. Missing details never rule anybody out.
    /// </summary>
    public static class SanctionsMatcher
    {
        /// <summary>The name score from which a match is raised, unless configured otherwise.</summary>
        public const int DefaultThreshold = 75;

        private const double WordMatch = 0.88;

        private static readonly HashSet<string> Honorifics = new(StringComparer.Ordinal)
        {
            "MR", "MRS", "MS", "MISS", "DR", "PROF", "SIR",
            "MULLAH", "MULLA", "MAULAVI", "MAULVI", "MAWLAWI", "HAJI", "HAJJI",
            "SHEIKH", "SHAIKH", "QARI", "MUFTI", "SAYED", "SAYYID"
        };

        private static readonly HashSet<string> CompanyWords = new(StringComparer.Ordinal)
        {
            "LTD", "LIMITED", "PVT", "PRIVATE", "PTY", "CO", "COMPANY", "INC", "INCORPORATED",
            "LLC", "PLC", "CORP", "CORPORATION", "SA", "GMBH", "THE", "AND", "OF"
        };

        private static readonly HashSet<string> GenericCompanyWords = new(StringComparer.Ordinal)
        {
            "TRADING", "TRADERS", "HOLDINGS", "GROUP", "INVESTMENTS", "ENTERPRISES", "SERVICES",
            "INTERNATIONAL", "GENERAL", "NATIONAL", "BANK", "SOLUTIONS", "SUPPLIES", "SYSTEMS"
        };

        public static ScreeningHit? Match(ScreeningSubject subject, ScreeningTarget target, int threshold = DefaultThreshold)
        {
            if (subject.IsOrganisation == false && target.IsEntity)
                return null;

            var company = target.IsEntity || subject.IsOrganisation == true;

            var subjectIds = new[] { subject.IdNumber }
                .Concat(subject.OtherIds ?? Array.Empty<string>())
                .Select(Alphanumeric)
                .Where(i => i.Length >= 6)
                .Distinct()
                .ToList();

            var listedIds = target.Identifiers.Select(Alphanumeric).Where(i => i.Length >= 6).ToHashSet();

            var sameId = subjectIds.FirstOrDefault(listedIds.Contains);

            var name = BestName(subject.Name, target, company);

            var dob = DateOfBirth(subject.DateOfBirth, target.DateOfBirth);
            var nationality = Nationality(subject.Nationality, target.Nationality);

            var idEvidence = sameId != null
                ? "ID number: the same (" + sameId + ")"
                : subjectIds.Count > 0 && listedIds.Count > 0
                    ? "ID number: no match with the numbers listed"
                    : "ID number: not available on both sides";

            if (sameId != null)
            {
                return new ScreeningHit(
                    SanctionsMatchStrength.Exact,
                    100,
                    $"ID number {sameId} is the same as one given for the listed entry.",
                    Evidence(name, dob, nationality, idEvidence));
            }

            if (name == null || name.Score < threshold)
                return null;

            if (name.Exact)
            {
                if (target.IsEntity)
                {
                    return new ScreeningHit(
                        SanctionsMatchStrength.Exact,
                        98,
                        $"Same name as the listed organisation \"{name.Variant}\", leaving out endings such as Ltd.",
                        Evidence(name, dob, nationality, idEvidence));
                }

                if (dob.Agreement == Agreement.Different)
                {
                    return new ScreeningHit(
                        SanctionsMatchStrength.Potential,
                        90,
                        $"Same name as \"{name.Variant}\", but the date of birth differs: probably a different person.",
                        Evidence(name, dob, nationality, idEvidence));
                }

                if (dob.Agreement is Agreement.Same or Agreement.Consistent || nationality.Agreement == Agreement.Same)
                {
                    return new ScreeningHit(
                        SanctionsMatchStrength.Exact,
                        99,
                        $"Same name as \"{name.Variant}\", and the " +
                        (dob.Agreement is Agreement.Same or Agreement.Consistent ? "date of birth" : "nationality") +
                        " agrees.",
                        Evidence(name, dob, nationality, idEvidence));
                }

                return new ScreeningHit(
                    SanctionsMatchStrength.Potential,
                    95,
                    $"Same name as \"{name.Variant}\". No date of birth or nationality on both sides to confirm or rule it out.",
                    Evidence(name, dob, nationality, idEvidence));
            }

            if (dob.Agreement == Agreement.Different)
            {
                return new ScreeningHit(
                    SanctionsMatchStrength.SetAside,
                    name.Score,
                    $"Name resembles \"{name.Variant}\", but the date of birth differs, so it was set aside.",
                    Evidence(name, dob, nationality, idEvidence));
            }

            var score = dob.Agreement is Agreement.Same or Agreement.Consistent
                ? Math.Min(97, name.Score + 3)
                : name.Score;

            return new ScreeningHit(
                SanctionsMatchStrength.Potential,
                score,
                $"Name resembles \"{name.Variant}\" ({name.Matched} of {name.Of} name words match).",
                Evidence(name, dob, nationality, idEvidence));
        }

        private sealed record NameMatch(int Score, bool Exact, string Variant, bool IsAlias, int Matched, int Of);

        private sealed record Check(Agreement Agreement, string Text);

        private static List<string> Evidence(NameMatch? name, Check dob, Check nationality, string id)
        {
            var lines = new List<string>();

            if (name == null)
            {
                lines.Add("Name similarity: low");
            }
            else
            {
                lines.Add(name.Exact
                    ? "Name similarity: the same name"
                    : $"Name similarity: {(name.Score >= 90 ? "high" : "medium")} ({name.Score}%)");
                lines.Add(name.IsAlias ? $"Matched on: an alias, \"{name.Variant}\"" : "Matched on: the listed name");
            }

            lines.Add(dob.Text);
            lines.Add(nationality.Text);
            lines.Add(id);

            return lines;
        }

        private static NameMatch? BestName(string subjectName, ScreeningTarget target, bool company)
        {
            var subject = Words(subjectName, company);

            if (subject.Count == 0)
                return null;

            NameMatch? best = null;
            var first = true;

            foreach (var variant in new[] { target.Name }.Concat(target.Aliases))
            {
                var hit = CompareNames(subject, Words(variant, company), variant, !first, company);
                first = false;

                if (hit != null && (best == null || hit.Score > best.Score || (hit.Exact && !best.Exact)))
                    best = hit;
            }

            return best;
        }

        private static NameMatch? CompareNames(
            List<string> subject,
            List<string> listed,
            string listedName,
            bool isAlias,
            bool company)
        {
            if (listed.Count == 0)
                return null;

            if (subject.OrderBy(w => w).SequenceEqual(listed.OrderBy(w => w)))
                return new NameMatch(98, true, listedName, isAlias, listed.Count, listed.Count);

            var shorter = subject.Count <= listed.Count ? subject : listed;
            var longer = ReferenceEquals(shorter, subject) ? listed : subject;

            if (shorter.Count < 2 &&
                !(company && shorter.Count == 1 && shorter[0].Length >= 5 && !GenericCompanyWords.Contains(shorter[0])))
            {
                return null;
            }

            var scores = new List<double>();
            var used = new HashSet<int>();

            foreach (var word in shorter)
            {
                var bestScore = 0d;
                var bestIndex = -1;

                for (var i = 0; i < longer.Count; i++)
                {
                    if (used.Contains(i))
                        continue;

                    var score = WordSimilarity(word, longer[i]);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestScore < WordMatch)
                    return null;

                used.Add(bestIndex);
                scores.Add(bestScore);
            }

            var coverage = (double)shorter.Count / longer.Count;
            var score100 = Math.Min(
                97,
                (int)Math.Round(scores.Average() * (0.6 + 0.4 * coverage) * 100));

            return new NameMatch(score100, false, listedName, isAlias, shorter.Count, longer.Count);
        }

        public static List<string> Words(string? name, bool company = false) =>
            SanctionsListParser.Normalise(name)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2 && !Honorifics.Contains(w) && !(company && CompanyWords.Contains(w)))
                .ToList();

        /// <summary>
        /// Compares a date of birth with what a listed entry gives,
        /// which may be several dates, a year, "Approximately 1968" or
        /// "Between 1975 and 1978". A full date must be the same day; an
        /// approximate year allows two years either way.
        /// </summary>
        private static Check DateOfBirth(DateTime? subject, string? listed)
        {
            if (subject == null || string.IsNullOrWhiteSpace(listed))
            {
                return new Check(
                    Agreement.NotAvailable,
                    subject == null
                        ? "Date of birth: not available for this person"
                        : "Date of birth: not given in the listed entry");
            }

            var dates = new List<DateTime>();
            var years = new List<(int From, int To)>();
            var text = listed;

            foreach (Match m in Regex.Matches(text, @"\b(\d{1,2})\s+([A-Za-z]{3})[A-Za-z]*\.?\s+(\d{4})\b"))
            {
                if (DateTime.TryParseExact(
                        $"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}",
                        "d MMM yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    dates.Add(date);
                }
            }

            var rest = Regex.Replace(text, @"\b\d{1,2}\s+[A-Za-z]{3}[A-Za-z]*\.?\s+\d{4}\b", " ");

            foreach (Match m in Regex.Matches(rest, @"[Bb]etween\s+(\d{4})\s+and\s+(\d{4})"))
                years.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));

            rest = Regex.Replace(rest, @"[Bb]etween\s+\d{4}\s+and\s+\d{4}", " ");

            foreach (Match m in Regex.Matches(rest, @"(?<approx>[Aa]pprox\w*\.?\s+)?\b(?<year>(?:19|20)\d{2})\b"))
            {
                var year = int.Parse(m.Groups["year"].Value);
                var spread = m.Groups["approx"].Success ? 2 : 0;
                years.Add((year - spread, year + spread));
            }

            if (dates.Count == 0 && years.Count == 0)
                return new Check(Agreement.NotAvailable, "Date of birth: the listed entry's could not be read");

            var given = subject.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

            if (dates.Any(d => d.Date == subject.Value.Date))
                return new Check(Agreement.Same, $"Date of birth: the same ({given})");

            if (years.Any(y => subject.Value.Year >= y.From && subject.Value.Year <= y.To))
                return new Check(Agreement.Consistent, $"Date of birth: consistent ({given}; listed: {listed})");

            return new Check(Agreement.Different, $"Date of birth: different ({given}; listed: {listed})");
        }

        private static Check Nationality(string? subject, string? listed)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(listed))
            {
                return new Check(
                    Agreement.NotAvailable,
                    string.IsNullOrWhiteSpace(subject)
                        ? "Nationality: not available for this person"
                        : "Nationality: not given in the listed entry");
            }

            static string Stem(string value)
            {
                var letters = SanctionsListParser.Normalise(value).Replace(" ", string.Empty);
                return letters.Length > 5 ? letters[..5] : letters;
            }

            var countries = Regex.Split(listed, @"\b[a-z]\)|,|;|\band\b")
                .Select(c => c.Trim())
                .Where(c => c.Length > 2)
                .Select(Stem)
                .ToHashSet();

            return countries.Contains(Stem(subject))
                ? new Check(Agreement.Same, $"Nationality: the same ({subject})")
                : new Check(Agreement.Different, $"Nationality: different ({subject}; listed: {listed})");
        }

        private static string Alphanumeric(string? value) =>
            new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

        /// <summary>
        /// How alike two words are, 0 to 1. Two spellings of the same
        /// transliterated name (Muttaqi and Motaqi, Mansoor and Mansur)
        /// count as nearly the same word.
        /// </summary>
        public static double WordSimilarity(string a, string b)
        {
            if (a == b)
                return 1;

            var similarity = JaroWinkler(a, b);

            return a.Length >= 3 && b.Length >= 3 && Transliteration(a) == Transliteration(b)
                ? Math.Max(similarity, 0.95)
                : similarity;
        }

        /// <summary>
        /// A word reduced to how it sounds once written in Latin
        /// letters: the vowel and consonant pairs that different
        /// transliterations swap are made the same, and doubled letters
        /// single.
        /// </summary>
        public static string Transliteration(string word)
        {
            var s = word
                .Replace("KH", "K")
                .Replace("GH", "G")
                .Replace("PH", "F")
                .Replace("CK", "K")
                .Replace("Q", "K")
                .Replace("OO", "U")
                .Replace("OU", "U")
                .Replace("EE", "I")
                .Replace("O", "U")
                .Replace("E", "I")
                .Replace("Y", "I");

            var single = new StringBuilder(s.Length);

            foreach (var c in s)
            {
                if (single.Length == 0 || single[^1] != c)
                    single.Append(c);
            }

            return single.Length > 1 && single[^1] == 'H' ? single.ToString(0, single.Length - 1) : single.ToString();
        }

        /// <summary>
        /// Jaro-Winkler similarity, 0 to 1: how alike two words are,
        /// weighting the start of the word, which is where
        /// transliterations of the same name agree most.
        /// </summary>
        public static double JaroWinkler(string a, string b)
        {
            if (a == b)
                return 1;

            if (a.Length == 0 || b.Length == 0)
                return 0;

            var range = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
            var aMatched = new bool[a.Length];
            var bMatched = new bool[b.Length];
            var matches = 0;

            for (var i = 0; i < a.Length; i++)
            {
                var from = Math.Max(0, i - range);
                var to = Math.Min(i + range + 1, b.Length);

                for (var j = from; j < to; j++)
                {
                    if (bMatched[j] || a[i] != b[j])
                        continue;

                    aMatched[i] = bMatched[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0)
                return 0;

            var transpositions = 0;
            var k = 0;

            for (var i = 0; i < a.Length; i++)
            {
                if (!aMatched[i])
                    continue;

                while (!bMatched[k])
                    k++;

                if (a[i] != b[k])
                    transpositions++;

                k++;
            }

            var m = (double)matches;
            var jaro = (m / a.Length + m / b.Length + (m - transpositions / 2d) / m) / 3;

            var prefix = 0;

            while (prefix < Math.Min(4, Math.Min(a.Length, b.Length)) && a[prefix] == b[prefix])
                prefix++;

            return jaro + prefix * 0.1 * (1 - jaro);
        }
    }
}
