namespace HrPortal.Entities
{
    public class TrainingMaterial
    {
        public int Id { get; set; }

        public int TrainingCourseId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; }

        public int UploadedByEmployeeId { get; set; }

        public TrainingCourse TrainingCourse { get; set; } = null!;

        public Employee UploadedByEmployee { get; set; } = null!;
    }
}
