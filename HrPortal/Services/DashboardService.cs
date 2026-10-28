using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Controllers;
using HrPortal.Security;
using HrPortal.Services.Reporting;
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
        private readonly ApproverDirectory _approvers;

        public DashboardService(
            ApplicationDbContext context,
            CurrentUserService currentUser,
            ApproverDirectory approvers)
        {
            _context = context;
            _currentUser = currentUser;
            _approvers = approvers;
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

            model.Focus = BuildFocus(model);

            model.Trends = await BuildTrendsAsync(user, employee);

            model.Upcoming = await BuildUpcomingAsync(user, employee);

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
            var weekStart = WeekStart(today);
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

                LicensesExpired = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate < today),

                LicensesExpiringSoon = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate >= today &&
                        l.ExpiryDate <= today.AddDays(
                            ExpiryWindows.LicenceDays)),

                ActivitiesThisWeek = await _context.ItActivities
                    .CountAsync(a => a.LoggedOn >= weekStart),

                ActivitiesOutstanding = await _context.ItActivities
                    .CountAsync(a => a.Status != ItActivityStatus.Resolved),

                WeeklyReportDue =
                    today.DayOfWeek >= DayOfWeek.Friday &&
                    !await _context.WeeklyReports
                        .AnyAsync(r => r.PeriodStart == weekStart),

                HasHrOfficer = await _approvers.AnybodyAsync(
                    ApprovalRole.HROfficer),

                HasMd = await _approvers.AnybodyAsync(ApprovalRole.MD),

                RecentActivity = await _context.AuditLogs
                    .Include(a => a.Employee)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(ListLength)
                    .ToListAsync()
            };
        }

        /// <summary>
        /// Collects the outstanding work the signed-in user can act on,
        /// most pressing first, so the dashboard can lead with it
        /// instead of burying it among the section tiles.
        /// </summary>
        private static List<DashboardFocusItem> BuildFocus(
            DashboardViewModel model)
        {
            var focus = new List<DashboardFocusItem>();

            if (model.Md is { AwaitingFinalApproval: > 0 } mdLeave)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-stamp",
                    Label = "Leave to sign off",
                    Figure = mdLeave.AwaitingFinalApproval.ToString("N0"),
                    Unit = mdLeave.AwaitingFinalApproval == 1
                        ? "request" : "requests",
                    Note = "Cleared HR and waiting on your decision.",
                    Controller = "LeaveApproval",
                    Action = "FinalApproval"
                });
            }

            if (model.Hr is { AwaitingReview: > 0 } hrQueue)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-inbox",
                    Label = "Leave to review",
                    Figure = hrQueue.AwaitingReview.ToString("N0"),
                    Unit = hrQueue.AwaitingReview == 1
                        ? "request" : "requests",
                    Note = "Submitted by staff and waiting on HR.",
                    Controller = "LeaveApproval",
                    Action = "Index"
                });
            }

            if (model.Mine is { OverdueTraining: > 0 } myTraining)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-graduation-cap",
                    Label = "Your training is overdue",
                    Figure = myTraining.OverdueTraining.ToString("N0"),
                    Unit = myTraining.OverdueTraining == 1
                        ? "course" : "courses",
                    Note = "Past the due date and assigned to you.",
                    Tone = "is-critical",
                    Controller = "MyTraining",
                    Action = "Index"
                });
            }

            if (model.Admin is { LicensesExpired: > 0 } expiredLicences)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-certificate",
                    Label = "Licences lapsed",
                    Figure = expiredLicences.LicensesExpired.ToString("N0"),
                    Unit = expiredLicences.LicensesExpired == 1
                        ? "licence" : "licences",
                    Note = "Still active but past the expiry date.",
                    Tone = "is-critical",
                    Controller = "License",
                    Action = "Index",
                    RouteData = new Dictionary<string, string>
                    {
                        ["show"] = "expired"
                    }
                });
            }

            if (model.Admin is { MaintenanceOverdue: > 0 } lateMaintenance)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-screwdriver-wrench",
                    Label = "Maintenance overdue",
                    Figure = lateMaintenance
                        .MaintenanceOverdue.ToString("N0"),
                    Unit = lateMaintenance.MaintenanceOverdue == 1
                        ? "job" : "jobs",
                    Note = "Scheduled for a date that has already passed.",
                    Tone = "is-critical",
                    Controller = "AssetMaintenance",
                    Action = "Index"
                });
            }

            if (model.Hr is { OverdueTrainingOrgWide: > 0 } orgTraining)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-triangle-exclamation",
                    Label = "Overdue training",
                    Figure = orgTraining
                        .OverdueTrainingOrgWide.ToString("N0"),
                    Unit = orgTraining.OverdueTrainingOrgWide == 1
                        ? "assignment" : "assignments",
                    Note = "Past the due date across the business.",
                    Tone = "is-warning",

                    Controller = "Reports",
                    Action = "Training"
                });
            }

            if (model.Hr is { ReturnedToEmployees: > 0 } returned)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-rotate-left",
                    Label = "Returned to staff",
                    Figure = returned.ReturnedToEmployees.ToString("N0"),
                    Unit = returned.ReturnedToEmployees == 1
                        ? "request" : "requests",
                    Note = "Sent back for changes and not resubmitted.",
                    Tone = "is-warning",
                    Controller = "LeaveRequest",
                    Action = "Index"
                });
            }

            if (model.Admin is { WeeklyReportDue: true })
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-file-lines",
                    Label = "Weekly IT report",
                    Note = "The report for this week is not drafted yet.",
                    Tone = "is-warning",
                    Controller = "ItActivity",
                    Action = "Index"
                });
            }

            if (model.Mine is { UnreadNotifications: > 0 } notices)
            {
                focus.Add(new DashboardFocusItem
                {
                    Icon = "fa-bell",
                    Label = "Unread notices",
                    Figure = notices.UnreadNotifications.ToString("N0"),
                    Unit = notices.UnreadNotifications == 1
                        ? "notice" : "notices",
                    Note = "Waiting for you to read.",
                    Controller = "MyNotifications",
                    Action = "Index"
                });
            }

            return focus;
        }

        /// <summary>
        /// Builds the twelve-month charts shown under the personal
        /// section. Each one is grouped by month in SQL, so a chart
        /// costs twelve rows rather than a year of records.
        /// </summary>
        private async Task<List<ReportTrend>> BuildTrendsAsync(
            ClaimsPrincipal user,
            Employee? employee)
        {
            var trends = new List<ReportTrend>();

            var start = new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1).AddMonths(-11);

            if (employee != null && !user.IsInRole(Roles.Md))
            {
                var mine = await MonthlyLeaveDaysAsync(
                    start, employee.Id);

                trends.Add(ReportMath.MonthlyTrend(
                    "My leave days", mine, "days", "0.##"));
            }

            var seesEveryone =
                user.IsInRole(Roles.HrOfficer) ||
                user.IsInRole(Roles.Md) ||
                user.IsInRole(Roles.Administrator);

            if (seesEveryone)
            {
                var everyone = await MonthlyLeaveDaysAsync(start, null);

                trends.Add(ReportMath.MonthlyTrend(
                    "Leave days approved", everyone, "days", "0.##"));
            }

            if (user.IsInRole(Roles.Administrator))
            {
                var logged = await _context.ItActivities
                    .AsNoTracking()
                    .Where(a => a.LoggedOn >= start)
                    .GroupBy(a => new
                    {
                        a.LoggedOn.Year,
                        a.LoggedOn.Month
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Value = (decimal)g.Count()
                    })
                    .ToListAsync();

                trends.Add(ReportMath.MonthlyTrend(
                    "IT activity logged",
                    logged.Select(r =>
                        (new DateTime(r.Year, r.Month, 1), r.Value)),
                    "logged"));
            }

            return trends;
        }

        /// <summary>
        /// Approved leave days per month, for one employee when an id
        /// is given or across the business when it is not.
        /// </summary>
        private async Task<List<(DateTime When, decimal Value)>>
            MonthlyLeaveDaysAsync(DateTime start, int? employeeId)
        {
            var rows = await _context.LeaveRequests
                .AsNoTracking()
                .Where(r =>
                    r.Status == LeaveRequestStatus.Approved &&
                    r.StartDate >= start &&
                    (employeeId == null || r.EmployeeId == employeeId))
                .GroupBy(r => new
                {
                    r.StartDate.Year,
                    r.StartDate.Month
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Value = g.Sum(r => r.NumberOfDays)
                })
                .ToListAsync();

            return rows
                .Select(r =>
                    (new DateTime(r.Year, r.Month, 1), r.Value))
                .ToList();
        }

        /// <summary>
        /// Dates coming up that the signed-in person can do something
        /// about: their own training first, then the renewals and
        /// bookings an administrator keeps an eye on. Anything more
        /// than three months out is left off, and the list is short
        /// because it sits on the dashboard rather than replacing the
        /// pages that hold the detail.
        /// </summary>
        private async Task<List<DashboardUpcomingItem>> BuildUpcomingAsync(
            ClaimsPrincipal user,
            Employee? employee)
        {
            var today = DateTime.Today;
            var horizon = today.AddMonths(3);

            var items = new List<DashboardUpcomingItem>();

            if (employee != null)
            {
                var training = await _context.TrainingAssignments
                    .AsNoTracking()
                    .Include(t => t.TrainingCourse)
                    .Where(t =>
                        t.EmployeeId == employee.Id &&
                        t.CompletedAt == null &&
                        t.DueDate != null &&
                        t.DueDate <= horizon)
                    .OrderBy(t => t.DueDate)
                    .Take(5)
                    .ToListAsync();

                items.AddRange(training.Select(t =>
                    new DashboardUpcomingItem
                    {
                        Icon = "fa-graduation-cap",
                        Title = t.TrainingCourse.Title,
                        Detail = "Training assigned to you",
                        Date = t.DueDate!.Value,
                        Controller = "MyTraining"
                    }));
            }

            if (user.IsInRole(Roles.Administrator))
            {
                var licences = await _context.Licenses
                    .AsNoTracking()
                    .Where(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate <= horizon)
                    .OrderBy(l => l.ExpiryDate)
                    .Take(5)
                    .ToListAsync();

                items.AddRange(licences.Select(l =>
                    new DashboardUpcomingItem
                    {
                        Icon = "fa-certificate",
                        Title = l.Name,
                        Detail = "Licence renewal",
                        Date = l.ExpiryDate,
                        Controller = "License"
                    }));

                var maintenance = await _context.AssetMaintenances
                    .AsNoTracking()
                    .Include(m => m.Asset)
                    .Where(m =>
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor <= horizon)
                    .OrderBy(m => m.ScheduledFor)
                    .Take(5)
                    .ToListAsync();

                items.AddRange(maintenance.Select(m =>
                    new DashboardUpcomingItem
                    {
                        Icon = "fa-screwdriver-wrench",
                        Title = m.Asset == null
                            ? "Maintenance"
                            : m.Asset.DeviceModel,
                        Detail = "Maintenance booked",
                        Date = m.ScheduledFor,
                        Controller = "AssetMaintenance"
                    }));
            }

            return items
                .OrderBy(i => i.Date)
                .Take(6)
                .ToList();
        }

        /// <summary>
        /// The Monday of the week a date falls in. The IT activity
        /// count and the weekly report both run to that week.
        /// </summary>
        private static DateTime WeekStart(DateTime date)
        {
            var offset = ((int)date.DayOfWeek + 6) % 7;

            return date.Date.AddDays(-offset);
        }
    }
}
