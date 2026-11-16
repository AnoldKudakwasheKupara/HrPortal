namespace HrPortal.Entities
{
    /// <summary>
    /// One of the group's companies. Each has its own portal address
    /// and brand, matched on <see cref="Key"/>, and its own staff list,
    /// though somebody who works for two companies has one record that
    /// both portals share.
    /// </summary>
    public class Company
    {
        public const int IntellegoId = 1;

        public const int MmcId = 2;

        public int Id { get; set; }

        /// <summary>
        /// The brand key, the same as the file name in Brands.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public ICollection<EmployeeCompany> Members { get; set; }
            = new List<EmployeeCompany>();
    }

    /// <summary>
    /// Says that somebody works for a company, so they can sign in to
    /// its portal and appear on its staff list.
    /// </summary>
    public class EmployeeCompany
    {
        public int EmployeeId { get; set; }

        public Employee Employee { get; set; } = null!;

        public int CompanyId { get; set; }

        public Company Company { get; set; } = null!;
    }
}
