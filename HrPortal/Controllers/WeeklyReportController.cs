using HrPortal.Data;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class WeeklyReportController : Controller
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;
        private readonly WeeklyReportBuilder _builder;
        private readonly WeeklyReportPdfService _pdf;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;

        public WeeklyReportController(
            ApplicationDbContext context,
            WeeklyReportBuilder builder,
            WeeklyReportPdfService pdf,
            AuditService audit,
            CurrentUserService currentUser)
        {
            _context = context;
            _builder = builder;
            _pdf = pdf;
            _audit = audit;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var reports = await _context.WeeklyReports
                .AsNoTracking()
                .Include(r => r.Sections)
                .Include(r => r.Priorities)
                .OrderByDescending(r => r.PeriodStart)
                .ToPagedResultAsync(page, PageSize);

            ViewBag.Drafts = await _context.WeeklyReports
                .CountAsync(r => r.Status == WeeklyReportStatus.Draft);

            ViewBag.Issued = await _context.WeeklyReports
                .CountAsync(r => r.Status == WeeklyReportStatus.Final);

            var lastPeriod = await _context.WeeklyReports
                .OrderByDescending(r => r.PeriodStart)
                .Select(r => (DateTime?)r.PeriodStart)
                .FirstOrDefaultAsync();

            ViewBag.NextPeriodStart = WeeklyReportBuilder.NextPeriodStart(lastPeriod);

            ViewBag.UnloggedDays = await _builder.UnloggedDaysAsync(
                (DateTime)ViewBag.NextPeriodStart);

            return View(reports);
        }

        public async Task<IActionResult> Create()
        {
            var lastPeriod = await _context.WeeklyReports
                .OrderByDescending(r => r.PeriodStart)
                .Select(r => (DateTime?)r.PeriodStart)
                .FirstOrDefaultAsync();

            var start = WeeklyReportBuilder.NextPeriodStart(lastPeriod);

            return View(new WeeklyReportCreateViewModel
            {
                PeriodStart = start,
                PeriodEnd = start.AddDays(4),
                PreparedBy = await _currentUser.GetDisplayNameAsync() ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WeeklyReportCreateViewModel model)
        {
            if (model.PeriodEnd < model.PeriodStart)
            {
                ModelState.AddModelError(
                    nameof(model.PeriodEnd),
                    "The period cannot end before it starts.");
            }

            var clash = await _context.WeeklyReports
                .AnyAsync(r => r.PeriodStart == model.PeriodStart);

            if (clash)
            {
                ModelState.AddModelError(
                    nameof(model.PeriodStart),
                    "A report already exists for that period.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var report = await _builder.BuildDraftAsync(
                model.PeriodStart,
                model.PeriodEnd,
                model.PreparedBy.Trim());

            _context.WeeklyReports.Add(report);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report drafted for " +
                $"{report.PeriodStart:dd MMM} to {report.PeriodEnd:dd MMM yyyy}.");

            TempData["Success"] =
                "Draft created from the activity log. " +
                "Write the overview and fill in the week's specifics.";

            return RedirectToAction(nameof(Edit), new { id = report.Id });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await _context.WeeklyReports
                .Include(r => r.Sections)
                .Include(r => r.Priorities)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            var previous = await _context.WeeklyReports
                .Include(r => r.Sections)
                .Where(r => r.PeriodStart < report.PeriodStart)
                .OrderByDescending(r => r.PeriodStart)
                .FirstOrDefaultAsync();

            ViewBag.PreviousPeriod = previous == null
                ? null
                : $"{previous.PeriodStart:dd MMM} to " +
                  $"{previous.PeriodEnd:dd MMM yyyy}";

            ViewBag.ActivityCount = await _context.ItActivities
                .CountAsync(a =>
                    a.LoggedOn >= report.PeriodStart &&
                    a.LoggedOn <= report.PeriodEnd);

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

            var report = await _context.WeeklyReports
                .Include(r => r.Sections)
                .Include(r => r.Priorities)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ActivityCount = await _context.ItActivities
                    .CountAsync(a =>
                        a.LoggedOn >= report.PeriodStart &&
                        a.LoggedOn <= report.PeriodEnd);

                return View(model);
            }

            report.PreparedBy = model.PreparedBy.Trim();
            report.ExecutiveOverview = model.ExecutiveOverview?.Trim();

            foreach (var posted in model.Sections)
            {
                var section = report.Sections
                    .FirstOrDefault(s => s.Id == posted.Id);

                if (section == null)
                    continue;

                section.Heading = posted.Heading.Trim();
                section.Workstream = posted.Workstream?.Trim();
                section.WorkstreamStatus = posted.WorkstreamStatus;
                section.Body = WeeklyReportBuilder.NormaliseBody(posted.Body);
            }

            await _builder.ReplacePrioritiesAsync(report, model.PrioritiesText);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} edited.");

            TempData["Success"] = "Report saved.";

            if (Request.Form["thenPreview"] == "true")
                return RedirectToAction(nameof(Details), new { id = report.Id });

            return RedirectToAction(nameof(Edit), new { id = report.Id });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null)
                return NotFound();

            return View(report);
        }

        public async Task<IActionResult> Pdf(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null)
                return NotFound();

            var bytes = _pdf.Render(report);

            var name =
                $"IT_Weekly_Report_{report.PeriodEnd:dd-MM-yyyy}" +
                (report.Status == WeeklyReportStatus.Draft ? "_DRAFT" : "") +
                ".pdf";

            return File(bytes, "application/pdf", name);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finalise(int id)
        {
            var report = await _context.WeeklyReports.FindAsync(id);

            if (report == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(report.ExecutiveOverview))
            {
                TempData["Error"] =
                    "Write the executive overview before issuing the report.";

                return RedirectToAction(nameof(Edit), new { id });
            }

            report.Status = WeeklyReportStatus.Final;
            report.FinalisedOn = DateTime.Today;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} issued.");

            TempData["Success"] = "Report issued.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id)
        {
            var report = await _context.WeeklyReports.FindAsync(id);

            if (report == null)
                return NotFound();

            report.Status = WeeklyReportStatus.Draft;
            report.FinalisedOn = null;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} " +
                "reopened for editing.");

            TempData["Success"] = "Report reopened.";

            return RedirectToAction(nameof(Edit), new { id });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var report = await LoadFullAsync(id.Value);

            if (report == null)
                return NotFound();

            return View(report);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var report = await _context.WeeklyReports.FindAsync(id);

            if (report == null)
                return NotFound();

            _context.WeeklyReports.Remove(report);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(WeeklyReport),
                report.Id.ToString(),
                $"Weekly report for {report.PeriodStart:dd MMM yyyy} deleted.");

            TempData["Success"] = "Report deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<WeeklyReport?> LoadFullAsync(int id) =>
            await _context.WeeklyReports
                .Include(r => r.Sections)
                .Include(r => r.Priorities)
                .FirstOrDefaultAsync(r => r.Id == id);

        private static WeeklyReportEditViewModel ToEditModel(
            WeeklyReport report,
            WeeklyReport? previous) =>
            new()
            {
                Id = report.Id,
                PeriodStart = report.PeriodStart,
                PeriodEnd = report.PeriodEnd,
                PreparedBy = report.PreparedBy,
                ExecutiveOverview = report.ExecutiveOverview,
                Status = report.Status,

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
                            .FirstOrDefault(p => p.Order == s.Order)?.Body
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
