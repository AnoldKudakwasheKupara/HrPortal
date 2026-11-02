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

        public const string AdminOnly = Administrator;

        public const string AdminOrHr =
            Administrator + "," + HrOfficer;

        public const string HrOnly = HrOfficer;

        public const string HrOrMd = HrOfficer + "," + Md;

        public const string AnyManager =
            Administrator + "," + HrOfficer + "," + Md;

        public const string AdminOrMd =
            Administrator + "," + Md;

        public const string MdOnly = Md;
    }
}
