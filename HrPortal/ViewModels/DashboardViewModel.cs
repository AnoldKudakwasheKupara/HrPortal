using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class DashboardViewModel
    {
        public string GreetingName { get; set; } = string.Empty;

        public bool HasEmployeeRecord { get; set; }

        public MyDashboardSection? Mine { get; set; }

        public HrDashboardSection? Hr { get; set; }

        public MdDashboardSection? Md { get; set; }

        public AdminDashboardSection? Admin { get; set; }
    }

    public class MyDashboardSection
    {
        public decimal RemainingDays { get; set; }

        public string? HeadlineLeaveType { get; set; }

        public bool HasBalances { get; set; }

        public int RequestsInProgress { get; set; }

        public int OutstandingTraining { get; set; }

        public int OverdueTraining { get; set; }

        public int UnreadNotifications { get; set; }

        public List<LeaveRequest> RecentRequests { get; set; } = new();

        public List<TrainingAssignment> TrainingToDo { get; set; } = new();
    }

    public class HrDashboardSection
    {
        public int AwaitingReview { get; set; }

        public int ActiveEmployees { get; set; }

        public int OverdueTrainingOrgWide { get; set; }

        public List<LeaveRequest> ReviewQueue { get; set; } = new();
    }

    public class MdDashboardSection
    {
        public int AwaitingFinalApproval { get; set; }

        public List<LeaveRequest> ApprovalQueue { get; set; } = new();
    }

    public class AdminDashboardSection
    {
        public int Employees { get; set; }

        public int Departments { get; set; }

        public int LeaveTypes { get; set; }

        public bool HrApproverConfigured { get; set; }

        public bool MdApproverConfigured { get; set; }

        public List<AuditLog> RecentActivity { get; set; } = new();
    }
}
