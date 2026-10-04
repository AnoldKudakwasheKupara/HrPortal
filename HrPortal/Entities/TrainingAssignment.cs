using HrPortal.Security;

namespace HrPortal.Entities
{
    public class TrainingAssignment : IEmployeeOwned
    {
        public int Id { get; set; }

        public int TrainingCourseId { get; set; }

        public int EmployeeId { get; set; }

        public DateTime? DueDate { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime AssignedAt { get; set; }


        public TrainingCourse TrainingCourse { get; set; } = null!;

        public Employee Employee { get; set; } = null!;

        public TrainingFeedback? Feedback { get; set; }
    }
}
