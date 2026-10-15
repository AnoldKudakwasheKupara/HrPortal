namespace HrPortal.Entities
{
    public class DocumentCategory
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Documents in this category are for HR only and are hidden
        /// from the employee they are about. Disciplinary and
        /// performance records are the reason this exists: the person
        /// concerned should not read them from their own profile.
        /// </summary>
        public bool IsRestricted { get; set; }


        public ICollection<EmployeeDocument> Documents { get; set; }
            = new List<EmployeeDocument>();
    }
}
