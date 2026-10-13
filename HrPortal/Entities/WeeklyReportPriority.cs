namespace HrPortal.Entities
{
    /// <summary>
    /// One line under "Priorities for the Next Reporting Period".
    /// These are what the following week's draft starts from.
    /// </summary>
    public class WeeklyReportPriority
    {
        public int Id { get; set; }

        public int WeeklyReportId { get; set; }

        public int Order { get; set; }

        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Set when the line was brought forward from the previous
        /// week rather than written fresh.
        /// </summary>
        public bool CarriedForward { get; set; }

        public WeeklyReport Report { get; set; } = null!;
    }
}
