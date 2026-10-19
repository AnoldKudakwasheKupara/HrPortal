namespace HrPortal.Entities
{
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

        public int? VendorId { get; set; }

        public bool RequiresMdApproval { get; set; }

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
