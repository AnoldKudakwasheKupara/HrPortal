using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyTimesheetController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly TimesheetEntryValidator _validator;
        private readonly TimesheetAccessService _access;
        private readonly AuditService _audit;
        private readonly IAuthorizationService _authorization;

        public MyTimesheetController(
            ApplicationDbContext context,
            TimesheetEntryValidator validator,
            TimesheetAccessService access,
            AuditService audit,
            IAuthorizationService authorization)
        {
            _context = context;
            _validator = validator;
            _access = access;
            _audit = audit;
            _authorization = authorization;
        }

        public async Task<IActionResult> Index(DateTime? week)
        {
            var (access, blocked) = await GateAsync();

            if (blocked != null)
                return blocked;

            var employeeId = access.EmployeeId!.Value;
            var today = DateTime.Today;

            var weekStart = WeekStart(week ?? today);
            var weekEnd = weekStart.AddDays(6);

            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var entries = await _context.TimesheetEntries
                .AsNoTracking()
                .Include(t => t.Asset)
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    t.WorkDate >= weekStart &&
                    t.WorkDate <= weekEnd)
                .OrderBy(t => t.WorkDate)
                .ThenBy(t => t.StartTime)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var daily = Enumerable.Range(0, 7)
                .ToDictionary(
                    i => weekStart.AddDays(i),
                    i => entries
                        .Where(t => t.WorkDate.Date == weekStart.AddDays(i))
                        .Sum(t => t.DurationMinutes));

            var month = await _context.TimesheetEntries
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    t.WorkDate >= monthStart &&
                    t.WorkDate <= monthEnd)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Minutes = g.Sum(t => t.DurationMinutes),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            return View(new MyTimesheetIndexViewModel
            {
                WeekStart = weekStart,
                IsCurrentWeek = weekStart == WeekStart(today),
                DepartmentName = access.DepartmentName ?? string.Empty,
                Entries = entries,
                DailyMinutes = daily,
                MinutesThisWeek = entries.Sum(t => t.DurationMinutes),
                MinutesThisMonth = month?.Minutes ?? 0,
                EntriesThisMonth = month?.Count ?? 0
            });
        }

        public async Task<IActionResult> Create(DateTime? date)
        {
            var (_, blocked) = await GateAsync();

            if (blocked != null)
                return blocked;

            await LoadAssets();

            var workDate = date?.Date ?? DateTime.Today;

            if (workDate > DateTime.Today)
                workDate = DateTime.Today;

            return View(new TimesheetEntryViewModel
            {
                WorkDate = workDate
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TimesheetEntryViewModel model)
        {
            var (access, blocked) = await GateAsync();

            if (blocked != null)
                return blocked;

            var employeeId = access.EmployeeId!.Value;

            var validation = await _validator.ValidateAsync(
                employeeId,
                model.WorkDate,
                model.AssetId,
                model.StartTime,
                model.EndTime,
                model.Hours,
                null,
                !ModelState.IsValid);

            var minutes = validation.Minutes;

            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            if (!ModelState.IsValid)
            {
                await LoadAssets();
                return View(model);
            }

            var entry = new TimesheetEntry
            {
                EmployeeId = employeeId,
                WorkDate = model.WorkDate.Date,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                DurationMinutes = minutes,
                Category = model.Category,
                Title = model.Title.Trim(),
                Details = Clean(model.Details),
                Reference = Clean(model.Reference),
                AssetId = model.AssetId,
                CreatedAt = DateTime.UtcNow
            };

            _context.TimesheetEntries.Add(entry);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(TimesheetEntry),
                entry.Id.ToString(),
                $"Logged {TimesheetEntry.FormatDuration(minutes)} of " +
                $"{entry.Category} on {entry.WorkDate:dd MMM yyyy}: " +
                entry.Title);

            TempData["Success"] =
                $"Logged {TimesheetEntry.FormatDuration(minutes)} " +
                $"for {entry.WorkDate:ddd d MMM}.";

            return RedirectToWeek(entry.WorkDate);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var (_, blocked) = await GateAsync();

            if (blocked != null)
                return blocked;

            var entry = await LoadAuthorizedEntry(id.Value, Operations.Manage);

            if (entry == null)
                return NotFound();

            await LoadAssets();

            return View(new TimesheetEntryViewModel
            {
                Id = entry.Id,
                WorkDate = entry.WorkDate,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                Hours = Math.Round(entry.Hours, 2),
                Category = entry.Category,
                Title = entry.Title,
                Details = entry.Details,
                Reference = entry.Reference,
                AssetId = entry.AssetId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TimesheetEntryViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var (access, blocked) = await GateAsync();

            if (blocked != null)
                return blocked;

            var entry = await LoadAuthorizedEntry(id, Operations.Manage);

            if (entry == null)
                return NotFound();

            var validation = await _validator.ValidateAsync(
                access.EmployeeId!.Value,
                model.WorkDate,
                model.AssetId,
                model.StartTime,
                model.EndTime,
                model.Hours,
                id,
                !ModelState.IsValid);

            var minutes = validation.Minutes;

            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            if (!ModelState.IsValid)
            {
                await LoadAssets();
                return View(model);
            }

            entry.WorkDate = model.WorkDate.Date;
            entry.StartTime = model.StartTime;
            entry.EndTime = model.EndTime;
            entry.DurationMinutes = minutes;
            entry.Category = model.Category;
            entry.Title = model.Title.Trim();
            entry.Details = Clean(model.Details);
            entry.Reference = Clean(model.Reference);
            entry.AssetId = model.AssetId;
            entry.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(TimesheetEntry),
                entry.Id.ToString(),
                $"Timesheet entry for {entry.WorkDate:dd MMM yyyy} " +
                $"updated ({TimesheetEntry.FormatDuration(minutes)}).");

            TempData["Success"] = "Entry updated.";

            return RedirectToWeek(entry.WorkDate);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (_, blocked) = await GateAsync();

            if (blocked != null)
                return blocked;

            var entry = await LoadAuthorizedEntry(id, Operations.Manage);

            if (entry == null)
                return NotFound();

            _context.TimesheetEntries.Remove(entry);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(TimesheetEntry),
                entry.Id.ToString(),
                $"Timesheet entry for {entry.WorkDate:dd MMM yyyy} " +
                $"removed: {entry.Title}");

            TempData["Success"] = "Entry removed.";

            return RedirectToWeek(entry.WorkDate);
        }

        private async Task<(TimesheetAccess Access, IActionResult? Blocked)>
            GateAsync()
        {
            var access = await _access.GetAsync();

            if (access.EmployeeId == null)
                return (access, View("NoEmployeeRecord"));

            if (!access.Enabled)
                return (access, View("NotEnabled", access.DepartmentName));

            return (access, null);
        }

        private async Task<TimesheetEntry?> LoadAuthorizedEntry(
            int id,
            OperationAuthorizationRequirement operation)
        {
            var entry = await _context.TimesheetEntries
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entry == null)
                return null;

            var result = await _authorization.AuthorizeAsync(
                User,
                entry,
                operation);

            return result.Succeeded ? entry : null;
        }

        private IActionResult RedirectToWeek(DateTime date)
        {
            return RedirectToAction(
                nameof(Index),
                new { week = WeekStart(date).ToString("yyyy-MM-dd") });
        }

        private async Task LoadAssets()
        {
            var assets = await _context.Assets
                .AsNoTracking()
                .Include(a => a.AssignedToEmployee)
                .OrderBy(a => a.DeviceModel)
                .ThenBy(a => a.SerialNumber)
                .ToListAsync();

            ViewBag.Assets = assets
                .Select(a =>
                {
                    var label = a.DeviceModel;

                    if (!string.IsNullOrWhiteSpace(a.SerialNumber))
                        label += " (" + a.SerialNumber + ")";

                    if (a.AssignedToEmployee != null)
                    {
                        label += " - " +
                            a.AssignedToEmployee.FirstName + " " +
                            a.AssignedToEmployee.LastName;
                    }

                    return new SelectListItem(label, a.Id.ToString());
                })
                .ToList();
        }

        public static DateTime WeekStart(DateTime date)
        {
            var offset = ((int)date.DayOfWeek + 6) % 7;

            return date.Date.AddDays(-offset);
        }

        private static string? Clean(string? value)
        {
            var text = value?.Trim();

            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
