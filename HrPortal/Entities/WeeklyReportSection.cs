namespace HrPortal.Entities
{
    /// <summary>
    /// A numbered section of the weekly report. Bullets are held as
    /// one block of text, one bullet per line, which is how they are
    /// edited and how they are rendered.
    /// </summary>
    public class WeeklyReportSection
    {
        public int Id { get; set; }

        public int WeeklyReportId { get; set; }

        public int Order { get; set; }

        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// The named workstream shown above the bullets, as in
        /// "Sophos Firewall Implementation". Left empty for sections
        /// that are a plain list.
        /// </summary>
        public string? Workstream { get; set; }

        public WorkstreamStatus WorkstreamStatus { get; set; }
            = WorkstreamStatus.None;

        public string? Body { get; set; }

        public WeeklyReport Report { get; set; } = null!;
    }
}
