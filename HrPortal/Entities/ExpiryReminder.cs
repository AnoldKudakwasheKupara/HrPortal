namespace HrPortal.Entities
{
    /// <summary>
    /// What is running out.
    /// </summary>
    public enum ExpiryReminderKind
    {
        Licence,

        Certification
    }

    /// <summary>
    /// One expiry reminder already sent.
    ///
    /// A licence or certification is reminded about twice, a month
    /// out and a week out. The job that sends them runs every
    /// morning and again whenever the portal restarts, so this is
    /// how it knows a reminder has already gone. The expiry date is
    /// part of it: once a licence is renewed to a new date, the
    /// reminders for that date go out in their turn.
    /// </summary>
    public class ExpiryReminder
    {
        public int Id { get; set; }

        public ExpiryReminderKind Kind { get; set; }

        /// <summary>
        /// The licence or certification, by its own id. Not a foreign
        /// key, since it points at one of two tables.
        /// </summary>
        public int RecordId { get; set; }

        public DateTime ExpiryDate { get; set; }

        /// <summary>
        /// Which of the two reminders this was: 30 or 7.
        /// </summary>
        public int DaysBefore { get; set; }

        public DateTime SentOn { get; set; }
    }
}
