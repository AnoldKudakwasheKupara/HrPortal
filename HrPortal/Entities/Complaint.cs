namespace HrPortal.Entities
{
    /// <summary>
    /// A complaint from a client, kept in the company's complaints
    /// register with what was done about it.
    /// </summary>
    public class Complaint : ICompanyOwned
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        /// <summary>When it came in. Older entries do not always say.</summary>
        public DateTime? ReceivedOn { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string? ClientPhone { get; set; }

        public string? ClientEmail { get; set; }

        /// <summary>What the client complained about.</summary>
        public string? Nature { get; set; }

        /// <summary>The staff member or members it concerns, as written.</summary>
        public string? StaffInvolved { get; set; }

        public string? ActionTaken { get; set; }

        public DateTime? ResolvedOn { get; set; }

        public ComplaintStatus Status { get; set; } = ComplaintStatus.Pending;

        public string? Remarks { get; set; }

        public DateTime RecordedAt { get; set; }

        public string? RecordedBy { get; set; }
    }

    public enum ComplaintStatus
    {
        Pending = 0,
        Resolved = 1
    }
}
