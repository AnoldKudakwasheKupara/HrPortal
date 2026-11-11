namespace HrPortal.ViewModels
{
    /// <summary>
    /// The same four groups for every kind of request an employee makes
    /// (leave, courses, loans): everything, still in progress, approved,
    /// and rejected or cancelled.
    /// </summary>
    public static class RequestGroups
    {
        public const string All = "all";
        public const string Open = "open";
        public const string Approved = "approved";
        public const string Closed = "closed";

        public static string Normalise(string? show) =>
            show is Open or Approved or Closed ? show : All;

        public static bool In(string show, bool isFinal, bool isApproved) => show switch
        {
            Open => !isFinal,
            Approved => isApproved,
            Closed => isFinal && !isApproved,
            _ => true
        };

        public static List<ListTab> Tabs<T>(
            IReadOnlyCollection<T> items,
            Func<T, bool> isFinal,
            Func<T, bool> isApproved,
            string allText)
        {
            int Count(string show) => items.Count(i => In(show, isFinal(i), isApproved(i)));

            return new List<ListTab>
            {
                new(All, allText, "fa-list-ul", "is-teal", items.Count),
                new(Open, "In Progress", "fa-hourglass-half", "is-blue", Count(Open)),
                new(Approved, "Approved", "fa-circle-check", "is-green", Count(Approved)),
                new(Closed, "Rejected / Cancelled", "fa-circle-xmark", "is-amber", Count(Closed))
            };
        }

        public static PagedResult<T> Page<T>(
            IReadOnlyCollection<T> items,
            Func<T, bool> isFinal,
            Func<T, bool> isApproved,
            string show,
            int page) =>
            PagedList.Of(items.Where(i => In(show, isFinal(i), isApproved(i))).ToList(), page);
    }
}
