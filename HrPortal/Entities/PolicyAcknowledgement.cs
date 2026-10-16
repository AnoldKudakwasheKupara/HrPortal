namespace HrPortal.Entities
{
    /// <summary>
    /// A record that somebody confirmed they have read a policy.
    ///
    /// Tied to the policy rather than the title, so a new version
    /// starts with nobody having acknowledged it: that is the point of
    /// reissuing one.
    /// </summary>
    public class PolicyAcknowledgement
    {
        public int Id { get; set; }

        public int PolicyId { get; set; }

        public int EmployeeId { get; set; }

        public DateTime AcknowledgedAt { get; set; }

        public Policy Policy { get; set; } = null!;

        public Employee Employee { get; set; } = null!;
    }
}
