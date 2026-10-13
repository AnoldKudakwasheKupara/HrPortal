namespace HrPortal.Entities
{
    /// <summary>
    /// One piece of IT work: a request, an incident or a task, logged
    /// as it happens so the weekly report can be assembled from the
    /// record rather than from memory.
    /// </summary>
    public class ItActivity
    {
        public int Id { get; set; }

        public DateTime LoggedOn { get; set; }

        public ItActivityCategory Category { get; set; }

        public ItActivityStatus Status { get; set; }
            = ItActivityStatus.Open;

        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// What was actually done. This is what the weekly report
        /// draws on, so it is worth a sentence rather than a word.
        /// </summary>
        public string? ActionTaken { get; set; }

        /// <summary>
        /// The employee who raised it, when it came from someone on
        /// the staff register.
        /// </summary>
        public int? RaisedByEmployeeId { get; set; }

        /// <summary>
        /// Used when the request came from outside the register, or
        /// the work was not raised by anyone in particular.
        /// </summary>
        public string? RaisedByOther { get; set; }

        /// <summary>
        /// The machine involved, when the work was against one on the
        /// asset register.
        /// </summary>
        public int? AssetId { get; set; }

        public DateTime? ResolvedOn { get; set; }

        /// <summary>
        /// Whether this is worth calling out by name in the weekly
        /// report rather than only counting.
        /// </summary>
        public bool IsNotable { get; set; }

        public string? LoggedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public Employee? RaisedByEmployee { get; set; }

        public Asset? Asset { get; set; }
    }
}
