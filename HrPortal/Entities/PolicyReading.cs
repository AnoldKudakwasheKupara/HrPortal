namespace HrPortal.Entities
{
    /// <summary>
    /// When somebody first opened a policy's document. It has to be
    /// opened before it can be acknowledged, and it stands beside the
    /// acknowledgement as part of the confirmation.
    /// </summary>
    public class PolicyReading
    {
        public int Id { get; set; }

        public int PolicyId { get; set; }

        public int EmployeeId { get; set; }

        public DateTime OpenedAt { get; set; }

        public Policy Policy { get; set; } = null!;

        public Employee Employee { get; set; } = null!;
    }
}
