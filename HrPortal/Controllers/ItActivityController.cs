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
    [Authorize(Roles = Roles.AdminOnly)]
    public class ItActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly CurrentUserService _currentUser;

        public ItActivityController(
            ApplicationDbContext context,
            AuditService audit,
            CurrentUserService currentUser)
        {
            _context = context;
            _audit = audit;
            _currentUser = currentUser;
        }

        public async Task<IActionResult> Index(
            DateTime? from,
            DateTime? to,
            ItActivityCategory? category,
            ItActivityStatus? status)
        {
            var today = DateTime.Today;

            var start = from ?? today.AddDays(
                -(((int)today.DayOfWeek + 6) % 7));

            var end = to ?? start.AddDays(6);

            var query = _context.ItActivities
                .AsNoTracking()
                .Include(a => a.RaisedByEmployee)
                .Include(a => a.Asset)
                .Where(a => a.LoggedOn >= start && a.LoggedOn <= end);

            if (category.HasValue)
                query = query.Where(a => a.Category == category.Value);

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            var activities = await query
                .OrderByDescending(a => a.LoggedOn)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            ViewBag.From = start;
            ViewBag.To = end;
            ViewBag.CategoryFilter = category;
            ViewBag.StatusFilter = status;

            ViewBag.InPeriod = await _context.ItActivities
                .CountAsync(a => a.LoggedOn >= start && a.LoggedOn <= end);

            ViewBag.Outstanding = await _context.ItActivities
                .CountAsync(a => a.Status != ItActivityStatus.Resolved);

            ViewBag.HardwareInPeriod = await _context.ItActivities
                .CountAsync(a =>
                    a.LoggedOn >= start &&
                    a.LoggedOn <= end &&
                    a.Category == ItActivityCategory.Hardware);

            ViewBag.NotableInPeriod = await _context.ItActivities
                .CountAsync(a =>
                    a.LoggedOn >= start &&
                    a.LoggedOn <= end &&
                    a.IsNotable);

            ViewBag.ByCategory = await _context.ItActivities
                .Where(a => a.LoggedOn >= start && a.LoggedOn <= end)
                .GroupBy(a => a.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToDictionaryAsync(g => g.Category, g => g.Count);

            return View(activities);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

            return View(new ItActivityCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ItActivityCreateViewModel model)
        {
            Validate(model);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var activity = new ItActivity
            {
                LoggedOn = model.LoggedOn,
                Category = model.Category,
                Status = model.Status,
                Summary = model.Summary.Trim(),
                ActionTaken = model.ActionTaken?.Trim(),
                RaisedByEmployeeId = model.RaisedByEmployeeId,
                RaisedByOther = model.RaisedByEmployeeId == null
                    ? model.RaisedByOther?.Trim()
                    : null,
                AssetId = model.AssetId,
                ResolvedOn = model.Status == ItActivityStatus.Resolved
                    ? model.ResolvedOn ?? model.LoggedOn
                    : null,
                IsNotable = model.IsNotable,
                LoggedBy = await _currentUser.GetDisplayNameAsync(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ItActivities.Add(activity);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(ItActivity),
                activity.Id.ToString(),
                $"{activity.Category} activity logged: {activity.Summary}");

            TempData["Success"] = "Activity logged.";

            if (Request.Form["saveAndNew"] == "true")
                return RedirectToAction(nameof(Create));

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var activity = await _context.ItActivities.FindAsync(id);

            if (activity == null)
                return NotFound();

            await LoadLookups();

            return View(new ItActivityEditViewModel
            {
                Id = activity.Id,
                LoggedOn = activity.LoggedOn,
                Category = activity.Category,
                Status = activity.Status,
                Summary = activity.Summary,
                ActionTaken = activity.ActionTaken,
                RaisedByEmployeeId = activity.RaisedByEmployeeId,
                RaisedByOther = activity.RaisedByOther,
                AssetId = activity.AssetId,
                ResolvedOn = activity.ResolvedOn,
                IsNotable = activity.IsNotable,
                LoggedBy = activity.LoggedBy
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ItActivityEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            Validate(model);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var activity = await _context.ItActivities.FindAsync(id);

            if (activity == null)
                return NotFound();

            activity.LoggedOn = model.LoggedOn;
            activity.Category = model.Category;
            activity.Status = model.Status;
            activity.Summary = model.Summary.Trim();
            activity.ActionTaken = model.ActionTaken?.Trim();
            activity.RaisedByEmployeeId = model.RaisedByEmployeeId;

            activity.RaisedByOther = model.RaisedByEmployeeId == null
                ? model.RaisedByOther?.Trim()
                : null;

            activity.AssetId = model.AssetId;

            activity.ResolvedOn = model.Status == ItActivityStatus.Resolved
                ? model.ResolvedOn ?? model.LoggedOn
                : null;

            activity.IsNotable = model.IsNotable;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(ItActivity),
                activity.Id.ToString(),
                $"Activity updated: {activity.Summary}");

            TempData["Success"] = "Activity updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id)
        {
            var activity = await _context.ItActivities.FindAsync(id);

            if (activity == null)
                return NotFound();

            activity.Status = ItActivityStatus.Resolved;
            activity.ResolvedOn = DateTime.Today;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(ItActivity),
                activity.Id.ToString(),
                $"Activity resolved: {activity.Summary}");

            TempData["Success"] = "Activity marked resolved.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var activity = await _context.ItActivities
                .AsNoTracking()
                .Include(a => a.RaisedByEmployee)
                .Include(a => a.Asset)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound();

            return View(activity);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var activity = await _context.ItActivities.FindAsync(id);

            if (activity == null)
                return NotFound();

            _context.ItActivities.Remove(activity);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(ItActivity),
                activity.Id.ToString(),
                $"Activity removed: {activity.Summary}");

            TempData["Success"] = "Activity deleted.";

            return RedirectToAction(nameof(Index));
        }

        private void Validate(ItActivityCreateViewModel model)
        {
            if (model.LoggedOn > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.LoggedOn),
                    "The date cannot be in the future.");
            }

            if (model.Status == ItActivityStatus.Resolved &&
                model.ResolvedOn.HasValue &&
                model.ResolvedOn.Value < model.LoggedOn)
            {
                ModelState.AddModelError(
                    nameof(model.ResolvedOn),
                    "It cannot have been resolved before it came up.");
            }
        }

        private async Task LoadLookups()
        {
            ViewBag.Employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToListAsync();

            ViewBag.Assets = await _context.Assets
                .OrderBy(a => a.DeviceModel)
                .Select(a => new SelectListItem(
                    a.DeviceModel + " (" + a.SerialNumber + ")",
                    a.Id.ToString()))
                .ToListAsync();
        }
    }
}
