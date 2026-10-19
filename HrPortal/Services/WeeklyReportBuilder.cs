using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class WeeklyReportBuilder
    {
        private readonly ApplicationDbContext _context;

        public WeeklyReportBuilder(ApplicationDbContext context)
        {
            _context = context;
        }

        private static readonly (int Order, string Heading)[] Template =
        {
            (1, "Network & Infrastructure"),
            (2, "Cybersecurity & Access Management"),
            (3, "End-User Hardware Support"),
            (4, "Website Administration & Content"),
            (5, "General IT Support")
        };

        private static int SectionFor(ItActivityCategory category) => category switch
        {
            ItActivityCategory.Network => 1,
            ItActivityCategory.Access => 2,
            ItActivityCategory.Hardware => 3,
            ItActivityCategory.Website => 4,
            _ => 5
        };

        public async Task<WeeklyReport> BuildDraftAsync(
            DateTime periodStart,
            DateTime periodEnd,
            string preparedBy)
        {
            var activities = await _context.ItActivities
                .Include(a => a.RaisedByEmployee)
                .Include(a => a.Asset)
                .Where(a =>
                    a.LoggedOn >= periodStart &&
                    a.LoggedOn <= periodEnd)
                .OrderBy(a => a.LoggedOn)
                .ToListAsync();

            var previous = await _context.WeeklyReports
                .Include(r => r.Sections)
                .Include(r => r.Priorities)
                .Where(r => r.PeriodStart < periodStart)
                .OrderByDescending(r => r.PeriodStart)
                .FirstOrDefaultAsync();

            var report = new WeeklyReport
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                PreparedBy = preparedBy,
                Status = WeeklyReportStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var (order, heading) in Template)
            {
                var carried = previous?.Sections
                    .FirstOrDefault(s => s.Order == order);

                report.Sections.Add(new WeeklyReportSection
                {
                    Order = order,
                    Heading = heading,

                    Workstream = carried?.Workstream,
                    WorkstreamStatus =
                        carried?.WorkstreamStatus ?? WorkstreamStatus.None,

                    Body = BodyFor(order, activities, periodStart, periodEnd)
                });
            }

            var nextOrder = 1;

            if (previous != null)
            {
                foreach (var priority in previous.Priorities.OrderBy(p => p.Order))
                {
                    report.Priorities.Add(new WeeklyReportPriority
                    {
                        Order = nextOrder++,
                        Text = priority.Text,
                        CarriedForward = true
                    });
                }
            }

            foreach (var open in activities
                .Where(a => a.Status != ItActivityStatus.Resolved)
                .OrderBy(a => a.LoggedOn))
            {
                report.Priorities.Add(new WeeklyReportPriority
                {
                    Order = nextOrder++,
                    Text = $"Close out: {open.Summary}",
                    CarriedForward = false
                });
            }

            return report;
        }

        private static string? BodyFor(
            int order,
            List<ItActivity> activities,
            DateTime periodStart,
            DateTime periodEnd)
        {
            var mine = activities
                .Where(a => SectionFor(a.Category) == order)
                .ToList();

            var lines = new List<string>();

            if (order == 3)
            {
                if (!mine.Any())
                {
                    lines.Add(
                        "No malfunctioning equipment or newly reported " +
                        "end-user hardware issues were recorded during the " +
                        "reporting period. The absence of new incidents " +
                        "indicates that the existing hardware environment " +
                        "remained operational throughout the week.");
                }
                else
                {
                    lines.Add(
                        $"{Count(mine.Count, "end-user hardware issue")} " +
                        "recorded during the reporting period.");
                }
            }
            else if (order == 5)
            {
                if (mine.Any())
                {
                    var categories = mine
                        .Select(a => a.Category.ToString().ToLowerInvariant())
                        .Distinct()
                        .OrderBy(c => c)
                        .ToList();

                    lines.Add(
                        $"{Count(mine.Count, "support request")} handled " +
                        "during the reporting period, covering " +
                        $"{Join(categories)}-related matters, with " +
                        "assistance provided according to the nature and " +
                        "urgency of each request.");

                    var resolved = mine.Count(a =>
                        a.Status == ItActivityStatus.Resolved);

                    if (resolved < mine.Count)
                    {
                        lines.Add(
                            $"{resolved} of {mine.Count} were resolved " +
                            "within the period; the remainder carry into " +
                            "the next reporting period.");
                    }
                }
            }

            foreach (var notable in mine.Where(a => a.IsNotable))
            {
                var line = notable.Summary.TrimEnd('.');

                if (!string.IsNullOrWhiteSpace(notable.ActionTaken))
                    line += $". {notable.ActionTaken.Trim().TrimEnd('.')}";

                lines.Add(line + ".");
            }

            return lines.Any() ? string.Join("\n", lines) : null;
        }

        private static string Count(int n, string noun) =>
            n == 1 ? $"One {noun} was" : $"{n} {noun}s were";

        private static string Join(List<string> items) =>
            items.Count switch
            {
                0 => string.Empty,
                1 => items[0],
                _ => string.Join(", ", items.Take(items.Count - 1)) +
                     " and " + items[^1]
            };
        public static DateTime NextPeriodStart(DateTime? lastPeriodStart)
        {
            if (lastPeriodStart.HasValue)
                return lastPeriodStart.Value.AddDays(7);

            var today = DateTime.Today;

            return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        }

        public static string? NormaliseBody(string? body)
        {
            var lines = WeeklyReportPdfService.Lines(body);

            return lines.Any() ? string.Join("\n", lines) : null;
        }

        public async Task<int> UnloggedDaysAsync(DateTime periodStart)
        {
            var periodEnd = periodStart.AddDays(4);

            var logged = await _context.ItActivities
                .Where(a =>
                    a.LoggedOn >= periodStart &&
                    a.LoggedOn <= periodEnd)
                .Select(a => a.LoggedOn.Date)
                .Distinct()
                .CountAsync();

            var elapsed = Enumerable.Range(0, 5)
                .Select(offset => periodStart.AddDays(offset))
                .Count(d => d <= DateTime.Today);

            return Math.Max(0, elapsed - logged);
        }

        public async Task ReplacePrioritiesAsync(
            WeeklyReport report,
            string? text)
        {
            var lines = WeeklyReportPdfService.Lines(text);

            var previousTexts = await _context.WeeklyReportPriorities
                .Where(p => p.Report.PeriodStart < report.PeriodStart)
                .Select(p => p.Text)
                .ToListAsync();

            _context.WeeklyReportPriorities.RemoveRange(report.Priorities);

            report.Priorities = lines
                .Select((line, index) => new WeeklyReportPriority
                {
                    WeeklyReportId = report.Id,
                    Order = index + 1,
                    Text = line,
                    CarriedForward = previousTexts.Contains(line)
                })
                .ToList();
        }
    }
}
