namespace HrPortal.Entities
{
    public class RequisitionHistory
    {
        public int Id { get; set; }

        public int RequisitionId { get; set; }

        public int ActionedByEmployeeId { get; set; }

        public RequisitionAction Action { get; set; }

        public RequisitionStatus PreviousStatus { get; set; }

        public RequisitionStatus NewStatus { get; set; }

        public string? Comment { get; set; }

        public DateTime ActionDate { get; set; }

        public Requisition Requisition { get; set; } = null!;

        public Employee ActionedByEmployee { get; set; } = null!;
    }
}
