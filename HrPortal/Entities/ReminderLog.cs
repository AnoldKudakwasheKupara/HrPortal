namespace HrPortal.Entities
{
    /// <summary>
    /// A record that a particular reminder has already gone to a
    /// particular person about a particular thing.
    ///
    /// Without this the daily run would send the same warning every
    /// morning for weeks, and people stop reading reminders that
    /// repeat. A reminder is only sent again once the repeat interval
    /// has passed.
    /// </summary>
    public class ReminderLog
    {
        public int Id { get; set; }

        /// <summary>What the reminder is about, such as LicenceExpiry.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the thing being warned about within that kind,
        /// normally its id.
        /// </summary>
        public string ReferenceKey { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public DateTime LastSentOn { get; set; }

        public Employee Employee { get; set; } = null!;
    }
}
