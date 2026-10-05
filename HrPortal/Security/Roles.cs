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

        public const string AdminOrHrOrMd =
            Administrator + "," + HrOfficer + "," + Md;

        public const string MdOnly = Md;
    }
}
