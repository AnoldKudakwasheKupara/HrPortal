namespace HrPortal.Entities
{
    public class LeaveType
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal DefaultDays { get; set; }

        public bool RequiresApproval { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public ICollection<LeaveRequest> LeaveRequests { get; set; }
            = new List<LeaveRequest>();
    }
}
