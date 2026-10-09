using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Models;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace HrPortal.Controllers
{
    public class HomeController : Controller
    {
        private const int ListLength = 5;
        private const int StartingSoonDays = 14;

        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ApplicationDbContext context,
            CurrentUserService currentUser,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _currentUser = currentUser;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var employee = await _currentUser.GetEmployeeAsync();

            var model = new DashboardViewModel
            {
                GreetingName = employee?.FirstName
                    ?? await GetAccountFirstNameAsync(),

                HasEmployeeRecord = employee != null,

                ShowsOwnLeave = !User.IsInRole(Roles.Md)
            };

            if (employee != null)
                model.Mine = await BuildMineAsync(employee.Id);

            if (User.IsInRole(Roles.HrOfficer))
                model.Hr = await BuildHrAsync();

            if (User.IsInRole(Roles.Md))
                model.Md = await BuildMdAsync();

            if (User.IsInRole(Roles.Administrator))
                model.Admin = await BuildAdminAsync();

            return View(model);
        }

        private async Task<string> GetAccountFirstNameAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            var fullName = user?.FullName;

            if (string.IsNullOrWhiteSpace(fullName))
                return "there";

            return fullName.Trim().Split(' ')[0];
        }

        private async Task<MyDashboardSection> BuildMineAsync(
            int employeeId)
        {
            var year = DateTime.Today.Year;
            var today = DateTime.Today;

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

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}
