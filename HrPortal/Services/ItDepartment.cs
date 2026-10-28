namespace HrPortal.Services
{
    /// <summary>
    /// Which department is the IT department.
    ///
    /// There is no flag on a department saying so, only its name, and
    /// companies spell it a dozen ways. The seeder already knew this
    /// list; the support page needs the same answer, and two lists
    /// that drift apart would mean the help page naming one set of
    /// people and the seeder filing the administrator under another.
    /// </summary>
    public static class ItDepartment
    {
        private static readonly HashSet<string> Names = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "IT", "ICT", "I.T.", "I.T", "IT DEPARTMENT", "ICT DEPARTMENT",
            "INFORMATION TECHNOLOGY",
            "INFORMATION TECHNOLOGY DEPARTMENT",
            "INFORMATION AND COMMUNICATION TECHNOLOGY",
            "INFORMATION & COMMUNICATION TECHNOLOGY",
            "INFORMATION COMMUNICATION TECHNOLOGY",
            "IT SUPPORT", "IT SERVICES", "TECHNOLOGY"
        };

        public static bool Matches(string? departmentName) =>
            !string.IsNullOrWhiteSpace(departmentName) &&
            Names.Contains(departmentName.Trim());

        /// <summary>
        /// The same list, for callers that have to compare in the
        /// database rather than in memory.
        /// </summary>
        public static IReadOnlyCollection<string> All => Names;
    }
}
