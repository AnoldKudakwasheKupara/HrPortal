namespace HrPortal.Entities
{
    public class Position
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? DepartmentId { get; set; }

        public int? Establishment { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public Department? Department { get; set; }

        public ICollection<Employee> Employees { get; set; }
            = new List<Employee>();
    }
}
