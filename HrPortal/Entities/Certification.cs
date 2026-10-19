using HrPortal.Security;

namespace HrPortal.Entities
{
    public class Certification : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? IssuedBy { get; set; }

        public string? Reference { get; set; }

        public DateTime IssuedOn { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public int? TrainingCourseId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public TrainingCourse? TrainingCourse { get; set; }
    }
}
