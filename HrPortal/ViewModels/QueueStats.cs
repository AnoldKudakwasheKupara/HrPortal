namespace HrPortal.ViewModels
{
    /// <summary>
    /// The four summary cards every approval queue shows: how many are
    /// waiting, a total that matters for that queue, how long the oldest
    /// has waited, and one more figure the queue chooses.
    /// </summary>
    public sealed record QueueStats(
        int Waiting,
        string TotalLabel,
        string TotalValue,
        DateTime? OldestSince,
        string ExtraLabel,
        string ExtraValue,
        string ExtraIcon = "fa-calendar-check")
    {
        /// <summary>Whole days since the oldest arrived, or null when nothing is waiting.</summary>
        public int? OldestDays(DateTime now) =>
            OldestSince == null ? null : Math.Max(0, (int)(now - OldestSince.Value).TotalDays);
    }
}
