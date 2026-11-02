using HrPortal.Entities;

namespace HrPortal.Services.Sanctions
{
    /// <summary>
    /// What a review found, in plain sentences, said the same way on
    /// the screen and in the report.
    /// </summary>
    public static class SanctionsFindings
    {
        public static List<string> Sentences(SanctionsReview review)
        {
            var sentences = new List<string>();

            var added = review.Changes.Count(c => c.Type == SanctionsChangeType.Added);
            var removed = review.Changes.Count(c => c.Type == SanctionsChangeType.Removed);
            var changed = review.Changes.Count(c => c.Type == SanctionsChangeType.Changed);

            if (review.PreviousDocument == null)
            {
                sentences.Add(
                    "This is the first document, so there was nothing to compare it with. " +
                    "It is the starting point for the next comparison.");
            }
            else if (added + removed + changed == 0)
            {
                sentences.Add("No names were added, removed or changed between the two documents.");
            }
            else
            {
                var text = $"{Names(added, "added")}, {Names(removed, "removed")} and {Names(changed, "changed")}.";

                sentences.Add(char.ToUpperInvariant(text[0]) + text[1..]);
            }

            var discrepancies = review.Concerns.Count(c => c.Category == SanctionsConcernCategory.Discrepancy);

            sentences.Add(discrepancies == 0
                ? "No discrepancies were found."
                : discrepancies == 1
                    ? "1 discrepancy was found."
                    : $"{discrepancies} discrepancies were found.");

            if (review.Screened)
            {
                var screened = review.ScreenedStaff + review.ScreenedVendors + review.ScreenedClients;

                var found = review.Concerns
                    .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                    .ToList();

                var carried = found.Count(c => c.ResolvedByName == SanctionsService.CarriedForwardBy);
                var fresh = found.Count - carried;

                if (found.Count == 0)
                {
                    sentences.Add($"None of the {screened} staff, vendors and clients checked is on the new document.");
                }
                else
                {
                    var text = (found.Count == 1 ? "1 possible match was" : $"{found.Count} possible matches were") +
                               $" found among the {screened} staff, vendors and clients checked";

                    text += fresh == 0
                        ? found.Count == 1
                            ? "; it was looked into in an earlier review and that decision still stands."
                            : "; all were looked into in an earlier review and those decisions still stand."
                        : carried == 0
                            ? "."
                            : $"; {carried} {(carried == 1 ? "was" : "were")} looked into in an earlier review, " +
                              $"{fresh} {(fresh == 1 ? "is" : "are")} new.";

                    sentences.Add(text);
                }
            }

            return sentences;
        }

        private static string Names(int n, string what) =>
            n switch
            {
                0 => $"no names were {what}",
                1 => $"1 name was {what}",
                _ => $"{n} names were {what}"
            };
    }
}
