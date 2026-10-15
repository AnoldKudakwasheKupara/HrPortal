using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Reporting across leave, training, people, documents, assets and
    /// the audit trail.
    ///
    /// Every report is restricted to the roles that may already see the
    /// records behind it, so reporting cannot be used as a way around
    /// the boundaries the rest of the portal enforces. The administrator
    /// therefore gets the system reports, not the HR ones.
    /// </summary>
    [Authorize(Roles = Roles.AnyManager)]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var isAdmin = User.IsInRole(Roles.Administrator);
            var isHr = User.IsInRole(Roles.HrOfficer);
            var isMd = User.IsInRole(Roles.Md);

            var today = DateTime.Today;
            var yearStart = new DateTime(today.Year, 1, 1);

            var model = new ReportsIndexViewModel
            {
                ShowLeave = isHr || isMd,
                ShowBalances = isHr,
                ShowTraining = isHr,
                ShowPeople = isHr || isAdmin,
                ShowDocuments = isHr,
                ShowSystem = isAdmin,
                ShowProcurement = isAdmin || isMd
            };

            // Each figure is only queried for the roles that may see
            // the report it belongs to.
            if (model.ShowLeave)
            {
                model.LeaveRequests = await _context.LeaveRequests
                    .CountAsync();

                model.LeaveAwaitingDecision = await _context.LeaveRequests
                    .CountAsync(r =>
                        r.Status == LeaveRequestStatus.Submitted ||
                        r.Status == LeaveRequestStatus.HRReview ||
                        r.Status == LeaveRequestStatus.HRApproved ||
                        r.Status == LeaveRequestStatus.MDReview);

                var approvedThisYear = await _context.LeaveRequests
                    .Where(r =>
                        r.Status == LeaveRequestStatus.Approved &&
                        r.StartDate >= yearStart)
                    .ToListAsync();

                model.ApprovedDaysThisYear =
                    approvedThisYear.Sum(r => r.NumberOfDays);

                model.EmployeesOnLeaveThisYear = approvedThisYear
                    .Select(r => r.EmployeeId)
                    .Distinct()
                    .Count();
            }

            if (model.ShowBalances)
            {
                var balances = await _context.LeaveBalances
                    .Where(b => b.Year == today.Year)
                    .ToListAsync();

                model.RemainingDays = balances.Sum(b => b.RemainingDays);
                model.BalancesFullyUsed =
                    balances.Count(b => b.RemainingDays <= 0);
            }

            if (model.ShowTraining)
            {
                model.TrainingAssignments = await _context.TrainingAssignments
                    .CountAsync();

                model.TrainingOverdue = await _context.TrainingAssignments
                    .CountAsync(a =>
                        !a.IsCompleted &&
                        a.DueDate != null &&
                        a.DueDate < today);

                model.Courses = await _context.TrainingCourses
                    .CountAsync(c => c.IsActive);

                var ratings = await _context.TrainingFeedbacks
                    .Select(f => f.Rating)
                    .ToListAsync();

                model.AverageCourseRating = ratings.Count == 0
                    ? null
                    : Math.Round(ratings.Average(), 2);
            }

            if (model.ShowPeople)
            {
                model.ActiveEmployees = await _context.Employees
                    .CountAsync(e => e.IsActive);

                model.EmployeesWithoutLogin = await _context.Employees
                    .CountAsync(e => e.IsActive && e.UserId == null);
            }

            if (model.ShowDocuments)
            {
                model.Documents = await _context.EmployeeDocuments
                    .CountAsync();

                var covered = await _context.EmployeeDocuments
                    .Select(d => d.EmployeeId)
                    .Distinct()
                    .CountAsync();

                var active = await _context.Employees
                    .CountAsync(e => e.IsActive);

                model.EmployeesWithoutDocuments =
                    Math.Max(0, active - covered);
            }

            if (model.ShowProcurement)
            {
                model.RequisitionsAwaiting = await _context.Requisitions
                    .CountAsync(r =>
                        r.Status == RequisitionStatus.Submitted ||
                        r.Status == RequisitionStatus.ReviewApproved);

                model.RequisitionsApprovedValue = await _context.Requisitions
                    .Where(r =>
                        r.Status == RequisitionStatus.Approved &&
                        r.CompletedAt != null &&
                        r.CompletedAt >= yearStart)
                    .SumAsync(r => (decimal?)r.EstimatedTotal) ?? 0m;
            }

            if (model.ShowSystem)
            {
                model.Vendors = await _context.Vendors
                    .CountAsync(v => v.IsActive);

                model.VendorsWithoutContact = await _context.Vendors
                    .CountAsync(v =>
                        v.IsActive &&
                        (v.Email == null || v.Email == "") &&
                        (v.PhoneNumber == null || v.PhoneNumber == ""));

                model.Assets = await _context.Assets.CountAsync();

                model.AssetsNeedingAttention = await _context.Assets
                    .CountAsync(a =>
                        a.Condition == AssetCondition.Old ||
                        a.Condition == AssetCondition.Bad);

                model.ItActivity30Days = await _context.ItActivities
                    .CountAsync(a => a.LoggedOn >= today.AddDays(-30));

                model.ItActivityOutstanding = await _context.ItActivities
                    .CountAsync(a => a.Status != ItActivityStatus.Resolved);

                model.MaintenanceOpen = await _context.AssetMaintenances
                    .CountAsync(m =>
                        m.Status == MaintenanceStatus.Scheduled);

                model.MaintenanceOverdue = await _context.AssetMaintenances
                    .CountAsync(m =>
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor < today);

                model.Licences = await _context.Licenses
                    .CountAsync(l => l.Status == LicenseStatus.Active);

                var licenceCutoff = today.AddDays(
                    LicenseController.ExpiringWindowDays);

                model.LicencesDueSoon = await _context.Licenses
                    .CountAsync(l =>
                        l.Status == LicenseStatus.Active &&
                        l.ExpiryDate <= licenceCutoff);

                var since = today.AddDays(-30);

                model.AuditEntries30Days = await _context.AuditLogs
                    .CountAsync(a => a.Timestamp >= since);
            }

            return View(model);
        }


        // ============================================================
        // LEAVE
        // ============================================================

        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> LeaveRequests(
            ReportFilter filter,
            string? export)
        {
            var query = _context.LeaveRequests
                .Include(r => r.Employee).ThenInclude(e => e.Department)
                .Include(r => r.LeaveType)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(r => r.EndDate >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(r => r.StartDate <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(r =>
                    r.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(r => r.EmployeeId == filter.EmployeeId.Value);

            if (filter.LeaveTypeId.HasValue)
                query = query.Where(r => r.LeaveTypeId == filter.LeaveTypeId.Value);

            if (filter.Status.HasValue)
                query = query.Where(r => r.Status == filter.Status.Value);

            var requests = await query
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            var rows = requests.Select(r => new LeaveRequestReportRow
            {
                RequestNumber = r.RequestNumber,
                Employee = r.Employee.FirstName + " " + r.Employee.LastName,
                Department = r.Employee.Department?.Name ?? "--",
                LeaveType = r.LeaveType.Name,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                Days = r.NumberOfDays,
                Status = r.Status,
                SubmittedAt = r.SubmittedAt,
                CompletedAt = r.CompletedAt,
                TurnaroundDays = r.CompletedAt.HasValue
                    ? Math.Round(
                        (r.CompletedAt.Value - r.SubmittedAt).TotalDays, 1)
                    : null
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "leave-requests",
                    new[]
                    {
                        "Request", "Employee", "Department", "Leave type",
                        "Start", "End", "Days", "Status", "Submitted",
                        "Completed", "Turnaround (days)"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.RequestNumber, r.Employee, r.Department,
                        r.LeaveType, D(r.StartDate), D(r.EndDate),
                        N(r.Days), r.Status.ToString(), D(r.SubmittedAt),
                        D(r.CompletedAt), r.TurnaroundDays?.ToString("0.#")
                    }));
            }

            var approved = rows
                .Where(r => r.Status == LeaveRequestStatus.Approved)
                .ToList();

            var turnarounds = rows
                .Where(r => r.TurnaroundDays.HasValue)
                .Select(r => r.TurnaroundDays!.Value)
                .ToList();

            var model = new ReportViewModel<LeaveRequestReportRow>
            {
                Title = "Leave requests",
                Description =
                    "Every leave request, with how long each took to " +
                    "reach a decision.",
                Action = nameof(LeaveRequests),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Requests", rows.Count.ToString()),
                    new ReportStat(
                        "Approved",
                        approved.Count.ToString(),
                        N(approved.Sum(r => r.Days)) + " days"),
                    new ReportStat(
                        "In progress",
                        rows.Count(r =>
                            !LeaveWorkflowService.IsFinal(r.Status))
                            .ToString()),
                    new ReportStat(
                        "Rejected",
                        rows.Count(r =>
                            r.Status == LeaveRequestStatus.Rejected ||
                            r.Status == LeaveRequestStatus.HRRejected)
                            .ToString()),
                    new ReportStat(
                        "Average turnaround",
                        turnarounds.Count == 0
                            ? "--"
                            : turnarounds.Average().ToString("0.#") + " d",
                        "Submission to decision")
                },
                Coverage = Period(filter)
            };

            var byStatus = new ReportBreakdown("By status", "requests");

            foreach (var g in rows
                .GroupBy(r => r.Status)
                .OrderByDescending(g => g.Count()))
            {
                byStatus.Add(
                    g.Key.ToString(),
                    g.Count(),
                    g.Key == LeaveRequestStatus.Approved ? "good"
                        : g.Key == LeaveRequestStatus.Rejected ||
                          g.Key == LeaveRequestStatus.HRRejected ? "critical"
                        : null);
            }

            var byDepartment = new ReportBreakdown("Days by department", "days");

            foreach (var g in rows
                .Where(r => r.Status == LeaveRequestStatus.Approved)
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Sum(r => r.Days)))
            {
                byDepartment.Add(g.Key, g.Sum(r => r.Days));
            }

            var byType = new ReportBreakdown("Days by leave type", "days");

            foreach (var g in rows
                .Where(r => r.Status == LeaveRequestStatus.Approved)
                .GroupBy(r => r.LeaveType)
                .OrderByDescending(g => g.Sum(r => r.Days)))
            {
                byType.Add(g.Key, g.Sum(r => r.Days));
            }

            model.Breakdowns.AddRange(
                new[] { byStatus, byDepartment, byType }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> LeaveBalances(
            ReportFilter filter,
            string? export)
        {
            filter.Year ??= DateTime.Today.Year;

            var query = _context.LeaveBalances
                .Include(b => b.Employee).ThenInclude(e => e.Department)
                .Include(b => b.LeaveType)
                .AsNoTracking()
                .Where(b => b.Year == filter.Year.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(b =>
                    b.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(b => b.EmployeeId == filter.EmployeeId.Value);

            if (filter.LeaveTypeId.HasValue)
                query = query.Where(b => b.LeaveTypeId == filter.LeaveTypeId.Value);

            var rows = await query
                .OrderBy(b => b.Employee.FirstName)
                .ThenBy(b => b.LeaveType.Name)
                .Select(b => new LeaveBalanceReportRow
                {
                    Employee = b.Employee.FirstName + " " + b.Employee.LastName,
                    Department = b.Employee.Department!.Name,
                    LeaveType = b.LeaveType.Name,
                    Year = b.Year,
                    Allocated = b.AllocatedDays,
                    Used = b.UsedDays,
                    Pending = b.PendingDays,
                    Remaining = b.RemainingDays
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "leave-balances",
                    new[]
                    {
                        "Employee", "Department", "Leave type", "Year",
                        "Allocated", "Used", "Pending", "Remaining"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, r.LeaveType,
                        r.Year.ToString(), N(r.Allocated), N(r.Used),
                        N(r.Pending), N(r.Remaining)
                    }));
            }

            var model = new ReportViewModel<LeaveBalanceReportRow>
            {
                Title = "Leave balances",
                Description =
                    "Entitlement, what has been taken and what is left.",
                Action = nameof(LeaveBalances),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Balances", rows.Count.ToString()),
                    new ReportStat(
                        "Allocated", N(rows.Sum(r => r.Allocated)), "days"),
                    new ReportStat(
                        "Used", N(rows.Sum(r => r.Used)), "days"),
                    new ReportStat(
                        "Pending", N(rows.Sum(r => r.Pending)), "days"),
                    new ReportStat(
                        "Remaining", N(rows.Sum(r => r.Remaining)), "days"),
                    new ReportStat(
                        "Fully used",
                        rows.Count(r => r.Remaining <= 0).ToString(),
                        "nothing left")
                },
                Coverage = $"Balance year {filter.Year}"
            };

            var remainingByDept =
                new ReportBreakdown("Remaining by department", "days");

            foreach (var g in rows
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Sum(r => r.Remaining)))
            {
                remainingByDept.Add(g.Key, g.Sum(r => r.Remaining));
            }

            var usageByType =
                new ReportBreakdown("Days used by leave type", "days");

            foreach (var g in rows
                .GroupBy(r => r.LeaveType)
                .OrderByDescending(g => g.Sum(r => r.Used)))
            {
                usageByType.Add(g.Key, g.Sum(r => r.Used));
            }

            model.Breakdowns.AddRange(
                new[] { remainingByDept, usageByType }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> Absence(
            ReportFilter filter,
            string? export)
        {
            filter.From ??= new DateTime(DateTime.Today.Year, 1, 1);
            filter.To ??= new DateTime(DateTime.Today.Year, 12, 31);

            var query = _context.LeaveRequests
                .Include(r => r.Employee).ThenInclude(e => e.Department)
                .AsNoTracking()
                .Where(r =>
                    r.EndDate >= filter.From.Value &&
                    r.StartDate <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(r =>
                    r.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            var requests = await query.ToListAsync();

            var rows = requests
                .GroupBy(r => new
                {
                    r.EmployeeId,
                    Name = r.Employee.FirstName + " " + r.Employee.LastName,
                    Department = r.Employee.Department != null
                        ? r.Employee.Department.Name
                        : "--"
                })
                .Select(g => new AbsenceReportRow
                {
                    Employee = g.Key.Name,
                    Department = g.Key.Department,
                    ApprovedDays = g
                        .Where(r => r.Status == LeaveRequestStatus.Approved)
                        .Sum(r => r.NumberOfDays),
                    ApprovedRequests = g
                        .Count(r => r.Status == LeaveRequestStatus.Approved),
                    PendingDays = g
                        .Where(r => !LeaveWorkflowService.IsFinal(r.Status))
                        .Sum(r => r.NumberOfDays),
                    RejectedRequests = g.Count(r =>
                        r.Status == LeaveRequestStatus.Rejected ||
                        r.Status == LeaveRequestStatus.HRRejected),
                    LastLeaveEnd = g
                        .Where(r => r.Status == LeaveRequestStatus.Approved)
                        .Select(r => (DateTime?)r.EndDate)
                        .DefaultIfEmpty(null)
                        .Max()
                })
                .OrderByDescending(r => r.ApprovedDays)
                .ThenBy(r => r.Employee)
                .ToList();

            if (export == "csv")
            {
                return Csv(
                    "absence-summary",
                    new[]
                    {
                        "Employee", "Department", "Approved days",
                        "Approved requests", "Days in progress",
                        "Rejected requests", "Last day of leave"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, N(r.ApprovedDays),
                        r.ApprovedRequests.ToString(), N(r.PendingDays),
                        r.RejectedRequests.ToString(), D(r.LastLeaveEnd)
                    }));
            }

            var model = new ReportViewModel<AbsenceReportRow>
            {
                Title = "Absence summary",
                Description =
                    "Days taken per person over the period, highest first.",
                Action = nameof(Absence),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Employees", rows.Count.ToString(),
                        "with leave in the period"),
                    new ReportStat(
                        "Approved days", N(rows.Sum(r => r.ApprovedDays))),
                    new ReportStat(
                        "Average per person",
                        rows.Count == 0
                            ? "--"
                            : N(rows.Sum(r => r.ApprovedDays) / rows.Count)),
                    new ReportStat(
                        "Days in progress", N(rows.Sum(r => r.PendingDays)))
                },
                Coverage = Period(filter)
            };

            var topTakers = new ReportBreakdown("Most days taken", "days");

            foreach (var r in rows.Take(8).Where(r => r.ApprovedDays > 0))
                topTakers.Add(r.Employee, r.ApprovedDays);

            var deptDays = new ReportBreakdown("Days by department", "days");

            foreach (var g in rows
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Sum(r => r.ApprovedDays)))
            {
                deptDays.Add(g.Key, g.Sum(r => r.ApprovedDays));
            }

            model.Breakdowns.AddRange(
                new[] { topTakers, deptDays }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // TRAINING
        // ============================================================

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Training(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var query = _context.TrainingAssignments
                .Include(a => a.Employee).ThenInclude(e => e.Department)
                .Include(a => a.TrainingCourse)
                .Include(a => a.Feedback)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(a => a.AssignedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(a => a.AssignedAt <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(a =>
                    a.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(a => a.EmployeeId == filter.EmployeeId.Value);

            if (filter.TrainingCourseId.HasValue)
            {
                query = query.Where(a =>
                    a.TrainingCourseId == filter.TrainingCourseId.Value);
            }

            var assignments = await query
                .OrderBy(a => a.IsCompleted)
                .ThenBy(a => a.DueDate)
                .ToListAsync();

            var rows = assignments.Select(a => new TrainingReportRow
            {
                Course = a.TrainingCourse.Title,
                Employee = a.Employee.FirstName + " " + a.Employee.LastName,
                Department = a.Employee.Department?.Name ?? "--",
                AssignedAt = a.AssignedAt,
                DueDate = a.DueDate,
                IsCompleted = a.IsCompleted,
                CompletedAt = a.CompletedAt,
                IsOverdue = !a.IsCompleted &&
                            a.DueDate != null &&
                            a.DueDate < today,
                Rating = a.Feedback?.Rating
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "training-assignments",
                    new[]
                    {
                        "Course", "Employee", "Department", "Assigned",
                        "Due", "Completed", "Completed on", "Overdue",
                        "Rating"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Course, r.Employee, r.Department,
                        D(r.AssignedAt), D(r.DueDate),
                        r.IsCompleted ? "Yes" : "No", D(r.CompletedAt),
                        r.IsOverdue ? "Yes" : "No", r.Rating?.ToString()
                    }));
            }

            var completed = rows.Count(r => r.IsCompleted);

            var model = new ReportViewModel<TrainingReportRow>
            {
                Title = "Training assignments",
                Description =
                    "Who has been assigned what, and whether it is done.",
                Action = nameof(Training),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Assignments", rows.Count.ToString()),
                    new ReportStat("Completed", completed.ToString()),
                    new ReportStat(
                        "Completion rate",
                        rows.Count == 0
                            ? "--"
                            : (completed * 100m / rows.Count)
                                .ToString("0.#") + "%"),
                    new ReportStat(
                        "Overdue",
                        rows.Count(r => r.IsOverdue).ToString(),
                        "past the due date")
                },
                Coverage = Period(filter)
            };

            var progress = new ReportBreakdown("Progress", "assignments")
                .Add("Completed", rows.Count(r => r.IsCompleted), "good")
                .Add("Outstanding",
                    rows.Count(r => !r.IsCompleted && !r.IsOverdue))
                .Add("Overdue", rows.Count(r => r.IsOverdue), "critical");

            var byCourse =
                new ReportBreakdown("Assignments by course", "assignments");

            foreach (var g in rows
                .GroupBy(r => r.Course)
                .OrderByDescending(g => g.Count()))
            {
                byCourse.Add(g.Key, g.Count());
            }

            var outstandingByDept =
                new ReportBreakdown("Outstanding by department", "assignments");

            foreach (var g in rows
                .Where(r => !r.IsCompleted)
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Count()))
            {
                outstandingByDept.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { progress, byCourse, outstandingByDept }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> TrainingCourses(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var courses = await _context.TrainingCourses
                .Include(c => c.Assignments).ThenInclude(a => a.Feedback)
                .AsNoTracking()
                .Where(c => filter.IncludeInactive || c.IsActive)
                .OrderBy(c => c.Title)
                .ToListAsync();

            var rows = courses.Select(c =>
            {
                var ratings = c.Assignments
                    .Where(a => a.Feedback != null)
                    .Select(a => a.Feedback!.Rating)
                    .ToList();

                var assigned = c.Assignments.Count;
                var done = c.Assignments.Count(a => a.IsCompleted);

                return new TrainingCourseReportRow
                {
                    Course = c.Title,
                    Trainer = c.Trainer,
                    TrainingDate = c.TrainingDate,
                    Assigned = assigned,
                    Completed = done,
                    Overdue = c.Assignments.Count(a =>
                        !a.IsCompleted &&
                        a.DueDate != null &&
                        a.DueDate < today),
                    CompletionRate = assigned == 0
                        ? 0
                        : Math.Round(done * 100m / assigned, 1),
                    AverageRating = ratings.Count == 0
                        ? null
                        : Math.Round(ratings.Average(), 2),
                    Responses = ratings.Count
                };
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "training-courses",
                    new[]
                    {
                        "Course", "Trainer", "Training date", "Assigned",
                        "Completed", "Overdue", "Completion rate %",
                        "Average rating", "Responses"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Course, r.Trainer, D(r.TrainingDate),
                        r.Assigned.ToString(), r.Completed.ToString(),
                        r.Overdue.ToString(), N(r.CompletionRate),
                        r.AverageRating?.ToString("0.00"),
                        r.Responses.ToString()
                    }));
            }

            var allRatings = rows
                .Where(r => r.AverageRating.HasValue)
                .ToList();

            var model = new ReportViewModel<TrainingCourseReportRow>
            {
                Title = "Course effectiveness",
                Description =
                    "Take-up and feedback for each course.",
                Action = nameof(TrainingCourses),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Courses", rows.Count.ToString()),
                    new ReportStat(
                        "Assignments", rows.Sum(r => r.Assigned).ToString()),
                    new ReportStat(
                        "Overall completion",
                        rows.Sum(r => r.Assigned) == 0
                            ? "--"
                            : (rows.Sum(r => r.Completed) * 100m /
                               rows.Sum(r => r.Assigned)).ToString("0.#") + "%"),
                    new ReportStat(
                        "Average rating",
                        allRatings.Count == 0
                            ? "--"
                            : allRatings
                                .Average(r => r.AverageRating!.Value)
                                .ToString("0.00"),
                        "out of 5")
                },
                Coverage = filter.IncludeInactive
                    ? "All courses"
                    : "Active courses"
            };

            var completion =
                new ReportBreakdown("Completion rate by course", "%");

            foreach (var r in rows
                .Where(r => r.Assigned > 0)
                .OrderByDescending(r => r.CompletionRate))
            {
                completion.Add(
                    r.Course,
                    r.CompletionRate,
                    r.CompletionRate >= 80 ? "good"
                        : r.CompletionRate < 40 ? "critical"
                        : "warning");
            }

            var ratingsByCourse =
                new ReportBreakdown("Average rating", "out of 5");

            foreach (var r in rows
                .Where(r => r.AverageRating.HasValue)
                .OrderByDescending(r => r.AverageRating))
            {
                ratingsByCourse.Add(
                    r.Course,
                    (decimal)r.AverageRating!.Value,
                    r.AverageRating >= 4 ? "good"
                        : r.AverageRating < 3 ? "critical"
                        : null);
            }

            model.Breakdowns.AddRange(
                new[] { completion, ratingsByCourse }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // PEOPLE
        // ============================================================

        [Authorize(Roles = Roles.AdminOrHr)]
        public async Task<IActionResult> Employees(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var query = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.User)
                .AsNoTracking()
                .AsQueryable();

            if (!filter.IncludeInactive)
                query = query.Where(e => e.IsActive);

            if (filter.DepartmentId.HasValue)
                query = query.Where(e => e.DepartmentId == filter.DepartmentId.Value);

            if (filter.From.HasValue)
                query = query.Where(e => e.DateJoined >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(e => e.DateJoined <= filter.To.Value);

            var employees = await query
                .OrderBy(e => e.Department!.Name)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            var rows = new List<EmployeeReportRow>();

            foreach (var e in employees)
            {
                var roles = e.User != null
                    ? await _userManager.GetRolesAsync(e.User)
                    : new List<string>();

                rows.Add(new EmployeeReportRow
                {
                    EmployeeNumber = e.EmployeeNumber,
                    Name = e.FirstName + " " + e.LastName,
                    JobTitle = e.JobTitle,
                    EmploymentType = e.EmploymentType,
                    ContractEndDate = e.ContractEndDate,
                    EndDate = e.EndDate,
                    Department = e.Department?.Name ?? "--",
                    Email = e.Email,
                    DateJoined = e.DateJoined,
                    YearsOfService =
                        (int)((today - e.DateJoined).TotalDays / 365.25),
                    IsActive = e.IsActive,
                    HasLogin = e.UserId != null,
                    Roles = string.Join(", ", roles.OrderBy(r => r))
                });
            }

            if (export == "csv")
            {
                return Csv(
                    "employees",
                    new[]
                    {
                        "Employee number", "Name", "Job title", "Department",
                        "Email", "Employment type", "Contract ends", "Left on",
                        "Date joined", "Years of service", "Active",
                        "Has login", "Roles"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.EmployeeNumber, r.Name, r.JobTitle, r.Department,
                        r.Email, r.EmploymentType.ToString(), D(r.ContractEndDate),
                        D(r.EndDate),
                        D(r.DateJoined), r.YearsOfService.ToString(),
                        r.IsActive ? "Yes" : "No",
                        r.HasLogin ? "Yes" : "No", r.Roles
                    }));
            }

            var model = new ReportViewModel<EmployeeReportRow>
            {
                Title = "Employees",
                Description =
                    "Headcount with length of service and account status.",
                Action = nameof(Employees),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Employees", rows.Count.ToString()),
                    new ReportStat(
                        "Active", rows.Count(r => r.IsActive).ToString()),
                    new ReportStat(
                        "Departments",
                        rows.Select(r => r.Department).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "Without a login",
                        rows.Count(r => !r.HasLogin).ToString()),
                    new ReportStat(
                        "Average service",
                        rows.Count == 0
                            ? "--"
                            : rows.Average(r => r.YearsOfService)
                                .ToString("0.#") + " yrs")
                },
                Coverage = filter.IncludeInactive
                    ? "Active and inactive employees"
                    : "Active employees"
            };

            var headcount =
                new ReportBreakdown("Headcount by department", "people");

            foreach (var g in rows
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Count()))
            {
                headcount.Add(g.Key, g.Count());
            }

            var service = new ReportBreakdown("Length of service", "people")
                .Add("Under 1 year",
                    rows.Count(r => r.YearsOfService < 1))
                .Add("1 to 3 years",
                    rows.Count(r => r.YearsOfService is >= 1 and < 3))
                .Add("3 to 5 years",
                    rows.Count(r => r.YearsOfService is >= 3 and < 5))
                .Add("5 years or more",
                    rows.Count(r => r.YearsOfService >= 5));

            var byRole = new ReportBreakdown("By role", "people");

            foreach (var g in rows
                .SelectMany(r => string.IsNullOrWhiteSpace(r.Roles)
                    ? new[] { "No role" }
                    : r.Roles.Split(", "))
                .GroupBy(role => role)
                .OrderByDescending(g => g.Count()))
            {
                byRole.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { headcount, service, byRole }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Documents(
            ReportFilter filter,
            string? export)
        {
            var query = _context.EmployeeDocuments
                .Include(d => d.Employee).ThenInclude(e => e.Department)
                .Include(d => d.DocumentCategory)
                .Include(d => d.UploadedByEmployee)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(d => d.UploadedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(d => d.UploadedAt <= filter.To.Value);

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(d =>
                    d.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(d => d.EmployeeId == filter.EmployeeId.Value);

            if (filter.DocumentCategoryId.HasValue)
            {
                query = query.Where(d =>
                    d.DocumentCategoryId == filter.DocumentCategoryId.Value);
            }

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var rows = documents.Select(d => new DocumentReportRow
            {
                Employee = d.Employee.FirstName + " " + d.Employee.LastName,
                Department = d.Employee.Department?.Name ?? "--",
                Category = d.DocumentCategory.Name,
                FileName = d.OriginalFileName,
                UploadedAt = d.UploadedAt,
                ExpiryDate = d.ExpiryDate,
                UploadedBy = !string.IsNullOrWhiteSpace(d.UploadedByName)
                    ? d.UploadedByName!
                    : d.UploadedByEmployee != null
                        ? d.UploadedByEmployee.FirstName + " " +
                          d.UploadedByEmployee.LastName
                        : "Not recorded"
            }).ToList();

            if (export == "csv")
            {
                return Csv(
                    "employee-documents",
                    new[]
                    {
                        "Employee", "Department", "Category", "File",
                        "Expires", "Uploaded", "Uploaded by"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, r.Category, r.FileName,
                        D(r.ExpiryDate), D(r.UploadedAt), r.UploadedBy
                    }));
            }

            var withDocs = rows.Select(r => r.Employee).Distinct().Count();

            var activeEmployees = await _context.Employees
                .CountAsync(e => e.IsActive);

            var model = new ReportViewModel<DocumentReportRow>
            {
                Title = "Employee documents",
                Description =
                    "What is on file, for whom, and who filed it.",
                Action = nameof(Documents),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Documents", rows.Count.ToString()),
                    new ReportStat(
                        "Employees covered", withDocs.ToString(),
                        $"of {activeEmployees} active"),
                    new ReportStat(
                        "Categories used",
                        rows.Select(r => r.Category).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "Nothing on file",
                        Math.Max(0, activeEmployees - withDocs).ToString(),
                        "active employees")
                },
                Coverage = Period(filter)
            };

            var byCategory = new ReportBreakdown("By category", "documents");

            foreach (var g in rows
                .GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count()))
            {
                byCategory.Add(g.Key, g.Count());
            }

            var today = DateTime.Today;

            var expiring = new ReportBreakdown("Expiry", "documents")
                .Add(
                    "Expired",
                    rows.Count(r =>
                        r.ExpiryDate != null &&
                        r.ExpiryDate.Value.Date < today),
                    "critical")
                .Add(
                    "Within 30 days",
                    rows.Count(r =>
                        r.ExpiryDate != null &&
                        r.ExpiryDate.Value.Date >= today &&
                        r.ExpiryDate.Value.Date <= today.AddDays(30)),
                    "warning")
                .Add(
                    "Later",
                    rows.Count(r =>
                        r.ExpiryDate != null &&
                        r.ExpiryDate.Value.Date > today.AddDays(30)),
                    "good")
                .Add(
                    "No expiry recorded",
                    rows.Count(r => r.ExpiryDate == null));


            var byDept = new ReportBreakdown("By department", "documents");

            foreach (var g in rows
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Count()))
            {
                byDept.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byCategory, byDept, expiring }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // SYSTEM
        // ============================================================

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Assets(
            ReportFilter filter,
            string? export)
        {
            var query = _context.Assets
                .Include(a => a.AssignedToEmployee)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(a => a.Company == filter.Company);

            if (filter.Condition.HasValue)
                query = query.Where(a => a.Condition == filter.Condition.Value);

            if (filter.EmployeeId.HasValue)
            {
                query = query.Where(a =>
                    a.AssignedToEmployeeId == filter.EmployeeId.Value);
            }

            var rows = await query
                .OrderBy(a => a.Company)
                .ThenBy(a => a.DeviceModel)
                .Select(a => new AssetReportRow
                {
                    Company = a.Company ?? "--",
                    DeviceModel = a.DeviceModel,
                    Processor = a.Processor,
                    SerialNumber = a.SerialNumber,
                    ProcuredOn = a.ProcuredOn,
                    Condition = a.Condition,
                    IsInUse = a.IsInUse,
                    AssignedTo = a.AssignedToEmployee != null
                        ? a.AssignedToEmployee.FirstName + " " +
                          a.AssignedToEmployee.LastName
                        : "Unassigned",
                    Notes = a.Notes
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "assets",
                    new[]
                    {
                        "Company", "Device / model", "Processor",
                        "Serial", "Procured", "Condition", "In use",
                        "Assigned to", "Notes"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Company, r.DeviceModel, r.Processor,
                        r.SerialNumber, D(r.ProcuredOn),
                        r.Condition.ToString(), r.IsInUse ? "Yes" : "No",
                        r.AssignedTo, r.Notes
                    }));
            }

            var model = new ReportViewModel<AssetReportRow>
            {
                Title = "Asset register",
                Description =
                    "Machines on the register, their condition and holder.",
                Action = nameof(Assets),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Assets", rows.Count.ToString()),
                    new ReportStat(
                        "Good",
                        rows.Count(r => r.Condition == AssetCondition.Good)
                            .ToString()),
                    new ReportStat(
                        "Needing attention",
                        rows.Count(r =>
                            r.Condition == AssetCondition.Old ||
                            r.Condition == AssetCondition.Bad).ToString(),
                        "old or bad"),
                    new ReportStat(
                        "Unassigned",
                        rows.Count(r => r.AssignedTo == "Unassigned")
                            .ToString()),
                    new ReportStat(
                        "Not in use",
                        rows.Count(r => !r.IsInUse).ToString())
                },
                Coverage = string.IsNullOrWhiteSpace(filter.Company)
                    ? "All companies"
                    : filter.Company
            };

            var byCondition = new ReportBreakdown("By condition", "machines");

            foreach (var c in Enum.GetValues<AssetCondition>())
            {
                var count = rows.Count(r => r.Condition == c);

                if (count > 0)
                {
                    byCondition.Add(
                        c.ToString(),
                        count,
                        c == AssetCondition.Good ? "good"
                            : c == AssetCondition.Bad ? "critical"
                            : "warning");
                }
            }

            var byCompany = new ReportBreakdown("By company", "machines");

            foreach (var g in rows
                .GroupBy(r => r.Company)
                .OrderByDescending(g => g.Count()))
            {
                byCompany.Add(g.Key, g.Count());
            }

            var byHolder = new ReportBreakdown("Most machines held", "machines");

            foreach (var g in rows
                .Where(r => r.AssignedTo != "Unassigned")
                .GroupBy(r => r.AssignedTo)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byHolder.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byCondition, byCompany, byHolder }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Vendors(
            ReportFilter filter,
            string? export)
        {
            var query = _context.Vendors.AsNoTracking().AsQueryable();

            if (!filter.IncludeInactive)
                query = query.Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(v => v.Category == filter.Company);

            var rows = await query
                .OrderBy(v => v.Category)
                .ThenBy(v => v.Name)
                .Select(v => new VendorReportRow
                {
                    Name = v.Name,
                    ProductOrService = v.ProductOrService,
                    Category = v.Category,
                    ContactPerson = v.ContactPerson,
                    Email = v.Email,
                    PhoneNumber = v.PhoneNumber,
                    IsActive = v.IsActive
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "vendors",
                    new[]
                    {
                        "Vendor", "Product or service", "Category",
                        "Contact person", "Email", "Phone", "In use"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Name, r.ProductOrService, r.Category,
                        r.ContactPerson, r.Email, r.PhoneNumber,
                        r.IsActive ? "Yes" : "No"
                    }));
            }

            var noContact = rows.Count(r =>
                string.IsNullOrWhiteSpace(r.Email) &&
                string.IsNullOrWhiteSpace(r.PhoneNumber));

            var model = new ReportViewModel<VendorReportRow>
            {
                Title = "Vendors",
                Description =
                    "Who we buy from, what they supply and how " +
                    "reachable they are.",
                Action = nameof(Vendors),
                Filter = filter,
                Rows = rows,
                Coverage = filter.IncludeInactive
                    ? "All vendors"
                    : "Vendors in use",
                Stats =
                {
                    new ReportStat("Vendors", rows.Count.ToString()),
                    new ReportStat(
                        "In use", rows.Count(r => r.IsActive).ToString()),
                    new ReportStat(
                        "Categories",
                        rows.Select(r => r.Category ?? "Uncategorised")
                            .Distinct().Count().ToString()),
                    new ReportStat(
                        "No contact details",
                        noContact.ToString(),
                        "no email or phone")
                }
            };

            var byCategory = new ReportBreakdown("By category", "vendors");

            foreach (var g in rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Category)
                    ? "Uncategorised"
                    : r.Category!)
                .OrderByDescending(g => g.Count()))
            {
                byCategory.Add(g.Key, g.Count());
            }

            var contactable = new ReportBreakdown(
                "Contact details held", "vendors")
                .Add("Email and phone",
                    rows.Count(r =>
                        !string.IsNullOrWhiteSpace(r.Email) &&
                        !string.IsNullOrWhiteSpace(r.PhoneNumber)),
                    "good")
                .Add("One of the two",
                    rows.Count(r =>
                        string.IsNullOrWhiteSpace(r.Email) !=
                        string.IsNullOrWhiteSpace(r.PhoneNumber)),
                    "warning")
                .Add("Neither", noContact, "critical");

            model.Breakdowns.AddRange(
                new[] { byCategory, contactable }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> ItActivity(
            ReportFilter filter,
            string? export)
        {
            // Defaults to the last 30 days, since the log is read far
            // more often for "what happened recently" than for all time.
            filter.From ??= DateTime.Today.AddDays(-30);

            var query = _context.ItActivities
                .AsNoTracking()
                .Include(a => a.RaisedByEmployee)
                .Include(a => a.Asset)
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(a => a.LoggedOn >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(a => a.LoggedOn <= filter.To.Value);

            if (filter.ActivityCategory.HasValue)
            {
                query = query.Where(a =>
                    a.Category == filter.ActivityCategory.Value);
            }

            if (filter.ActivityStatus.HasValue)
            {
                query = query.Where(a =>
                    a.Status == filter.ActivityStatus.Value);
            }

            if (filter.EmployeeId.HasValue)
            {
                query = query.Where(a =>
                    a.RaisedByEmployeeId == filter.EmployeeId.Value);
            }

            query = filter.Show switch
            {
                "open" => query.Where(a =>
                    a.Status != ItActivityStatus.Resolved),

                "notable" => query.Where(a => a.IsNotable),

                "hardware" => query.Where(a =>
                    a.Category == ItActivityCategory.Hardware),

                _ => query
            };

            var rows = await query
                .OrderByDescending(a => a.LoggedOn)
                .ThenByDescending(a => a.Id)
                .Select(a => new ItActivityReportRow
                {
                    LoggedOn = a.LoggedOn,
                    Category = a.Category,
                    Status = a.Status,
                    Summary = a.Summary,
                    ActionTaken = a.ActionTaken,
                    RaisedBy = a.RaisedByEmployee != null
                        ? a.RaisedByEmployee.FirstName + " " +
                          a.RaisedByEmployee.LastName
                        : (a.RaisedByOther ?? string.Empty),
                    Asset = a.Asset != null ? a.Asset.DeviceModel : string.Empty,
                    IsNotable = a.IsNotable,
                    ResolvedOn = a.ResolvedOn,
                    LoggedBy = a.LoggedBy,
                    DaysToResolve = a.ResolvedOn != null
                        ? EF.Functions.DateDiffDay(a.LoggedOn, a.ResolvedOn.Value)
                        : (int?)null
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "it-activity",
                    new[]
                    {
                        "Date", "Category", "Status", "What came up",
                        "What was done", "Raised by", "Machine", "Notable",
                        "Resolved on", "Days to resolve", "Logged by"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.LoggedOn.ToString("yyyy-MM-dd"),
                        r.Category.ToString(),
                        r.Status.ToString(),
                        r.Summary,
                        r.ActionTaken,
                        r.RaisedBy,
                        r.Asset,
                        r.IsNotable ? "Yes" : "No",
                        D(r.ResolvedOn),
                        r.DaysToResolve?.ToString(),
                        r.LoggedBy
                    }));
            }

            var resolved = rows
                .Where(r => r.Status == ItActivityStatus.Resolved)
                .ToList();

            var outstanding = rows.Count - resolved.Count;

            var closed = resolved
                .Where(r => r.DaysToResolve.HasValue)
                .Select(r => (double)r.DaysToResolve!.Value)
                .ToList();

            // Working days with nothing logged against them, which is
            // usually a gap in the record rather than a quiet day.
            var from = filter.From!.Value.Date;
            var to = (filter.To ?? DateTime.Today).Date;

            var loggedDays = rows
                .Select(r => r.LoggedOn.Date)
                .Distinct()
                .ToHashSet();

            var workingDays = 0;
            var unlogged = 0;

            for (var day = from;
                 day <= to && day <= DateTime.Today;
                 day = day.AddDays(1))
            {
                if (day.DayOfWeek == DayOfWeek.Saturday ||
                    day.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                workingDays++;

                if (!loggedDays.Contains(day))
                    unlogged++;
            }

            var model = new ReportViewModel<ItActivityReportRow>
            {
                Title = "IT activity",
                Description =
                    "Requests, incidents and tasks logged by the IT " +
                    "department, and how they were dealt with.",
                Action = nameof(ItActivity),
                Filter = filter,
                Rows = rows,
                Coverage = Period(filter) +
                    filter.Show switch
                    {
                        "open" => ", still outstanding",
                        "notable" => ", flagged notable only",
                        "hardware" => ", hardware only",
                        _ => string.Empty
                    },
                Stats =
                {
                    new ReportStat("Logged", rows.Count.ToString()),
                    new ReportStat(
                        "Resolved",
                        resolved.Count.ToString(),
                        rows.Count == 0
                            ? null
                            : $"{(decimal)resolved.Count / rows.Count:P0} of those shown"),
                    new ReportStat(
                        "Still outstanding",
                        outstanding.ToString(),
                        outstanding == 0 ? "nothing open" : "open or in progress"),
                    new ReportStat(
                        "Flagged notable",
                        rows.Count(r => r.IsNotable).ToString(),
                        "named in the weekly report")
                }
            };

            if (closed.Any())
            {
                model.Stats.Add(new ReportStat(
                    "Average to resolve",
                    $"{closed.Average():0.#} days",
                    "logged to resolved"));
            }

            if (workingDays > 0)
            {
                model.Stats.Add(new ReportStat(
                    "Days not logged",
                    unlogged.ToString(),
                    $"of {workingDays} working days"));
            }

            var byCategory = new ReportBreakdown("By category", "entries");

            foreach (var g in rows
                .GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count()))
            {
                byCategory.Add(g.Key.ToString(), g.Count());
            }

            var byStatus = new ReportBreakdown("By status", "entries")
                .Add(
                    "Open",
                    rows.Count(r => r.Status == ItActivityStatus.Open),
                    "critical")
                .Add(
                    "In progress",
                    rows.Count(r => r.Status == ItActivityStatus.InProgress),
                    "warning")
                .Add("Resolved", resolved.Count, "good");

            var byRaiser = new ReportBreakdown("Raised most often", "entries");

            foreach (var g in rows
                .Where(r => !string.IsNullOrWhiteSpace(r.RaisedBy))
                .GroupBy(r => r.RaisedBy)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byRaiser.Add(g.Key, g.Count());
            }

            var byMachine = new ReportBreakdown(
                "Machines involved most", "entries");

            foreach (var g in rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Asset))
                .GroupBy(r => r.Asset)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byMachine.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byCategory, byStatus, byRaiser, byMachine }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.AdminOrMd)]
        public async Task<IActionResult> Requisitions(
            ReportFilter filter,
            string? export)
        {
            var query = _context.Requisitions
                .AsNoTracking()
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.Vendor)
                .Include(r => r.Lines)
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(r => r.CreatedAt >= filter.From.Value);

            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Date.AddDays(1);
                query = query.Where(r => r.CreatedAt < to);
            }

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(r =>
                    r.Employee.DepartmentId == filter.DepartmentId.Value);
            }

            if (filter.EmployeeId.HasValue)
                query = query.Where(r => r.EmployeeId == filter.EmployeeId.Value);

            if (filter.VendorId.HasValue)
                query = query.Where(r => r.VendorId == filter.VendorId.Value);

            if (filter.RequisitionStatus.HasValue)
            {
                query = query.Where(r =>
                    r.Status == filter.RequisitionStatus.Value);
            }

            query = filter.Show switch
            {
                "awaiting" => query.Where(r =>
                    r.Status == RequisitionStatus.Submitted ||
                    r.Status == RequisitionStatus.ReviewApproved),

                "approved" => query.Where(r =>
                    r.Status == RequisitionStatus.Approved),

                "declined" => query.Where(r =>
                    r.Status == RequisitionStatus.Rejected ||
                    r.Status == RequisitionStatus.ReviewRejected),

                "overthreshold" => query.Where(r => r.RequiresMdApproval),

                _ => query
            };

            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RequisitionReportRow
                {
                    RequisitionNumber = r.RequisitionNumber,
                    RaisedBy = r.Employee.FirstName + " " + r.Employee.LastName,
                    Department = r.Employee.Department.Name,
                    Title = r.Title,
                    Status = r.Status,
                    Vendor = r.Vendor != null ? r.Vendor.Name : string.Empty,
                    Lines = r.Lines.Count,
                    EstimatedTotal = r.EstimatedTotal,
                    RequiresMdApproval = r.RequiresMdApproval,
                    NeededBy = r.NeededBy,
                    CreatedAt = r.CreatedAt,
                    SubmittedAt = r.SubmittedAt,
                    CompletedAt = r.CompletedAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                if (row.SubmittedAt.HasValue && row.CompletedAt.HasValue)
                {
                    row.TurnaroundDays =
                        (row.CompletedAt.Value - row.SubmittedAt.Value)
                            .TotalDays;
                }
            }

            if (export == "csv")
            {
                return Csv(
                    "requisitions",
                    new[]
                    {
                        "Number", "Raised by", "Department", "What for",
                        "Status", "Vendor", "Items", "Estimated total",
                        "Needed MD approval", "Needed by", "Raised",
                        "Submitted", "Decided", "Turnaround (days)"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.RequisitionNumber,
                        r.RaisedBy,
                        r.Department,
                        r.Title,
                        r.Status.ToString(),
                        r.Vendor,
                        r.Lines.ToString(),
                        r.EstimatedTotal.ToString("0.00"),
                        r.RequiresMdApproval ? "Yes" : "No",
                        D(r.NeededBy),
                        D(r.CreatedAt),
                        D(r.SubmittedAt),
                        D(r.CompletedAt),
                        r.TurnaroundDays?.ToString("0.#")
                    }));
            }

            var approved = rows
                .Where(r => r.Status == RequisitionStatus.Approved)
                .ToList();

            var awaiting = rows.Count(r =>
                r.Status == RequisitionStatus.Submitted ||
                r.Status == RequisitionStatus.ReviewApproved);

            var declined = rows.Count(r =>
                r.Status == RequisitionStatus.Rejected ||
                r.Status == RequisitionStatus.ReviewRejected);

            var decided = rows
                .Where(r => r.TurnaroundDays.HasValue)
                .Select(r => r.TurnaroundDays!.Value)
                .ToList();

            var model = new ReportViewModel<RequisitionReportRow>
            {
                Title = "Requisitions",
                Description =
                    "What has been asked for, what was approved and how " +
                    "long the decision took.",
                Action = nameof(Requisitions),
                Filter = filter,
                Rows = rows,
                Coverage = Period(filter) +
                    filter.Show switch
                    {
                        "awaiting" => ", awaiting a decision",
                        "approved" => ", approved only",
                        "declined" => ", declined only",
                        "overthreshold" => ", those needing MD approval",
                        _ => string.Empty
                    },
                Stats =
                {
                    new ReportStat("Requisitions", rows.Count.ToString()),
                    new ReportStat(
                        "Approved value",
                        approved.Sum(r => r.EstimatedTotal).ToString("N2"),
                        $"across {approved.Count} approved"),
                    new ReportStat(
                        "Awaiting a decision",
                        awaiting.ToString(),
                        awaiting == 0 ? "queues are clear" : "in review or with the MD"),
                    new ReportStat(
                        "Declined",
                        declined.ToString(),
                        rows.Count == 0
                            ? null
                            : $"{(decimal)declined / rows.Count:P0} of those shown")
                }
            };

            if (decided.Any())
            {
                model.Stats.Add(new ReportStat(
                    "Average turnaround",
                    $"{decided.Average():0.#} days",
                    "submitted to decided"));
            }

            var byStatus = new ReportBreakdown("By status", "requisitions")
                .Add(
                    "Draft",
                    rows.Count(r => r.Status == RequisitionStatus.Draft))
                .Add(
                    "Awaiting review",
                    rows.Count(r => r.Status == RequisitionStatus.Submitted),
                    "warning")
                .Add(
                    "Awaiting the MD",
                    rows.Count(r => r.Status == RequisitionStatus.ReviewApproved),
                    "warning")
                .Add(
                    "Returned",
                    rows.Count(r =>
                        r.Status == RequisitionStatus.ReturnedToRequester))
                .Add("Approved", approved.Count, "good")
                .Add("Declined", declined, "critical")
                .Add(
                    "Withdrawn",
                    rows.Count(r => r.Status == RequisitionStatus.Cancelled));

            var byDepartment = new ReportBreakdown(
                "Approved value by department", "");

            foreach (var g in approved
                .GroupBy(r => r.Department)
                .OrderByDescending(g => g.Sum(r => r.EstimatedTotal)))
            {
                byDepartment.Add(g.Key, g.Sum(r => r.EstimatedTotal));
            }

            var byRequester = new ReportBreakdown(
                "Most requisitions raised", "requisitions");

            foreach (var g in rows
                .GroupBy(r => r.RaisedBy)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byRequester.Add(g.Key, g.Count());
            }

            var byRoute = new ReportBreakdown(
                "Approval route", "requisitions")
                .Add(
                    "Review was final",
                    rows.Count(r =>
                        !r.RequiresMdApproval && r.SubmittedAt != null))
                .Add(
                    "Went to the MD",
                    rows.Count(r => r.RequiresMdApproval),
                    "warning");

            model.Breakdowns.AddRange(
                new[] { byStatus, byDepartment, byRoute, byRequester }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Maintenance(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;

            var query = _context.AssetMaintenances
                .AsNoTracking()
                .Include(m => m.Asset)
                    .ThenInclude(a => a.AssignedToEmployee)
                .Include(m => m.Vendor)
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(m => m.ScheduledFor >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(m => m.ScheduledFor <= filter.To.Value);

            if (filter.VendorId.HasValue)
                query = query.Where(m => m.VendorId == filter.VendorId.Value);

            query = filter.Show switch
            {
                "overdue" => query.Where(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor < today),

                "scheduled" => query.Where(m =>
                    m.Status == MaintenanceStatus.Scheduled),

                "completed" => query.Where(m =>
                    m.Status == MaintenanceStatus.Completed),

                "repairs" => query.Where(m =>
                    m.Type == MaintenanceType.Repair),

                _ => query
            };

            var rows = await query
                .OrderByDescending(m => m.ScheduledFor)
                .Select(m => new MaintenanceReportRow
                {
                    Asset = m.Asset.DeviceModel,
                    SerialNumber = m.Asset.SerialNumber,
                    HeldBy = m.Asset.AssignedToEmployee != null
                        ? m.Asset.AssignedToEmployee.FirstName + " " +
                          m.Asset.AssignedToEmployee.LastName
                        : string.Empty,
                    Type = m.Type,
                    Status = m.Status,
                    ScheduledFor = m.ScheduledFor,
                    CompletedOn = m.CompletedOn,
                    DoneBy = m.Vendor != null
                        ? m.Vendor.Name
                        : (m.PerformedBy ?? string.Empty),
                    Cost = m.Cost,
                    Notes = m.Notes,
                    IsOverdue =
                        m.Status == MaintenanceStatus.Scheduled &&
                        m.ScheduledFor < today,
                    DaysToComplete = m.CompletedOn != null
                        ? EF.Functions.DateDiffDay(
                            m.ScheduledFor, m.CompletedOn.Value)
                        : (int?)null
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "asset-maintenance",
                    new[]
                    {
                        "Machine", "Serial number", "Held by", "Work",
                        "Status", "Scheduled for", "Completed on",
                        "Days to complete", "Done by", "Cost",
                        "Work done / notes"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Asset,
                        r.SerialNumber,
                        r.HeldBy,
                        r.Type.ToString(),
                        r.IsOverdue ? "Overdue" : r.Status.ToString(),
                        r.ScheduledFor.ToString("yyyy-MM-dd"),
                        D(r.CompletedOn),
                        r.DaysToComplete?.ToString(),
                        r.DoneBy,
                        r.Cost?.ToString("0.00"),
                        r.Notes
                    }));
            }

            var completed = rows
                .Where(r => r.Status == MaintenanceStatus.Completed)
                .ToList();

            var overdue = rows.Count(r => r.IsOverdue);

            var turnaround = completed
                .Where(r => r.DaysToComplete.HasValue)
                .Select(r => (double)r.DaysToComplete!.Value)
                .ToList();

            var model = new ReportViewModel<MaintenanceReportRow>
            {
                Title = "Asset maintenance",
                Description =
                    "Work scheduled and done on the fleet, what it cost " +
                    "and who did it.",
                Action = nameof(Maintenance),
                Filter = filter,
                Rows = rows,
                Coverage = Period(filter) +
                    filter.Show switch
                    {
                        "overdue" => ", overdue work only",
                        "scheduled" => ", work still outstanding",
                        "completed" => ", completed work only",
                        "repairs" => ", repairs only",
                        _ => string.Empty
                    },
                Stats =
                {
                    new ReportStat("Jobs", rows.Count.ToString()),
                    new ReportStat(
                        "Completed",
                        completed.Count.ToString(),
                        rows.Count == 0
                            ? null
                            : $"{(decimal)completed.Count / rows.Count:P0} of those shown"),
                    new ReportStat(
                        "Overdue",
                        overdue.ToString(),
                        overdue == 0 ? "nothing outstanding" : "past the date"),
                    new ReportStat(
                        "Spend",
                        rows.Sum(r => r.Cost ?? 0m).ToString("N2"),
                        "on the work shown")
                }
            };

            if (turnaround.Any())
            {
                model.Stats.Add(new ReportStat(
                    "Average turnaround",
                    $"{turnaround.Average():0.#} days",
                    "scheduled to completed"));
            }

            var byType = new ReportBreakdown("Spend by work type", "");

            foreach (var g in rows
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Sum(r => r.Cost ?? 0m)))
            {
                byType.Add(g.Key.ToString(), g.Sum(r => r.Cost ?? 0m));
            }

            var byStatus = new ReportBreakdown("By status", "jobs")
                .Add("Overdue", overdue, "critical")
                .Add(
                    "Scheduled",
                    rows.Count(r =>
                        r.Status == MaintenanceStatus.Scheduled &&
                        !r.IsOverdue),
                    "warning")
                .Add("Completed", completed.Count, "good")
                .Add(
                    "Cancelled",
                    rows.Count(r => r.Status == MaintenanceStatus.Cancelled));

            var byMachine = new ReportBreakdown(
                "Most worked on", "jobs");

            foreach (var g in rows
                .GroupBy(r => r.Asset)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Sum(r => r.Cost ?? 0m))
                .Take(8))
            {
                byMachine.Add(g.Key, g.Count());
            }

            var byProvider = new ReportBreakdown("Spend by provider", "");

            foreach (var g in rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.DoneBy)
                    ? "Not recorded"
                    : r.DoneBy)
                .OrderByDescending(g => g.Sum(r => r.Cost ?? 0m))
                .Take(8))
            {
                byProvider.Add(g.Key, g.Sum(r => r.Cost ?? 0m));
            }

            model.Breakdowns.AddRange(
                new[] { byStatus, byType, byMachine, byProvider }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Licences(
            ReportFilter filter,
            string? export)
        {
            var today = DateTime.Today;
            var soon = today.AddDays(LicenseController.ExpiringWindowDays);

            var query = _context.Licenses
                .AsNoTracking()
                .Include(l => l.Vendor)
                .Include(l => l.AssignedToEmployee)
                .Include(l => l.Renewals)
                .AsQueryable();

            if (!filter.IncludeInactive)
                query = query.Where(l => l.Status == LicenseStatus.Active);

            if (filter.LicenceType.HasValue)
                query = query.Where(l => l.Type == filter.LicenceType.Value);

            if (filter.VendorId.HasValue)
                query = query.Where(l => l.VendorId == filter.VendorId.Value);

            query = filter.Show switch
            {
                "expired" => query.Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate < today),

                "expiring" => query.Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate >= today &&
                    l.ExpiryDate <= soon),

                "autorenew" => query.Where(l => l.AutoRenews),

                _ => query
            };

            var rows = await query
                .OrderBy(l => l.ExpiryDate)
                .Select(l => new LicenseReportRow
                {
                    Name = l.Name,
                    Type = l.Type,
                    Status = l.Status,
                    Reference = l.Reference,
                    Vendor = l.Vendor != null ? l.Vendor.Name : string.Empty,
                    HeldBy = l.AssignedToEmployee != null
                        ? l.AssignedToEmployee.FirstName + " " +
                          l.AssignedToEmployee.LastName
                        : string.Empty,
                    Seats = l.Seats,
                    StartDate = l.StartDate,
                    ExpiryDate = l.ExpiryDate,
                    Cost = l.Cost,
                    AutoRenews = l.AutoRenews,
                    Renewals = l.Renewals.Count,
                    LastRenewedOn = l.Renewals
                        .OrderByDescending(r => r.RenewedOn)
                        .Select(r => (DateTime?)r.RenewedOn)
                        .FirstOrDefault(),
                    DaysToExpiry = EF.Functions.DateDiffDay(
                        today, l.ExpiryDate)
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "licences",
                    new[]
                    {
                        "Licence", "Type", "Status", "Reference", "Vendor",
                        "Held by", "Seats", "Started", "Expires",
                        "Days to expiry", "Cost", "Auto renews",
                        "Renewals", "Last renewed"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Name,
                        r.Type.ToString(),
                        r.Status.ToString(),
                        r.Reference,
                        r.Vendor,
                        r.HeldBy,
                        r.Seats?.ToString(),
                        D(r.StartDate),
                        r.ExpiryDate.ToString("yyyy-MM-dd"),
                        r.DaysToExpiry.ToString(),
                        r.Cost?.ToString("0.00"),
                        r.AutoRenews ? "Yes" : "No",
                        r.Renewals.ToString(),
                        D(r.LastRenewedOn)
                    }));
            }

            var live = rows
                .Where(r => r.Status == LicenseStatus.Active)
                .ToList();

            var expired = live.Count(r => r.DaysToExpiry < 0);

            var expiring = live.Count(r =>
                r.DaysToExpiry >= 0 &&
                r.DaysToExpiry <= LicenseController.ExpiringWindowDays);

            var model = new ReportViewModel<LicenseReportRow>
            {
                Title = "Licences and renewals",
                Description =
                    "What expires when, what it costs and who supplies it.",
                Action = nameof(Licences),
                Filter = filter,
                Rows = rows,
                Coverage = Slice(filter, soon),
                Stats =
                {
                    new ReportStat("Licences", rows.Count.ToString()),
                    new ReportStat(
                        "Lapsed",
                        expired.ToString(),
                        expired == 0 ? "none overdue" : "past the renewal date"),
                    new ReportStat(
                        "Expiring soon",
                        expiring.ToString(),
                        $"within {LicenseController.ExpiringWindowDays} days"),
                    new ReportStat(
                        "Annual cost",
                        live.Sum(r => r.Cost ?? 0m).ToString("N2"),
                        "across live licences")
                }
            };

            var byType = new ReportBreakdown("By type", "licences");

            foreach (var g in rows
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Count()))
            {
                byType.Add(g.Key.ToString(), g.Count());
            }

            var byVendor = new ReportBreakdown("Cost by vendor", "");

            foreach (var g in live
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Vendor)
                    ? "Not on the register"
                    : r.Vendor)
                .OrderByDescending(g => g.Sum(r => r.Cost ?? 0m))
                .Take(8))
            {
                byVendor.Add(g.Key, g.Sum(r => r.Cost ?? 0m));
            }

            var byWindow = new ReportBreakdown("Renewal window", "licences")
                .Add("Lapsed", expired, "critical")
                .Add(
                    $"Next {LicenseController.ExpiringWindowDays} days",
                    expiring,
                    "warning")
                .Add(
                    "Later this year",
                    live.Count(r =>
                        r.DaysToExpiry > LicenseController.ExpiringWindowDays &&
                        r.DaysToExpiry <= 365))
                .Add(
                    "Beyond a year",
                    live.Count(r => r.DaysToExpiry > 365),
                    "good");

            model.Breakdowns.AddRange(
                new[] { byWindow, byType, byVendor }
                    .Where(b => b.Total > 0));

            await LoadLookups();

            return View(model);
        }

        /// <summary>
        /// Describes the licence slice in words, so an exported or
        /// printed copy says what it was showing.
        /// </summary>
        private static string Slice(ReportFilter filter, DateTime soon) =>
            filter.Show switch
            {
                "expired" => "Live licences already past their expiry date",
                "expiring" =>
                    "Live licences expiring on or before " +
                    soon.ToString("d MMM yyyy"),
                "autorenew" => "Licences set to renew automatically",
                _ => filter.IncludeInactive
                    ? "All licences, cancelled ones included"
                    : "Live licences"
            };

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> AuditTrail(
            ReportFilter filter,
            string? export)
        {
            filter.From ??= DateTime.Today.AddDays(-30);

            var query = _context.AuditLogs
                .Include(a => a.Employee)
                .AsNoTracking()
                .AsQueryable();

            if (filter.From.HasValue)
                query = query.Where(a => a.Timestamp >= filter.From.Value);

            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < to);
            }

            if (!string.IsNullOrWhiteSpace(filter.AuditAction))
            {
                query = query.Where(a =>
                    a.Action == filter.AuditAction);
            }

            var rows = await query
                .OrderByDescending(a => a.Timestamp)
                .Take(2000)
                .Select(a => new AuditReportRow
                {
                    Timestamp = a.Timestamp,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Actor = a.Employee != null
                        ? a.Employee.FirstName + " " + a.Employee.LastName
                        : "System",
                    Description = a.Description
                })
                .ToListAsync();

            if (export == "csv")
            {
                return Csv(
                    "audit-trail",
                    new[]
                    {
                        "When", "Action", "Record type", "Record",
                        "Actor", "Detail"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                        r.Action, r.EntityName, r.EntityId, r.Actor,
                        r.Description
                    }));
            }

            ViewBag.Actions = await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            var model = new ReportViewModel<AuditReportRow>
            {
                Title = "Audit trail",
                Description =
                    "Who did what, and when. Newest first, capped at " +
                    "2000 entries.",
                Action = nameof(AuditTrail),
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Entries", rows.Count.ToString()),
                    new ReportStat(
                        "Distinct actions",
                        rows.Select(r => r.Action).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "People involved",
                        rows.Select(r => r.Actor).Distinct()
                            .Count().ToString()),
                    new ReportStat(
                        "Busiest day",
                        rows.Count == 0
                            ? "--"
                            : rows.GroupBy(r => r.Timestamp.Date)
                                .OrderByDescending(g => g.Count())
                                .First().Key.ToString("d MMM"))
                },
                Coverage = Period(filter)
            };

            var byAction = new ReportBreakdown("By action", "entries");

            foreach (var g in rows
                .GroupBy(r => r.Action)
                .OrderByDescending(g => g.Count()))
            {
                byAction.Add(g.Key, g.Count());
            }

            var byActor = new ReportBreakdown("Most active", "entries");

            foreach (var g in rows
                .GroupBy(r => r.Actor)
                .OrderByDescending(g => g.Count())
                .Take(8))
            {
                byActor.Add(g.Key, g.Count());
            }

            var byRecord = new ReportBreakdown("By record type", "entries");

            foreach (var g in rows
                .GroupBy(r => r.EntityName)
                .OrderByDescending(g => g.Count()))
            {
                byRecord.Add(g.Key, g.Count());
            }

            model.Breakdowns.AddRange(
                new[] { byAction, byActor, byRecord }
                    .Where(b => b.Items.Count > 0));

            await LoadLookups();

            return View(model);
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private FileContentResult Csv(
            string name,
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string?>> rows)
        {
            return File(
                CsvExportService.Build(headers, rows),
                "text/csv",
                CsvExportService.FileName(name));
        }

        /// <summary>
        /// Says in words what the date filter currently covers, so a
        /// printed report is not ambiguous about its period.
        /// </summary>
        private static string Period(ReportFilter filter)
        {
            if (filter.From.HasValue && filter.To.HasValue)
            {
                return $"{filter.From.Value:d MMM yyyy} to " +
                       $"{filter.To.Value:d MMM yyyy}";
            }

            if (filter.From.HasValue)
                return $"From {filter.From.Value:d MMM yyyy}";

            if (filter.To.HasValue)
                return $"Up to {filter.To.Value:d MMM yyyy}";

            return "All dates";
        }

        private static string D(DateTime? value) =>
            value?.ToString("yyyy-MM-dd") ?? string.Empty;

        private static string N(decimal value) =>
            value.ToString("0.##");

        private async Task LoadLookups()
        {
            ViewBag.Departments = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .OrderBy(e => e.FirstName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToListAsync();

            ViewBag.LeaveTypes = await _context.LeaveTypes
                .OrderBy(l => l.Name)
                .Select(l => new SelectListItem(l.Name, l.Id.ToString()))
                .ToListAsync();

            ViewBag.Courses = await _context.TrainingCourses
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString()))
                .ToListAsync();

            ViewBag.Categories = await _context.DocumentCategories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
                .ToListAsync();

            ViewBag.Vendors = await _context.Vendors
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
                .ToListAsync();

            ViewBag.VendorCategories = await _context.Vendors
                .Where(v => v.Category != null && v.Category != "")
                .Select(v => v.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Companies = await _context.Assets
                .Where(a => a.Company != null && a.Company != "")
                .Select(a => a.Company!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
