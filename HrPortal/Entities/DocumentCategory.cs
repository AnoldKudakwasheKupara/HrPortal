namespace HrPortal.Entities
{
    public class DocumentCategory
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsRestricted { get; set; }

        public ICollection<EmployeeDocument> Documents { get; set; }
            = new List<EmployeeDocument>();
    }
}
