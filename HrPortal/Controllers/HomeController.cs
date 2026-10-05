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
    {        private const int ListLength = 5;

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

                HasEmployeeRecord = employee != null
            };

            if (employee != null)
                model.Mine = await BuildMineAsync(employee.Id);

            if (User.IsInRole(Roles.HrOfficer) ||
                User.IsInRole(Roles.Administrator))
            {
                model.Hr = await BuildHrAsync();
            }

            if (User.IsInRole(Roles.Md) ||
                User.IsInRole(Roles.Administrator))
            {
                model.Md = await BuildMdAsync();
            }

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
            return new HrDashboardSection
            {
                AwaitingReview = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Submitted ||
                        r.Status == LeaveRequestStatus.HRReview),

                ActiveEmployees = await _context.Employees
                    .CountAsync(e => e.IsActive),

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
                    .ToListAsync()
            };
        }

        private async Task<MdDashboardSection> BuildMdAsync()
        {
            return new MdDashboardSection
            {
                AwaitingFinalApproval = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview),

                ApprovalQueue = await _context.LeaveRequests
                    .Include(r => r.Employee)
                    .Include(r => r.LeaveType)
                    .Where(r =>
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview)
                    .OrderBy(r => r.SubmittedAt)
                    .Take(ListLength)
                    .ToListAsync()
            };
        }

        private async Task<AdminDashboardSection> BuildAdminAsync()
        {
            return new AdminDashboardSection
            {
                Employees = await _context.Employees.CountAsync(),

                Departments = await _context.Departments
                    .CountAsync(d => d.IsActive),

                LeaveTypes = await _context.LeaveTypes
                    .CountAsync(l => l.IsActive),

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
