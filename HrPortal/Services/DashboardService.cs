using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Controllers;
using HrPortal.Security;
using HrPortal.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HrPortal.Services
{
    public class DashboardService
    {
        private const int ListLength = 5;
        private const int StartingSoonDays = 14;

        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        public DashboardService(
            ApplicationDbContext context,
            CurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<DashboardViewModel> BuildAsync(ClaimsPrincipal user)
        {
            var employee = await _currentUser.GetEmployeeAsync();

            var model = new DashboardViewModel
            {
                GreetingName = employee?.FirstName
                    ?? await GetAccountFirstNameAsync(),

                HasEmployeeRecord = employee != null,

                ShowsOwnLeave = !user.IsInRole(Roles.Md)
            };

            if (employee != null)
                model.Mine = await BuildMineAsync(employee);

            if (user.IsInRole(Roles.HrOfficer))
                model.Hr = await BuildHrAsync();

            if (user.IsInRole(Roles.Md))
                model.Md = await BuildMdAsync();

            if (user.IsInRole(Roles.Administrator))
                model.Admin = await BuildAdminAsync();

            return model;
        }

        private async Task<string> GetAccountFirstNameAsync()
        {
            var fullName = await _currentUser.GetDisplayNameAsync();

            if (string.IsNullOrWhiteSpace(fullName))
                return "there";

            return fullName.Trim().Split(' ')[0];
        }

        private async Task<MyDashboardSection> BuildMineAsync(
            Employee employee)
        {
            var employeeId = employee.Id;
            var year = DateTime.Today.Year;
            var today = DateTime.Today;

            var weekStart = MyTimesheetController.WeekStart(today);
            var weekEnd = weekStart.AddDays(6);

            var keepsTimesheet =
                employee.IsActive &&
                employee.Department.TracksTimesheets;

            var timesheetWeek = !keepsTimesheet
                ? null
                : await _context.TimesheetEntries
                    .Where(t =>
                        t.EmployeeId == employeeId &&
                        t.WorkDate >= weekStart &&
                        t.WorkDate <= weekEnd)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Minutes = g.Sum(t => t.DurationMinutes),
                        Count = g.Count()
                    })
                    .FirstOrDefaultAsync();

            var balances = await _context.LeaveBalances
                .Include(b => b.LeaveType)
                .Where(b =>
                    b.EmployeeId == employeeId &&
                    b.Year == year)
                .ToListAsync();

            var headline = balances
                .OrderByDescending(b => b.RemainingDays)
                .FirstOrDefault();

            return new MyDashboardSection
            {
                HasBalances = balances.Count > 0,
                RemainingDays = headline?.RemainingDays ?? 0,
                HeadlineLeaveType = headline?.LeaveType.Name,

                RequestsInProgress = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.EmployeeId == employeeId &&
                        r.Status != LeaveRequestStatus.Approved &&
                        r.Status != LeaveRequestStatus.Rejected &&
                        r.Status != LeaveRequestStatus.HRRejected &&
                        r.Status != LeaveRequestStatus.Cancelled),

                OutstandingTraining = await _context.TrainingAssignments
                    .CountAsync(a =>
                        a.EmployeeId == employeeId &&
                        !a.IsCompleted),

                OverdueTraining = await _context.TrainingAssignments
                    .CountAsync(a =>
                        a.EmployeeId == employeeId &&
                        !a.IsCompleted &&
                        a.DueDate != null &&
                        a.DueDate < today),

                UnreadNotifications = await _context.Notifications
                    .CountAsync(n =>
                        n.EmployeeId == employeeId &&
                        !n.IsRead),

                MyAssets = await _context.Assets
                    .CountAsync(a =>
                        a.AssignedToEmployeeId == employeeId),

                KeepsTimesheet = keepsTimesheet,
                TimesheetMinutesThisWeek = timesheetWeek?.Minutes ?? 0,
                TimesheetEntriesThisWeek = timesheetWeek?.Count ?? 0,

                DaysTakenThisYear = balances.Sum(b => b.UsedDays),

                NextLeaveStart = await _context.LeaveRequests
                    .Where(r =>
                        r.EmployeeId == employeeId &&
                        r.Status == LeaveRequestStatus.Approved &&
                        r.StartDate >= today)
                    .OrderBy(r => r.StartDate)
                    .Select(r => (DateTime?)r.StartDate)
                    .FirstOrDefaultAsync(),

                RecentRequests = await _context.LeaveRequests
                    .Include(r => r.LeaveType)
                    .Where(r => r.EmployeeId == employeeId)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Take(ListLength)
                    .ToListAsync(),

                TrainingToDo = await _context.TrainingAssignments
                    .Include(a => a.TrainingCourse)
                    .Where(a =>
                        a.EmployeeId == employeeId &&
                        !a.IsCompleted)
                    .OrderBy(a => a.DueDate == null)
                    .ThenBy(a => a.DueDate)
                    .Take(ListLength)
                    .ToListAsync()
            };
        }

        private async Task<HrDashboardSection> BuildHrAsync()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var soon = today.AddDays(StartingSoonDays);

            return new HrDashboardSection
            {
                AwaitingReview = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Submitted ||
                        r.Status == LeaveRequestStatus.HRReview),

                WithMd = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview),

                ReturnedToEmployees = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.ReturnedToEmployee),

                ApprovedThisMonth = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.CompletedAt != null &&
                        r.CompletedAt >= monthStart),

                ActiveEmployees = await _context.Employees
                    .CountAsync(e => e.IsActive),

                EmployeesWithoutLogin = await _context.Employees
                    .CountAsync(e => e.IsActive && e.UserId == null),

                OnLeaveToday = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.StartDate <= today &&
                        r.EndDate >= today),

                OverdueTrainingOrgWide =
                    await _context.TrainingAssignments
                        .CountAsync(a =>
                            !a.IsCompleted &&
                            a.DueDate != null &&
                            a.DueDate < today),

                ReviewQueue = await _context.LeaveRequests
                    .Include(r => r.Employee)
                    .Include(r => r.LeaveType)
                    .Where(r =>
                        r.Status == LeaveRequestStatus.Submitted ||
                        r.Status == LeaveRequestStatus.HRReview)
                    .OrderBy(r => r.SubmittedAt)
                    .Take(ListLength)
                    .ToListAsync(),

                StartingSoon = await _context.LeaveRequests
                    .Include(r => r.Employee)
                    .Include(r => r.LeaveType)
                    .Where(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.EndDate >= today &&
                        r.StartDate <= soon)
                    .OrderBy(r => r.StartDate)
                    .Take(ListLength)
                    .ToListAsync()
            };
        }

        private async Task<MdDashboardSection> BuildMdAsync()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var soon = today.AddDays(StartingSoonDays);

            return new MdDashboardSection
            {
                RequisitionsAwaitingApproval = await _context.Requisitions
                    .CountAsync(r =>
                        r.Status == RequisitionStatus.ReviewApproved),

                RequisitionValueAwaiting = await _context.Requisitions
                    .Where(r => r.Status == RequisitionStatus.ReviewApproved)
                    .SumAsync(r => (decimal?)r.EstimatedTotal) ?? 0m,

                AwaitingFinalApproval = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview),

                ApprovedThisMonth = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.CompletedAt != null &&
                        r.CompletedAt >= monthStart),

                RejectedThisMonth = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Rejected &&
                        r.CompletedAt != null &&
                        r.CompletedAt >= monthStart),

                DaysApprovedThisMonth = await _context.LeaveRequests
                    .Where(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.CompletedAt != null &&
                        r.CompletedAt >= monthStart)
                    .SumAsync(r => (decimal?)r.NumberOfDays) ?? 0,

                OnLeaveToday = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.StartDate <= today &&
                        r.EndDate >= today),

                ApprovalQueue = await _context.LeaveRequests
                    .Include(r => r.Employee)
                    .Include(r => r.LeaveType)
                    .Where(r =>
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview)
                    .OrderBy(r => r.SubmittedAt)
                    .Take(ListLength)
                    .ToListAsync(),

                StartingSoon = await _context.LeaveRequests
                    .Include(r => r.Employee)
                    .Include(r => r.LeaveType)
                    .Where(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.EndDate >= today &&
                        r.StartDate <= soon)
                    .OrderBy(r => r.StartDate)
                    .Take(ListLength)
                    .ToListAsync()
            };
        }

        private async Task<AdminDashboardSection> BuildAdminAsync()
        {
            var today = DateTime.Today;
            var weekStart = MyTimesheetController.WeekStart(today);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            return new AdminDashboardSection
            {
                Employees = await _context.Employees.CountAsync(),

                ActiveEmployees = await _context.Employees
                    .CountAsync(e => e.IsActive),

                Departments = await _context.Departments
                    .CountAsync(d => d.IsActive),

                SignInAccounts = await _context.Employees
                    .CountAsync(e => e.UserId != null),

                EmployeesWithoutLogin = await _context.Employees
                    .CountAsync(e => e.IsActive && e.UserId == null),

                DocumentCategories = await _context.DocumentCategories
                    .CountAsync(c => c.IsActive),

                Assets = await _context.Assets.CountAsync(),

                AssetsNeedingAttention = await _context.Assets
                    .CountAsync(a =>
                        a.Condition == AssetCondition.Bad ||
                        a.Condition == AssetCondition.Old),

                UnassignedAssets = await _context.Assets
                    .CountAsync(a => a.AssignedToEmployeeId == null),

                MaintenanceOverdue = await _context.AssetMaintenances
                    .CountAsync(m =>
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor < today),

                MaintenanceDueSoon = await _context.AssetMaintenances
                    .CountAsync(m =>
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor >= today &&
                        m.ScheduledFor <= today.AddDays(30)),

                TimesheetMinutesThisMonth = await _context.TimesheetEntries
                    .Where(t => t.WorkDate >= monthStart && t.WorkDate <= today)
                    .SumAsync(t => (int?)t.DurationMinutes) ?? 0,

                TimesheetPeopleThisMonth = await _context.TimesheetEntries
                    .Where(t => t.WorkDate >= monthStart && t.WorkDate <= today)
                    .Select(t => t.EmployeeId)
                    .Distinct()
                    .CountAsync(),

                RequisitionsAwaitingReview = await _context.Requisitions
                    .CountAsync(r => r.Status == RequisitionStatus.Submitted),

                RequisitionValueInReview = await _context.Requisitions
                    .Where(r => r.Status == RequisitionStatus.Submitted)
                    .SumAsync(r => (decimal?)r.EstimatedTotal) ?? 0m,

                LicensesExpired = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate < today),

                LicensesExpiringSoon = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate >= today &&
                        l.ExpiryDate <= today.AddDays(
                            LicenseController.ExpiringWindowDays)),

                ActivitiesThisWeek = await _context.ItActivities
                    .CountAsync(a => a.LoggedOn >= weekStart),

                ActivitiesOutstanding = await _context.ItActivities
                    .CountAsync(a => a.Status != ItActivityStatus.Resolved),

                WeeklyReportDue =
                    today.DayOfWeek >= DayOfWeek.Friday &&
                    !await _context.WeeklyReports
                        .AnyAsync(r => r.PeriodStart == weekStart),

                HrApproverConfigured =
                    await _context.ApprovalConfigurations
                        .AnyAsync(a =>
                            a.Role == ApprovalRole.HROfficer &&
                            a.IsActive),

                MdApproverConfigured =
                    await _context.ApprovalConfigurations
                        .AnyAsync(a =>
                            a.Role == ApprovalRole.MD &&
                            a.IsActive),

                RecentActivity = await _context.AuditLogs
                    .Include(a => a.Employee)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(ListLength)
                    .ToListAsync()
            };
        }
    }
}
