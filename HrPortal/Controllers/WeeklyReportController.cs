using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Everybody writes their own weekly report. While it is a draft
    /// only they can see it; once submitted, their supervisor and the
    /// Managing Director can read it too. The MD has an overview of
    /// everybody's week, and a supervisor the same for their team.
    ///
    /// Reports from before this, written by an administrator for the
    /// IT department as a whole, belong to nobody in particular. They
    /// stay readable by administrators and the MD, and an
    /// administrator can still look after them.
    /// </summary>
    [Authorize]
    public class WeeklyReportController : Controller
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;
        private readonly WeeklyReportBuilder _builder;
        private readonly WeeklyReportPdfService _pdf;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;
        private readonly NotificationService _notifications;

        public WeeklyReportController(
            ApplicationDbContext context,
            WeeklyReportBuilder builder,
            WeeklyReportPdfService pdf,
            AuditService audit,
            CurrentUserService currentUser,
            NotificationService notifications)
        {
            _context = context;
            _builder = builder;
            _pdf = pdf;
            _audit = audit;
            _currentUser = currentUser;
            _notifications = notifications;
        }

        private bool IsMd => User.IsInRole(Roles.Md);

        private bool IsAdmin => User.IsInRole(Roles.Administrator);

        private async Task<int?> MeAsync() =>
            await _currentUser.GetEmployeeIdAsync();

        private async Task<bool> SupervisesAsync(int? me, int? employeeId) =>
            me != null &&
            employeeId != null &&
            await _context.Employees.AnyAsync(e =>
                e.Id == employeeId && e.SupervisorId == me);

        private async Task<bool> CanReadAsync(WeeklyReport report)
        {
            if (report.EmployeeId == null)
                return IsAdmin || IsMd;

            var me = await MeAsync();

            if (report.EmployeeId == me)
                return true;

            if (report.Status != WeeklyReportStatus.Final)
                return false;

            return IsMd || await SupervisesAsync(me, report.EmployeeId);
        }

        /// <summary>
        /// Only a draft can be changed, and only by whoever it belongs
        /// to: its author, or an administrator for the old IT reports.
        /// </summary>
        private async Task<bool> OwnsAsync(WeeklyReport report) =>
            report.EmployeeId == null
                ? IsAdmin
                : report.EmployeeId == await MeAsync();

        public async Task<IActionResult> Index(int page = 1)
        {
            var me = await MeAsync();
            var thisWeek = WeeklyReportBuilder.WeekStart(DateTime.Today);

            ViewBag.HasEmployeeRecord = me != null;
            ViewBag.ThisWeek = thisWeek;
            ViewBag.IsMd = IsMd;
            ViewBag.HasTeam = me != null &&
                await _context.Employees.AnyAsync(e =>
                    e.SupervisorId == me && e.IsActive);

            ViewBag.ThisWeeksReport = me == null
                ? null
                : await _context.WeeklyReports
                    .Where(r => r.EmployeeId == me && r.PeriodStart == thisWeek)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync();

            ViewBag.Earlier = IsAdmin || IsMd
                ? await _context.WeeklyReports
                    .AsNoTracking()
                    .Where(r => r.EmployeeId == null)
                    .OrderByDescending(r => r.PeriodStart)
                    .ToListAsync()
                : new List<WeeklyReport>();

            var mine = await _context.WeeklyReports
                .AsNoTracking()
                .Include(r => r.Sections)
                .Where(r => me != null && r.EmployeeId == me)
                .OrderByDescending(r => r.PeriodStart)
                .ToPagedResultAsync(page, PageSize);

            return View(mine);
        }

        /// <summary>
        /// Everybody's week at a glance: who has submitted, who has a
        /// draft going and who has not started, with what each
        /// submitted report says. The MD sees all staff; a supervisor
        /// sees the people who report to them.
        /// </summary>
        public async Task<IActionResult> Overview(DateTime? week)
        {
            var me = await MeAsync();
            var allStaff = IsMd;

            if (!allStaff &&
                (me == null ||
                 !await _context.Employees.AnyAsync(e => e.SupervisorId == me)))
            {
                return Forbid();
            }

            var start = WeeklyReportBuilder.WeekStart(week ?? DateTime.Today);

            var people = _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive);

            people = allStaff
                ? people.Where(e => e.UserId != null)
                : people.Where(e => e.SupervisorId == me);

            var staff = await people
                .Select(e => new
                {
                    e.Id,
                    Name = e.FirstName + " " + e.LastName,
                    e.JobTitle,
                    Department = e.Department == null ? null : e.Department.Name,
                    Supervisor = e.Supervisor == null
                        ? null
                        : e.Supervisor.FirstName + " " + e.Supervisor.LastName
                })
                .ToListAsync();

            var ids = staff.Select(s => s.Id).ToList();

            var reports = await _context.WeeklyReports
                .AsNoTracking()
                .Include(r => r.Sections)
                .Where(r =>
                    r.PeriodStart == start &&
                    r.EmployeeId != null &&
                    ids.Contains(r.EmployeeId.Value))
                .ToDictionaryAsync(r => r.EmployeeId!.Value);

            var model = new WeeklyOverviewViewModel
            {
                WeekStart = start,
                AllStaff = allStaff,
                Rows = staff
                    .Select(s =>
                    {
                        reports.TryGetValue(s.Id, out var report);

                        var submitted = report?.Status == WeeklyReportStatus.Final;

                        return new WeeklyOverviewRow
                        {
                            EmployeeId = s.Id,
                            Name = s.Name,
                            JobTitle = s.JobTitle,
                            Department = s.Department,
                            Supervisor = s.Supervisor,
                            ReportId = submitted ? report!.Id : null,
                            Progress = report == null
                                ? WeeklyProgress.NotStarted
                                : submitted
                                    ? WeeklyProgress.Submitted
                                    : WeeklyProgress.Draft,
                            SubmittedOn = submitted ? report!.FinalisedOn : null,
                            Items = submitted ? report!.Sections.Count(Filled) : 0,
                            Completed = submitted
                                ? report!.Sections.Count(x =>
                                    x.WorkstreamStatus == WorkstreamStatus.Completed)
                                : 0,
                            Summary = submitted ? report!.ExecutiveOverview : null
                        };
                    })
                    .OrderBy(r => r.Department)
                    .ThenBy(r => r.Name)
                    .ToList()
            };

            return View(model);
        }

        public IActionResult Create() =>
            View(new WeeklyReportCreateViewModel
            {
                PeriodStart = WeeklyReportBuilder.WeekStart(DateTime.Today)
            });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WeeklyReportCreateViewModel model)
        {
            var me = await MeAsync();

            if (me == null)
            {
                TempData["Error"] =
                    "Your sign-in is not linked to an employee record, so " +
                    "there is nobody to write a report for. Ask HR.";

                return RedirectToAction(nameof(Index));
            }

            var start = WeeklyReportBuilder.WeekStart(model.PeriodStart);

            var existing = await _context.WeeklyReports
                .Where(r => r.EmployeeId == me && r.PeriodStart == start)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                TempData["Success"] = "You already have a report for that week.";

                return RedirectToAction(nameof(Details), new { id = existing });
            }

            var owner = await _context.Employees
                .Include(e => e.Department)
                .FirstAsync(e => e.Id == me);

            var report = await _builder.BuildDraftAsync(
                owner,
                start,
                start.AddDays(4),
                $"{owner.FirstName} {owner.LastName}");

            _context.WeeklyReports.Add(report);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report started for {report.PeriodStart:dd MMM yyyy}.");

            return RedirectToAction(nameof(Edit), new { id = report.Id });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null || !await OwnsAsync(report))
                return NotFound();

            if (report.Status == WeeklyReportStatus.Final)
                return RedirectToAction(nameof(Details), new { id });

            var previous = await _context.WeeklyReports
                .AsNoTracking()
                .Include(r => r.Sections)
                .Where(r =>
                    r.EmployeeId == report.EmployeeId &&
                    r.PeriodStart < report.PeriodStart)
                .OrderByDescending(r => r.PeriodStart)
                .FirstOrDefaultAsync();

            ViewBag.PreviousPeriod = previous == null
                ? null
                : $"{previous.PeriodStart:dd MMM} to " +
                  $"{previous.PeriodEnd:dd MMM yyyy}";

            return View(ToEditModel(report, previous));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            WeeklyReportEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var report = await LoadFullAsync(id);

            if (report == null || !await OwnsAsync(report))
                return NotFound();

            if (report.Status == WeeklyReportStatus.Final)
            {
                TempData["Error"] =
                    "This report has been submitted. Take it back first " +
                    "to change it.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                var fresh = ToEditModel(report, null);
                model.OwnerName = fresh.OwnerName;
                model.OwnerDepartment = fresh.OwnerDepartment;
                model.FixedHeadings = fresh.FixedHeadings;

                return View(model);
            }

            report.ExecutiveOverview = model.ExecutiveOverview?.Trim();

            ApplySections(report, model);

            await _builder.ReplacePrioritiesAsync(report, model.PrioritiesText);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} edited.");

            if (Request.Form["thenSubmit"] == "true")
                return await SubmitAsync(report);

            TempData["Success"] = "Saved. It is still a draft only you can see.";

            if (Request.Form["thenPreview"] == "true")
                return RedirectToAction(nameof(Details), new { id = report.Id });

            return RedirectToAction(nameof(Edit), new { id = report.Id });
        }

        /// <summary>
        /// The IT headings are fixed, so only their contents change.
        /// Anybody else's pieces of work can be added, renamed and
        /// taken out; an item left with nothing in it is dropped.
        /// </summary>
        private void ApplySections(
            WeeklyReport report,
            WeeklyReportEditViewModel model)
        {
            var fixedHeadings = HasFixedHeadings(report);

            foreach (var posted in model.Sections)
            {
                var section = posted.Id == 0
                    ? null
                    : report.Sections.FirstOrDefault(s => s.Id == posted.Id);

                var heading = posted.Heading?.Trim();
                var body = WeeklyReportBuilder.NormaliseBody(posted.Body);
                var workstream = posted.Workstream?.Trim();

                var empty =
                    string.IsNullOrEmpty(heading) &&
                    body == null &&
                    string.IsNullOrEmpty(workstream);

                if (!fixedHeadings && (posted.Remove || empty))
                {
                    if (section != null)
                        _context.WeeklyReportSections.Remove(section);

                    continue;
                }

                if (section == null)
                {
                    if (fixedHeadings)
                        continue;

                    section = new WeeklyReportSection();
                    report.Sections.Add(section);
                }

                if (!fixedHeadings)
                    section.Heading = string.IsNullOrEmpty(heading) ? "Other work" : heading;

                section.Workstream = workstream;
                section.WorkstreamStatus = posted.WorkstreamStatus;
                section.Body = body;
            }

            var order = 1;

            foreach (var section in report.Sections
                .Where(s => _context.Entry(s).State != EntityState.Deleted)
                .OrderBy(s => s.Order == 0 ? int.MaxValue : s.Order))
            {
                section.Order = order++;
            }
        }

        private static bool HasFixedHeadings(WeeklyReport report) =>
            report.EmployeeId == null ||
            ItDepartment.Matches(report.Employee?.Department?.Name);

        private static bool Filled(WeeklyReportSection section) =>
            !string.IsNullOrWhiteSpace(section.Body) ||
            !string.IsNullOrWhiteSpace(section.Workstream);

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null || !await CanReadAsync(report))
                return NotFound();

            ViewBag.Owns = await OwnsAsync(report);

            return View(report);
        }

        public async Task<IActionResult> Pdf(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null || !await CanReadAsync(report))
                return NotFound();

            var bytes = _pdf.Render(report);

            var who = report.Employee == null
                ? "IT"
                : $"{report.Employee.FirstName}_{report.Employee.LastName}";

            var name =
                $"Weekly_Report_{who}_{report.PeriodEnd:dd-MM-yyyy}" +
                (report.Status == WeeklyReportStatus.Draft ? "_DRAFT" : "") +
                ".pdf";

            return File(bytes, "application/pdf", name);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            var report = await LoadFullAsync(id);

            if (report == null || !await OwnsAsync(report))
                return NotFound();

            return await SubmitAsync(report);
        }

        /// <summary>
        /// Hands the report over: from here the supervisor and the MD
        /// can read it, and the supervisor is told it is in.
        /// </summary>
        private async Task<IActionResult> SubmitAsync(WeeklyReport report)
        {
            if (string.IsNullOrWhiteSpace(report.ExecutiveOverview) &&
                !report.Sections.Any(Filled))
            {
                TempData["Error"] =
                    "Write a summary of the week, or at least one piece of " +
                    "work, before submitting.";

                return RedirectToAction(nameof(Edit), new { id = report.Id });
            }

            report.Status = WeeklyReportStatus.Final;
            report.FinalisedOn = DateTime.Today;

            if (report.Employee?.SupervisorId is int supervisor)
            {
                _notifications.Notify(
                    supervisor,
                    "Weekly report submitted",
                    $"{report.Employee.FirstName} {report.Employee.LastName} " +
                    "submitted their report for the week of " +
                    $"{report.PeriodStart:d MMM yyyy}.",
                    $"/WeeklyReport/Details/{report.Id}");
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} submitted.");

            TempData["Success"] = report.EmployeeId == null
                ? "Report issued."
                : "Submitted. Your supervisor and the Managing Director can " +
                  "now read it.";

            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id)
        {
            var report = await _context.WeeklyReports.FindAsync(id);

            if (report == null || !await OwnsAsync(report))
                return NotFound();

            report.Status = WeeklyReportStatus.Draft;
            report.FinalisedOn = null;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} " +
                "taken back to change.");

            TempData["Success"] =
                "Taken back as a draft. Submit it again once it is right.";

            return RedirectToAction(nameof(Edit), new { id });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null ||
                report.Status == WeeklyReportStatus.Final ||
                !await OwnsAsync(report))
            {
                return NotFound();
            }

            return View(report);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var report = await _context.WeeklyReports.FindAsync(id);

            if (report == null ||
                report.Status == WeeklyReportStatus.Final ||
                !await OwnsAsync(report))
            {
                return NotFound();
            }

            _context.WeeklyReports.Remove(report);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} deleted.");

            TempData["Success"] = "Draft deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<WeeklyReport?> LoadFullAsync(int id) =>
            await _context.WeeklyReports
                .Include(r => r.Sections)
                .Include(r => r.Priorities)
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(r => r.Id == id);

        private static WeeklyReportEditViewModel ToEditModel(
            WeeklyReport report,
            WeeklyReport? previous) =>
            new()
            {
                Id = report.Id,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                OwnerName = report.Employee == null
                    ? report.PreparedBy
                    : $"{report.Employee.FirstName} {report.Employee.LastName}",
                OwnerDepartment = report.Employee?.Department?.Name,
                ExecutiveOverview = report.ExecutiveOverview,
                Status = report.Status,
                FixedHeadings = HasFixedHeadings(report),

                Sections = report.Sections
                    .OrderBy(s => s.Order)
                    .Select(s => new WeeklyReportSectionEditViewModel
                    {
                        Id = s.Id,
                        Order = s.Order,
                        Heading = s.Heading,
                        Workstream = s.Workstream,
                        WorkstreamStatus = s.WorkstreamStatus,
                        Body = s.Body,
                        PreviousBody = previous?.Sections
                            .FirstOrDefault(p => p.Heading == s.Heading)?.Body
                    })
                    .ToList(),

                PrioritiesText = string.Join(
                    "\n",
                    report.Priorities
                        .OrderBy(p => p.Order)
                        .Select(p => p.Text))
            };
    }
}
