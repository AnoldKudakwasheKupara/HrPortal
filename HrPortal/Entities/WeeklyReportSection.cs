namespace HrPortal.Entities
{
    public class WeeklyReportSection
    {
        public int Id { get; set; }

        public int WeeklyReportId { get; set; }

        public int Order { get; set; }

        public string Heading { get; set; } = string.Empty;

        public string? Workstream { get; set; }

        public WorkstreamStatus WorkstreamStatus { get; set; }
            = WorkstreamStatus.None;

        public string? Body { get; set; }

        public WeeklyReport Report { get; set; } = null!;
    }
}
