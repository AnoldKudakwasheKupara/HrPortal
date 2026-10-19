using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reporting
{
    public class LeaveReportService
    {
        private readonly ApplicationDbContext _context;

        public LeaveReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportPayload> LeaveRequestsAsync(
            ReportFilter filter,
            bool export)
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

            if (export)
            {
                return ReportPayload.ForCsv(
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
                        r.LeaveType, ReportMath.D(r.StartDate), ReportMath.D(r.EndDate),
                        ReportMath.N(r.Days), r.Status.ToString(), ReportMath.D(r.SubmittedAt),
                        ReportMath.D(r.CompletedAt), r.TurnaroundDays?.ToString("0.#")
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
                Action = "LeaveRequests",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Requests", rows.Count.ToString()),
                    new ReportStat(
                        "Approved",
                        approved.Count.ToString(),
                        ReportMath.N(approved.Sum(r => r.Days)) + " days"),
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
                Coverage = ReportMath.Period(filter)
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

            model.Trends.Add(ReportMath.MonthlyTrend(
                "Days booked by month",
                rows.Select(r => (r.StartDate, r.Days)),
                "days",
                "0.##"));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> LeaveBalancesAsync(
            ReportFilter filter,
            bool export)
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

            if (export)
            {
                return ReportPayload.ForCsv(
                    "leave-balances",
                    new[]
                    {
                        "Employee", "Department", "Leave type", "Year",
                        "Allocated", "Used", "Pending", "Remaining"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, r.LeaveType,
                        r.Year.ToString(), ReportMath.N(r.Allocated), ReportMath.N(r.Used),
                        ReportMath.N(r.Pending), ReportMath.N(r.Remaining)
                    }));
            }

            var model = new ReportViewModel<LeaveBalanceReportRow>
            {
                Title = "Leave balances",
                Description =
                    "Entitlement, what has been taken and what is left.",
                Action = "LeaveBalances",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Balances", rows.Count.ToString()),
                    new ReportStat(
                        "Allocated", ReportMath.N(rows.Sum(r => r.Allocated)), "days"),
                    new ReportStat(
                        "Used", ReportMath.N(rows.Sum(r => r.Used)), "days"),
                    new ReportStat(
                        "Pending", ReportMath.N(rows.Sum(r => r.Pending)), "days"),
                    new ReportStat(
                        "Remaining", ReportMath.N(rows.Sum(r => r.Remaining)), "days"),
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

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> AbsenceAsync(
            ReportFilter filter,
            bool export)
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

            if (export)
            {
                return ReportPayload.ForCsv(
                    "absence-summary",
                    new[]
                    {
                        "Employee", "Department", "Approved days",
                        "Approved requests", "Days in progress",
                        "Rejected requests", "Last day of leave"
                    },
                    rows.Select(r => new string?[]
                    {
                        r.Employee, r.Department, ReportMath.N(r.ApprovedDays),
                        r.ApprovedRequests.ToString(), ReportMath.N(r.PendingDays),
                        r.RejectedRequests.ToString(), ReportMath.D(r.LastLeaveEnd)
                    }));
            }

            var model = new ReportViewModel<AbsenceReportRow>
            {
                Title = "Absence summary",
                Description =
                    "Days taken per person over the period, highest first.",
                Action = "Absence",
                Filter = filter,
                Rows = rows,
                Stats =
                {
                    new ReportStat("Employees", rows.Count.ToString(),
                        "with leave in the period"),
                    new ReportStat(
                        "Approved days", ReportMath.N(rows.Sum(r => r.ApprovedDays))),
                    new ReportStat(
                        "Average per person",
                        rows.Count == 0
                            ? "--"
                            : ReportMath.N(rows.Sum(r => r.ApprovedDays) / rows.Count)),
                    new ReportStat(
                        "Days in progress", ReportMath.N(rows.Sum(r => r.PendingDays)))
                },
                Coverage = ReportMath.Period(filter)
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

            return ReportPayload.ForView(model);
        }
    }
}
