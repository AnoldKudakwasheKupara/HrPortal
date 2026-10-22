namespace HrPortal.Entities
{
    public class CourseApplicationHistory
    {
        public int Id { get; set; }

        public int CourseApplicationId { get; set; }

        public int ActionedByEmployeeId { get; set; }

        public CourseApplicationAction Action { get; set; }

        public CourseApplicationStatus PreviousStatus { get; set; }

        public CourseApplicationStatus NewStatus { get; set; }

        public string? Comment { get; set; }

        public DateTime ActionDate { get; set; }

        public CourseApplication CourseApplication { get; set; } = null!;

        public Employee ActionedByEmployee { get; set; } = null!;
    }
}
