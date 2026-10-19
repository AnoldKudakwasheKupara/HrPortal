namespace HrPortal.Entities
{
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
