namespace HrPortal.Entities
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Whether staff in this department log what they work on and
        /// how long it takes. Meant for service teams such as IT.
        /// </summary>
        public bool TracksTimesheets { get; set; }

        public ICollection<Employee> Employees { get; set; }
            = new List<Employee>();
    }
}
