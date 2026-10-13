namespace HrPortal.Services
{
    /// <summary>
    /// The wording on the cover, header and footer of the weekly
    /// report, kept in configuration so the document can be issued
    /// under a different name without a rebuild.
    /// </summary>
    public class WeeklyReportBranding
    {
        public const string Section = "WeeklyReport";

        public string OrganisationName { get; set; } = "MMC Capital";

        public string DepartmentName { get; set; } = "IT Department";

        public string DocumentTitle { get; set; } = "WEEKLY STATUS REPORT";

        public string Subtitle { get; set; } = "INFRASTRUCTURE & TECHNOLOGY";

        public string Classification { get; set; } = "CONFIDENTIAL";
    }
}
