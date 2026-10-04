namespace HrPortal.Security
{
    /// <summary>
    /// The portal's four roles, and the combinations used on
    /// [Authorize]. Kept in one place so a rename is a single
    /// edit rather than a search through every controller.
    /// </summary>
    public static class Roles
    {
        public const string Administrator = "Administrator";

        public const string HrOfficer = "HR Officer";

        public const string Md = "MD";

        public const string Employee = "Employee";


        /// <summary>
        /// System administration: structure, roles and settings.
        /// </summary>
        public const string AdminOnly = Administrator;

        /// <summary>
        /// HR administration: employee records, documents,
        /// training and leave review. The Administrator is
        /// included so the system can always be run by one person
        /// in a small organisation.
        /// </summary>
        public const string AdminOrHr =
            Administrator + "," + HrOfficer;

        /// <summary>
        /// Anyone who takes part in the leave approval chain.
        /// Individual actions are restricted further.
        /// </summary>
        public const string AdminOrHrOrMd =
            Administrator + "," + HrOfficer + "," + Md;

        /// <summary>
        /// Final leave approval.
        /// </summary>
        public const string MdOnly = Md;
    }
}
