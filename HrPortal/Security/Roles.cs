namespace HrPortal.Security
{
    public static class Roles
    {
        public const string Administrator = "Administrator";

        public const string HrOfficer = "HR Officer";

        public const string Md = "MD";

        public const string Employee = "Employee";

        public const string AdminOnly = Administrator;

        public const string AdminOrHr =
            Administrator + "," + HrOfficer;

        public const string HrOnly = HrOfficer;

        public const string HrOrMd = HrOfficer + "," + Md;

        /// <summary>
        /// Anyone who gets a reports section. Each report is still
        /// restricted to the roles that may see its underlying data.
        /// </summary>
        public const string AnyManager =
            Administrator + "," + HrOfficer + "," + Md;

        public const string AdminOrMd =
            Administrator + "," + Md;

        public const string MdOnly = Md;
    }
}
