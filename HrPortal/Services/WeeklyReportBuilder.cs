using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Scaffolds a weekly report draft: the standing section
    /// structure, the numbers drawn from the activity log, and last
    /// week's priorities brought forward.
    ///
    /// Everything it writes is a starting point the author then
    /// edits. Nothing here is recomputed afterwards, so a report says
    /// what it said on the day it was issued.
    /// </summary>
    public class WeeklyReportBuilder
    {
        private readonly ApplicationDbContext _context;

        public WeeklyReportBuilder(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// The standing structure of the report. Headings are editable
        /// afterwards; this is only what a new draft starts with.
        /// </summary>
        private static readonly (int Order, string Heading)[] Template =
        {
            (1, "Network & Infrastructure"),
            (2, "Cybersecurity & Access Management"),
            (3, "End-User Hardware Support"),
            (4, "Website Administration & Content"),
            (5, "General IT Support")
        };

        /// <summary>
        /// Which section each kind of activity is reported under.
        /// </summary>
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

                    // The workstream and its status usually run across
                    // several weeks, so they are worth carrying. The
                    // text is not, because it describes last week.
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
                // The one section that is worth a sentence even when
                // there is nothing to report, because "nothing broke"
                // is itself the finding.
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

            // Anything flagged notable is named rather than only counted.
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
    }
}
