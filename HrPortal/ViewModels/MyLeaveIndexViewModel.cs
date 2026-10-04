using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class MyLeaveIndexViewModel
    {
        public List<LeaveRequest> Requests { get; set; } = new();

        public List<LeaveBalance> Balances { get; set; } = new();
    }
}
