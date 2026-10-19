using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services.Reporting
{
    public class OperationsReportService
    {
        private readonly ApplicationDbContext _context;

        public OperationsReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ReportPayload> ItActivityAsync(
            ReportFilter filter,
            bool export)
        {
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

            if (export)
            {
                return ReportPayload.ForCsv(
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
                        ReportMath.D(r.ResolvedOn),
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
                Action = "ItActivity",
                Filter = filter,
                Rows = rows,
                Coverage = ReportMath.Period(filter) +
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

            model.Trends.Add(ReportMath.MonthlyTrend(
                "Entries logged by month",
                rows.Select(r => (r.LoggedOn, 1m)),
                "entries"));

            return ReportPayload.ForView(model);
        }

        public async Task<ReportPayload> RequisitionsAsync(
            ReportFilter filter,
            bool export)
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

            if (export)
            {
                return ReportPayload.ForCsv(
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
                        ReportMath.D(r.NeededBy),
                        ReportMath.D(r.CreatedAt),
                        ReportMath.D(r.SubmittedAt),
                        ReportMath.D(r.CompletedAt),
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
                Action = "Requisitions",
                Filter = filter,
                Rows = rows,
                Coverage = ReportMath.Period(filter) +
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

            model.Trends.Add(ReportMath.MonthlyTrend(
                "Approved value by month",
                approved
                    .Where(r => r.CompletedAt != null)
                    .Select(r => (r.CompletedAt!.Value, r.EstimatedTotal)),
                "approved",
                "N2"));

            return ReportPayload.ForView(model);
        }
    }
}
