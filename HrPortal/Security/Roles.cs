namespace HrPortal.Security
{
    public static class Roles
    {
        public const string Administrator = "Administrator";

        public const string HrOfficer = "HR Officer";

        public const string Md = "MD";

        public const string Employee = "Employee";

        /// <summary>
        /// Looks after sanctions screening. Nobody else sees it, not
        /// even administrators, unless they are given this role too.
        /// </summary>
        public const string ComplianceOfficer = "Compliance Officer";

        public const string ComplianceOnly = ComplianceOfficer;

        /// <summary>
        /// Sees every leave application at the company, whatever its
        /// status, and can download the forms, but decides nothing and
        /// changes nothing.
        /// </summary>
        public const string LeaveViewer = "Leave Viewer";

        /// <summary>
        /// Sees every leave balance and every leave, loan and course
        /// application at the company from the moment it is sent,
        /// whoever it is waiting on, but decides and changes nothing.
        /// </summary>
        public const string RecordsViewer = "Records Viewer";

        public const string AdminOnly = Administrator;

        public const string AdminOrHr =
            Administrator + "," + HrOfficer;

        public const string HrOnly = HrOfficer;

        public const string HrOrMd = HrOfficer + "," + Md;

        public const string LeaveRecords = HrOrMd + "," + LeaveViewer + "," + RecordsViewer;

        public const string BalanceRecords = HrOfficer + "," + RecordsViewer;

        public const string AnyManager =
            Administrator + "," + HrOfficer + "," + Md;

        public const string AdminOrMd =
            Administrator + "," + Md;

        public const string MdOnly = Md;

        /// <summary>
        /// Whether a role is held at one company at a time. Only the
        /// administrator, who looks after every company, and the
        /// baseline employee role hold everywhere; somebody can be HR
        /// at one company and an ordinary employee at another.
        /// </summary>
        public static bool IsCompanyRole(string? role) =>
            !string.Equals(role, Administrator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, Employee, StringComparison.OrdinalIgnoreCase);
    }
}
