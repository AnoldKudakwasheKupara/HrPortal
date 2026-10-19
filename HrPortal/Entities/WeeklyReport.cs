namespace HrPortal.Entities
{
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
