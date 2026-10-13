namespace HrPortal.Entities
{
    /// <summary>
    /// One week's IT department status report. The sections and the
    /// priorities carry the content; the numbers quoted inside them
    /// are drawn from the activity log when the draft is opened.
    /// </summary>
    public class WeeklyReport
    {
        public int Id { get; set; }

        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        public WeeklyReportStatus Status { get; set; }
            = WeeklyReportStatus.Draft;

        public string PreparedBy { get; set; } = string.Empty;

        public string? ExecutiveOverview { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? FinalisedOn { get; set; }

        public ICollection<WeeklyReportSection> Sections { get; set; }
            = new List<WeeklyReportSection>();

        public ICollection<WeeklyReportPriority> Priorities { get; set; }
            = new List<WeeklyReportPriority>();
    }
}
