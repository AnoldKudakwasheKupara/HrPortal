using HrPortal.DbContexts;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Shows whether the daily reminder run is working, and lets it be
    /// run on the spot. A background job nobody can see is a background
    /// job nobody trusts.
    /// </summary>
    [Authorize(Roles = Roles.AdminOnly)]
    public class ReminderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReminderService _reminders;
        private readonly ReminderOptions _options;
        private readonly AuditService _audit;

        public ReminderController(
            ApplicationDbContext context,
            ReminderService reminders,
            IOptions<ReminderOptions> options,
            AuditService audit)
        {
            _context = context;
            _reminders = reminders;
            _options = options.Value;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Options = _options;
            ViewBag.LastRunAt = ReminderHostedService.LastRunAt;
            ViewBag.LastRunCount = ReminderHostedService.LastRunCount;

            ViewBag.Recent = await _context.ReminderLogs
                .AsNoTracking()
                .Include(r => r.Employee)
                .OrderByDescending(r => r.LastSentOn)
                .ThenBy(r => r.Kind)
                .Take(50)
                .ToListAsync();

            ViewBag.TotalSent = await _context.ReminderLogs.CountAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunNow()
        {
            var run = await _reminders.RunAsync();

            ReminderHostedService.Record(run);

            await _audit.LogAsync(
                AuditService.Created,
                "Reminders",
                null,
                $"Reminder run started by hand sent {run.Total} " +
                "reminder(s).");

            TempData["Success"] = run.Total == 0
                ? "Nothing needed a reminder. Anything already sent " +
                  "will not be repeated until the repeat interval has " +
                  "passed."
                : $"{run.Total} reminder(s) sent: " +
                  string.Join(
                      ", ",
                      run.Sent.Select(s => $"{s.Value} {s.Key.ToLower()}"));

            return RedirectToAction(nameof(Index));
        }
    }
}
