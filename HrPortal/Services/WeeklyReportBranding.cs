namespace HrPortal.Services
{
    /// <summary>
    /// What the weekly report calls itself and the company.
    ///
    /// Override any of it under the "WeeklyReport" section in
    /// configuration; the defaults here are what prints when nobody
    /// does, so they name this company rather than the one the
    /// template came from.
    /// </summary>
    public class WeeklyReportBranding
    {
        public const string Section = "WeeklyReport";

        public string OrganisationName { get; set; } =
            "Intellego Investment Consultants (Pvt) Ltd";

        public string DepartmentName { get; set; } = "IT Department";

        public string DocumentTitle { get; set; } = "WEEKLY STATUS REPORT";

        public string Subtitle { get; set; } = "INFRASTRUCTURE & TECHNOLOGY";

        public string Classification { get; set; } = "CONFIDENTIAL";
    }
}
