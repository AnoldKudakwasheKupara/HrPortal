namespace HrPortal.Entities
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;


        public ICollection<Employee> Employees { get; set; }
            = new List<Employee>();
    }
}
