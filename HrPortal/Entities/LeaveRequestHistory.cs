namespace HrPortal.Entities
{
    public class LeaveRequestHistory
    {
        public int Id { get; set; }

        public int LeaveRequestId { get; set; }

        public int ActionedByEmployeeId { get; set; }

        public LeaveRequestAction Action { get; set; }

        public LeaveRequestStatus PreviousStatus { get; set; }

        public LeaveRequestStatus NewStatus { get; set; }

        public string? Comment { get; set; }

        public DateTime ActionDate { get; set; }

        public LeaveRequest LeaveRequest { get; set; } = null!;

        public Employee ActionedByEmployee { get; set; } = null!;
    }
}
