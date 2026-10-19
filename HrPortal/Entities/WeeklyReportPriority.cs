namespace HrPortal.Entities
{
    public class WeeklyReportPriority
    {
        public int Id { get; set; }

        public int WeeklyReportId { get; set; }

        public int Order { get; set; }

        public string Text { get; set; } = string.Empty;

        public bool CarriedForward { get; set; }

        public WeeklyReport Report { get; set; } = null!;
    }
}
