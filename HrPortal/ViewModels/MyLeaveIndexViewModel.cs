using HrPortal.Entities;
using HrPortal.Services;

namespace HrPortal.ViewModels
{
    public class MyLeaveIndexViewModel
    {
        public List<LeaveRequest> Requests { get; set; } = new();

        public List<LeaveBalance> Balances { get; set; } = new();

        public List<LeaveBalanceEntry> Statement { get; set; } = new();

        /// <summary>The group of requests being shown: all, open, approved or closed.</summary>
        public string Show { get; set; } = MyLeaveList.All;

        public PagedResult<LeaveRequest> Page { get; set; } = new();

        public int InProgress { get; set; }

        public int ApprovedThisYear { get; set; }

        public decimal DaysTakenThisYear { get; set; }

        public LeaveRequest? NextLeave { get; set; }
    }

    /// <summary>Groups and pages an employee's own leave requests.</summary>
    public static class MyLeaveList
    {
        public const string All = RequestGroups.All;
        public const string Open = RequestGroups.Open;
        public const string Approved = RequestGroups.Approved;
        public const string Closed = RequestGroups.Closed;

        public const int PageSize = 10;

        public static string Normalise(string? show) => RequestGroups.Normalise(show);

        public static bool InGroup(LeaveRequestStatus status, string show) =>
            RequestGroups.In(show, LeaveWorkflowService.IsFinal(status), status == LeaveRequestStatus.Approved);

        public static PagedResult<LeaveRequest> Page(IReadOnlyList<LeaveRequest> requests, string show, int page) =>
            PagedList.Of(requests.Where(r => InGroup(r.Status, show)).ToList(), page, PageSize);

        /// <summary>Approved days that start in the given year.</summary>
        public static decimal DaysTaken(IEnumerable<LeaveRequest> requests, int year) =>
            requests
                .Where(r => r.Status == LeaveRequestStatus.Approved && r.StartDate.Year == year)
                .Sum(r => r.NumberOfDays);

        /// <summary>The soonest approved leave that has not finished yet.</summary>
        public static LeaveRequest? Next(IEnumerable<LeaveRequest> requests, DateTime today) =>
            requests
                .Where(r => r.Status == LeaveRequestStatus.Approved && r.EndDate.Date >= today.Date)
                .OrderBy(r => r.StartDate)
                .FirstOrDefault();
    }
}
