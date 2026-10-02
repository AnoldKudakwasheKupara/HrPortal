namespace HrPortal.Entities
{
    public class EmployeeDocument
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public int DocumentCategoryId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; }

        public int UploadedByEmployeeId { get; set; }

        public DateTime? ExpiryDate { get; set; }


        public Employee Employee { get; set; } = null!;

        public DocumentCategory DocumentCategory { get; set; } = null!;

        public Employee UploadedByEmployee { get; set; } = null!;
    }
}