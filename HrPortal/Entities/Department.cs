namespace HrPortal.Entities
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// The company it belongs to, or null for a department that
        /// serves the whole group, such as IT.
        /// </summary>
        public int? CompanyId { get; set; }

        public Company? Company { get; set; }


        public ICollection<Employee> Employees { get; set; }
            = new List<Employee>();
    }
}
