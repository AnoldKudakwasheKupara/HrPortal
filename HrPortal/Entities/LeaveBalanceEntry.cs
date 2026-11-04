namespace HrPortal.Entities
{
    public enum LeaveBalanceEntryKind
    {
        /// <summary>The balance was set from an uploaded file.</summary>
        Opening,

        /// <summary>The month's days were added.</summary>
        Earned,

        /// <summary>Approved leave was deducted.</summary>
        Taken,

        /// <summary>HR changed the figure by hand.</summary>
        Adjusted
    }

    /// <summary>
    /// One change to an employee's running leave balance, kept so any
    /// figure can be explained line by line.
    /// </summary>
    public class LeaveBalanceEntry
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public int LeaveTypeId { get; set; }

        public DateTime EntryDate { get; set; }

        public LeaveBalanceEntryKind Kind { get; set; }

        /// <summary>Added or, when negative, taken away. For an opening, the figure set.</summary>
        public decimal Days { get; set; }

        /// <summary>The balance after this change, not counting leave still awaiting approval.</summary>
        public decimal BalanceAfter { get; set; }

        public string? Note { get; set; }

        /// <summary>
        /// The month earned, as yyyy-MM, on Earned entries only. Unique
        /// per employee and leave type, so a month is never added twice.
        /// </summary>
        public string? Period { get; set; }

        public Employee Employee { get; set; } = null!;

        public LeaveType LeaveType { get; set; } = null!;
    }
}
