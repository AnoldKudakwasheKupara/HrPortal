using HrPortal.Entities;

namespace HrPortal.ViewModels
{
    public class DashboardViewModel
    {
        public string GreetingName { get; set; } = string.Empty;

        public bool HasEmployeeRecord { get; set; }

        public bool ShowsOwnLeave { get; set; } = true;

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

        public int MyAssets { get; set; }

        public bool KeepsTimesheet { get; set; }

        public int TimesheetMinutesThisWeek { get; set; }

        public int TimesheetEntriesThisWeek { get; set; }

        public decimal DaysTakenThisYear { get; set; }

        public List<LeaveRequest> RecentRequests { get; set; } = new();

        public List<TrainingAssignment> TrainingToDo { get; set; } = new();
    }

    public class HrDashboardSection
    {
        public int AwaitingReview { get; set; }

        public int WithMd { get; set; }

        public int ReturnedToEmployees { get; set; }

        public int ApprovedThisMonth { get; set; }

        public int ActiveEmployees { get; set; }

        public int EmployeesWithoutLogin { get; set; }

        public int OnLeaveToday { get; set; }

        public int OverdueTrainingOrgWide { get; set; }

        public List<LeaveRequest> ReviewQueue { get; set; } = new();

        public List<LeaveRequest> StartingSoon { get; set; } = new();
    }

    public class MdDashboardSection
    {
        public int AwaitingFinalApproval { get; set; }

        public int RequisitionsAwaitingApproval { get; set; }

        public decimal RequisitionValueAwaiting { get; set; }

        public int ApprovedThisMonth { get; set; }

        public int RejectedThisMonth { get; set; }

        public decimal DaysApprovedThisMonth { get; set; }

        public int OnLeaveToday { get; set; }

        public List<LeaveRequest> ApprovalQueue { get; set; } = new();

        public List<LeaveRequest> StartingSoon { get; set; } = new();
    }

    public class AdminDashboardSection
    {
        public int Employees { get; set; }

        public int ActiveEmployees { get; set; }

        public int Departments { get; set; }

        public int SignInAccounts { get; set; }

        public int EmployeesWithoutLogin { get; set; }

        public int DocumentCategories { get; set; }

        public int Assets { get; set; }

        public int AssetsNeedingAttention { get; set; }

        public int UnassignedAssets { get; set; }

        public int MaintenanceOverdue { get; set; }

        public int MaintenanceDueSoon { get; set; }

        public int LicensesExpired { get; set; }

        public int LicensesExpiringSoon { get; set; }

        public int ActivitiesThisWeek { get; set; }

        public int ActivitiesOutstanding { get; set; }

        public bool WeeklyReportDue { get; set; }

        public int RequisitionsAwaitingReview { get; set; }

        public decimal RequisitionValueInReview { get; set; }

        public int TimesheetMinutesThisMonth { get; set; }

        public int TimesheetPeopleThisMonth { get; set; }

        public bool HrApproverConfigured { get; set; }

        public bool MdApproverConfigured { get; set; }

        public List<AuditLog> RecentActivity { get; set; } = new();
    }
}
