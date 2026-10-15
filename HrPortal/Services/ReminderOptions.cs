namespace HrPortal.Services
{
    /// <summary>
    /// When the daily reminder run happens and how far ahead it looks.
    /// </summary>
    public class ReminderOptions
    {
        public const string Section = "Reminders";

        public bool Enabled { get; set; } = true;

        /// <summary>Hour of the day, local time, to run at.</summary>
        public int RunAtHour { get; set; } = 7;

        /// <summary>
        /// How far ahead an expiry has to be before it is worth
        /// warning about.
        /// </summary>
        public int ExpiryWindowDays { get; set; } = 30;

        /// <summary>
        /// How long before the same reminder is sent again. Daily
        /// repetition trains people to ignore them.
        /// </summary>
        public int RepeatEveryDays { get; set; } = 7;

        /// <summary>
        /// How long a request may sit with an approver before they are
        /// reminded it is there.
        /// </summary>
        public int StaleApprovalDays { get; set; } = 3;
    }
}
