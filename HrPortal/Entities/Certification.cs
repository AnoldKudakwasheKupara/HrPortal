using HrPortal.Security;

namespace HrPortal.Entities
{
    /// <summary>
    /// A qualification somebody holds: first aid, fire marshal, a trade
    /// certificate, a professional membership.
    ///
    /// Training recorded that a course was assigned and completed, but
    /// not that the person now holds something, nor that it stops being
    /// valid. "Who has a current first-aid certificate" was
    /// unanswerable.
    /// </summary>
    public class Certification : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Who awarded it, where that matters.</summary>
        public string? IssuedBy { get; set; }

        /// <summary>The certificate or membership number.</summary>
        public string? Reference { get; set; }

        public DateTime IssuedOn { get; set; }

        /// <summary>
        /// Null for a qualification that does not lapse. Where it is
        /// set, the reminder run warns before it does.
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// The course that led to it, when it came from training
        /// recorded in the portal.
        /// </summary>
        public int? TrainingCourseId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public TrainingCourse? TrainingCourse { get; set; }
    }
}
