using HrPortal.Security;

namespace HrPortal.Entities
{
    public class LeaveBalance : IEmployeeOwned
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public int LeaveTypeId { get; set; }

        public int Year { get; set; }

        public decimal AllocatedDays { get; set; }

        public decimal UsedDays { get; set; }

        public decimal PendingDays { get; set; }

        public decimal RemainingDays { get; set; }


        public Employee Employee { get; set; } = null!;

        public LeaveType LeaveType { get; set; } = null!;
    }
}