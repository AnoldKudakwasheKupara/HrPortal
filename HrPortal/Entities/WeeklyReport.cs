namespace HrPortal.Entities
{
    public class WeeklyReport
    {
        public int Id { get; set; }

        /// <summary>
        /// Whose report this is. Each person writes their own; only
        /// the IT department reports made before that have none.
        /// </summary>
        public int? EmployeeId { get; set; }

        public Employee? Employee { get; set; }

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
