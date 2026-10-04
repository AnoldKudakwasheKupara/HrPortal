using HrPortal.Security;

namespace HrPortal.Entities
{
    public class LeaveRequest : IEmployeeOwned
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public int LeaveTypeId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal NumberOfDays { get; set; }

        public string? Reason { get; set; }

        public LeaveRequestStatus Status { get; set; }

        public DateTime SubmittedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public Employee Employee { get; set; } = null!;

        public LeaveType LeaveType { get; set; } = null!;

        public ICollection<LeaveRequestHistory> History { get; set; }
            = new List<LeaveRequestHistory>();
    }
}