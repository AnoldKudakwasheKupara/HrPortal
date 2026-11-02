namespace HrPortal.Services.Sanctions
{
    /// <summary>A person or company the company deals with.</summary>
    public sealed record ScreeningSubject(
        string Type,
        int? Id,
        string Name,
        string? IdNumber);

    /// <summary>One listed party, in the form screening needs.</summary>
    public sealed record ScreeningTarget(
        string Reference,
        string Name,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> Identifiers);

    public sealed record ScreeningHit(int Score, string Reason);

    /// <summary>
    /// Decides whether somebody resembles a listed party.
    ///
    /// An identical passport or identity number is a match outright.
    /// Names are compared with every name and alias the entry gives,
    /// ignoring order, accents, punctuation and honorifics, and
    /// allowing for the different spellings a transliterated name
    /// comes in (Mutaqi, Muttaqi): each word of the shorter name must
    /// closely match a word of the longer one. It errs towards
    /// raising a matter: a false alarm costs a few minutes, a missed
    /// match costs far more.
    /// </summary>
    public static class SanctionsMatcher
    {
        /// <summary>The score from which a matter is raised.</summary>
        public const int Threshold = 75;

        private const double WordMatch = 0.88;

        private static readonly HashSet<string> Honorifics = new(StringComparer.Ordinal)
        {
            "MR", "MRS", "MS", "MISS", "DR", "PROF", "SIR",
            "MULLAH", "MULLA", "MAULAVI", "MAULVI", "MAWLAWI", "HAJI", "HAJJI",
            "SHEIKH", "SHAIKH", "QARI", "MUFTI", "SAYED", "SAYYID"
        };

        public static ScreeningHit? Match(ScreeningSubject subject, ScreeningTarget target)
        {
            var subjectId = Alphanumeric(subject.IdNumber);

            if (subjectId.Length >= 6 &&
                target.Identifiers.Any(i => Alphanumeric(i) == subjectId))
            {
                return new ScreeningHit(
                    100,
                    $"ID number {subject.IdNumber} is the same as one given for the listed entry.");
            }

            var subjectWords = Words(subject.Name);

            if (subjectWords.Count == 0)
                return null;

            ScreeningHit? best = null;

            foreach (var variant in new[] { target.Name }.Concat(target.Aliases))
            {
                var hit = CompareNames(subjectWords, Words(variant), variant);

                if (hit != null && (best == null || hit.Score > best.Score))
                    best = hit;
            }

            return best is { Score: >= Threshold } ? best : null;
        }

        private static ScreeningHit? CompareNames(
            List<string> subject,
            List<string> listed,
            string listedName)
        {
            if (listed.Count == 0)
                return null;

            if (subject.OrderBy(w => w).SequenceEqual(listed.OrderBy(w => w)))
                return new ScreeningHit(98, $"Name is the same as \"{listedName}\".");

            var shorter = subject.Count <= listed.Count ? subject : listed;
            var longer = ReferenceEquals(shorter, subject) ? listed : subject;

            if (shorter.Count < 2)
                return null;

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

                    var score = JaroWinkler(word, longer[i]);

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

            return new ScreeningHit(
                score100,
                $"Name closely resembles \"{listedName}\" " +
                $"({shorter.Count} of {longer.Count} name words match).");
        }

        public static List<string> Words(string? name) =>
            SanctionsListParser.Normalise(name)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2 && !Honorifics.Contains(w))
                .ToList();

        private static string Alphanumeric(string? value) =>
            new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

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
