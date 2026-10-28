namespace HrPortal.Entities
{
    /// <summary>
    /// What a reminder was about.
    /// </summary>
    public enum TrainingReminderKind
    {
        /// <summary>
        /// The trainer has content to post and a date to post it by.
        /// </summary>
        Content,

        /// <summary>
        /// The session itself is coming up.
        /// </summary>
        Session
    }

    /// <summary>
    /// One reminder already sent.
    ///
    /// Reminders go out daily, and the job that sends them runs
    /// again whenever the portal restarts. Without a record of what
    /// has gone, a morning with three restarts in it would send
    /// everybody the same reminder three times.
    /// </summary>
    public class TrainingReminder
    {
        public int Id { get; set; }

        public TrainingReminderKind Kind { get; set; }

        public int TrainingCourseId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>
        /// The day it went, not the moment: the question asked of
        /// this table is "has this person already been told today".
        /// </summary>
        public DateTime SentOn { get; set; }

        public TrainingCourse TrainingCourse { get; set; } = null!;

        public Employee Employee { get; set; } = null!;
    }
}
