using HrPortal.Security;

namespace HrPortal.Entities
{
    /// <summary>
    /// An application to study a professional programme, such as a
    /// CFA level. Human Resources reviews it and the Managing
    /// Consultant gives the final word.
    /// </summary>
    public class CourseApplication : IEmployeeOwned
    {
        public int Id { get; set; }

        public string ApplicationNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public CourseApplicationStatus Status { get; set; }
            = CourseApplicationStatus.Draft;

        /// <summary>The programme and level applied for.</summary>
        public string ProgramIntended { get; set; } = string.Empty;

        public string Justification { get; set; } = string.Empty;

        public bool HasSatBefore { get; set; }

        public CourseSitting Sitting { get; set; }
            = CourseSitting.FirstAttempt;

        public DateTime? TargetExamDate { get; set; }

        /// <summary>
        /// When the applicant made the pledge on the form. Stands in
        /// place of their signature, so an application cannot be sent
        /// without it.
        /// </summary>
        public DateTime? PledgedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public ICollection<CourseApplicationHistory> History
        { get; set; } = new List<CourseApplicationHistory>();
    }
}
