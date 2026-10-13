namespace HrPortal.Entities
{
    /// <summary>
    /// A request to buy goods or services. Review comes first; the
    /// Managing Director sees it only when the estimated total reaches
    /// the configured threshold, or when the requester is the reviewer
    /// and so cannot approve it.
    /// </summary>
    public class Requisition
    {
        public int Id { get; set; }

        public string RequisitionNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public RequisitionStatus Status { get; set; }
            = RequisitionStatus.Draft;

        public string Title { get; set; } = string.Empty;

        public string? Justification { get; set; }

        public DateTime? NeededBy { get; set; }

        /// <summary>
        /// A suggested supplier from the vendor register. Not binding
        /// on whoever ends up placing the order.
        /// </summary>
        public int? VendorId { get; set; }

        /// <summary>
        /// Written down when the requisition is submitted, so a later
        /// change to the threshold cannot reroute something already in
        /// flight.
        /// </summary>
        public bool RequiresMdApproval { get; set; }

        /// <summary>
        /// The estimated total at the moment of submission. The lines
        /// remain the detail; this is what the approval decision and
        /// the threshold were based on.
        /// </summary>
        public decimal EstimatedTotal { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public Vendor? Vendor { get; set; }

        public ICollection<RequisitionLine> Lines { get; set; }
            = new List<RequisitionLine>();

        public ICollection<RequisitionHistory> History { get; set; }
            = new List<RequisitionHistory>();
    }
}
