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
            var isNotice = review.NewDocument.Kind != SanctionsDocumentKind.FullList;

            if (review.NewDocument.EntryCount == 0 && review.Inconclusive)
            {
                sentences.Add("No names could be read from the new document, so it could not be compared.");
            }
            else if (review.PreviousDocument == null && !isNotice)
            {
                sentences.Add(
                    "This is the first full list of its kind uploaded, so there was nothing to compare it " +
                    "with. It is the starting point for the next comparison.");
            }
            else if (added + removed + changed == 0)
            {
                sentences.Add(isNotice
                    ? "The names this notice changes already read the same in the earlier version uploaded."
                    : "No names were added, removed or amended between the two documents.");
            }
            else
            {
                var text = $"{Names(added, "added")}, {Names(removed, "removed")} and {Names(changed, "amended")}";

                text += isNotice || review.UnchangedCount == 0
                    ? "."
                    : $"; {Names(review.UnchangedCount, "unchanged", "is", "are")}.";

                sentences.Add(char.ToUpperInvariant(text[0]) + text[1..]);
            }

            if (isNotice)
            {
                sentences.Add(
                    $"The new document is a {SanctionsText.Kind(review.NewDocument.Kind).ToLowerInvariant()}: " +
                    "it only speaks for the names it gives. Names it does not mention are not affected and " +
                    "were not counted as removed.");
            }

            if (review.Inconclusive)
            {
                sentences.Add(review.NotComparedCount > 0
                    ? $"The comparison is incomplete: {Names(review.NotComparedCount, "compared with anything", "could not be", "could not be")}."
                    : "The comparison is incomplete: part of it could not be worked out. See the discrepancies.");
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

                var all = review.Concerns
                    .Where(c => c.Category == SanctionsConcernCategory.PossibleMatch)
                    .ToList();

                var found = all.Where(c => c.Strength != SanctionsMatchStrength.SetAside).ToList();
                var setAside = all.Count - found.Count;
                var exact = found.Count(c => c.Strength == SanctionsMatchStrength.Exact);

                var carried = found.Count(c => c.ResolvedByName == SanctionsService.CarriedForwardBy);
                var fresh = found.Count - carried;

                var against = isNotice
                    ? $"the {review.NewDocument.EntryCount} name{(review.NewDocument.EntryCount == 1 ? "" : "s")} this notice gives"
                    : "the new document";

                if (found.Count == 0)
                {
                    sentences.Add($"None of the {screened} staff, vendors and clients checked matches {against}.");
                }
                else
                {
                    var text = (found.Count == 1 ? "1 possible match was" : $"{found.Count} possible matches were") +
                               $" found among the {screened} staff, vendors and clients checked against {against}" +
                               (exact == 0 ? "" : $" ({exact} exact)");

                    text += fresh == 0
                        ? found.Count == 1
                            ? "; it was cleared in an earlier review and nothing about it has changed."
                            : "; all were cleared in an earlier review and nothing about them has changed."
                        : carried == 0
                            ? "."
                            : $"; {carried} {(carried == 1 ? "was" : "were")} cleared in an earlier review and " +
                              $"{(carried == 1 ? "has" : "have")} not changed, {fresh} {(fresh == 1 ? "needs" : "need")} looking into.";

                    sentences.Add(text);
                }

                if (setAside > 0)
                {
                    sentences.Add(
                        $"{setAside} similar name{(setAside == 1 ? " was" : "s were")} set aside because the date of " +
                        "birth differs; they are listed with the results and can be reopened.");
                }

                if (review.ClientListId == null)
                    sentences.Add("No client list had been uploaded, so no clients were checked.");

                sentences.Add(
                    $"This covers only {against}, checked on {review.CreatedAt.ToLocalTime():d MMMM yyyy}. " +
                    "It is not a finding about anybody beyond that.");
            }

            return sentences;
        }

        private static string Names(int n, string what, string one = "was", string many = "were") =>
            n switch
            {
                0 => $"no names {many} {what}",
                1 => $"1 name {one} {what}",
                _ => $"{n} names {many} {what}"
            };
    }
}
