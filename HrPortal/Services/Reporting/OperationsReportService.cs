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
    }
}
