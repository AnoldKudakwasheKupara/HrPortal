namespace HrPortal.Entities
{
    public class LeaveType
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Days earned each month. Above zero, the type has a running
        /// balance per employee; at zero it has none.
        /// </summary>
        public decimal MonthlyAccrualDays { get; set; }

        public ICollection<LeaveRequest> LeaveRequests { get; set; }
            = new List<LeaveRequest>();
    }
}
