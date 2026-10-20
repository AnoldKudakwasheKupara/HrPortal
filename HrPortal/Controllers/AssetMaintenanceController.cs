using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class AssetMaintenanceController : Controller
    {
        private const int PageSize = 50;

        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public AssetMaintenanceController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            int? assetId,
            MaintenanceStatus? status,
            MaintenanceType? type,
            bool overdueOnly = false,
            int page = 1)
        {
            var today = DateTime.Today;

            var query = _context.AssetMaintenances
                .AsNoTracking()
                .Include(m => m.Asset).ThenInclude(a => a.AssignedToEmployee)
                .Include(m => m.Vendor)
                .AsQueryable();

            if (assetId.HasValue)
                query = query.Where(m => m.AssetId == assetId.Value);

            if (status.HasValue)
                query = query.Where(m => m.Status == status.Value);

            if (type.HasValue)
                query = query.Where(m => m.Type == type.Value);

            if (overdueOnly)
            {
                query = query.Where(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor < today);
            }

            var records = await query
                .OrderBy(m => m.Status == MaintenanceStatus.Scheduled ? 0 : 1)
                .ThenBy(m => m.ScheduledFor)
                .ToPagedResultAsync(page, PageSize);

            ViewBag.Overdue = await _context.AssetMaintenances
                .CountAsync(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor < today);

            ViewBag.DueSoon = await _context.AssetMaintenances
                .CountAsync(m =>
                    m.Status == MaintenanceStatus.Scheduled &&
                    m.ScheduledFor >= today &&
                    m.ScheduledFor <= today.AddDays(30));

            ViewBag.Scheduled = await _context.AssetMaintenances
                .CountAsync(m => m.Status == MaintenanceStatus.Scheduled);

            ViewBag.CompletedThisYear = await _context.AssetMaintenances
                .CountAsync(m =>
                    m.Status == MaintenanceStatus.Completed &&
                    m.CompletedOn != null &&
                    m.CompletedOn.Value.Year == today.Year);

            await LoadLookups();

            ViewBag.AssetFilter = assetId;
            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter = type;
            ViewBag.OverdueOnly = overdueOnly;

            return View(records);
        }

        public async Task<IActionResult> Create(int? assetId)
        {
            await LoadLookups();

            int? holder = assetId == null
                ? null
                : await _context.Assets
                    .Where(a => a.Id == assetId.Value)
                    .Select(a => a.AssignedToEmployeeId)
                    .FirstOrDefaultAsync();

            return View(new AssetMaintenanceCreateViewModel
            {
                AssetId = assetId ?? 0,
                EmployeeFilterId = holder,
                ScheduledFor = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AssetMaintenanceCreateViewModel model)
        {
            if (!await _context.Assets.AnyAsync(a => a.Id == model.AssetId))
                ModelState.AddModelError("AssetId", "Choose an asset.");

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var record = new AssetMaintenance
            {
                AssetId = model.AssetId,
                Type = model.Type,
                Status = MaintenanceStatus.Scheduled,
                ScheduledFor = model.ScheduledFor,
                VendorId = model.VendorId,
                PerformedBy = model.PerformedBy?.Trim(),
                Cost = model.Cost,
                Notes = model.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.AssetMaintenances.Add(record);

            await _context.SaveChangesAsync();

            var asset = await _context.Assets.FindAsync(model.AssetId);

            await _audit.LogAsync(
                AuditService.Created,
                nameof(AssetMaintenance),
                record.Id.ToString(),
                $"{model.Type} scheduled for {asset?.DeviceModel} " +
                $"on {model.ScheduledFor:dd MMM yyyy}.");

            TempData["Success"] = "Maintenance scheduled.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var record = await _context.AssetMaintenances.FindAsync(id);

            if (record == null)
                return NotFound();

            await LoadLookups();

            var holder = await _context.Assets
                .Where(a => a.Id == record.AssetId)
                .Select(a => a.AssignedToEmployeeId)
                .FirstOrDefaultAsync();

            return View(new AssetMaintenanceEditViewModel
            {
                Id = record.Id,
                AssetId = record.AssetId,
                EmployeeFilterId = holder,
                Type = record.Type,
                Status = record.Status,
                ScheduledFor = record.ScheduledFor,
                CompletedOn = record.CompletedOn,
                VendorId = record.VendorId,
                PerformedBy = record.PerformedBy,
                Cost = record.Cost,
                Notes = record.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            AssetMaintenanceEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (model.Status == MaintenanceStatus.Completed &&
                model.CompletedOn == null)
            {
                ModelState.AddModelError(
                    nameof(model.CompletedOn),
                    "Give the date the work was completed.");
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var record = await _context.AssetMaintenances.FindAsync(id);

            if (record == null)
                return NotFound();

            record.AssetId = model.AssetId;
            record.Type = model.Type;
            record.Status = model.Status;
            record.ScheduledFor = model.ScheduledFor;
            record.CompletedOn = model.Status == MaintenanceStatus.Completed
                ? model.CompletedOn
                : null;
            record.VendorId = model.VendorId;
            record.PerformedBy = model.PerformedBy?.Trim();
            record.Cost = model.Cost;
            record.Notes = model.Notes?.Trim();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(AssetMaintenance),
                record.Id.ToString(),
                $"Maintenance record updated ({record.Status}).");

            TempData["Success"] = "Maintenance record updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var record = await _context.AssetMaintenances
                .Include(m => m.Asset)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (record == null)
                return NotFound();

            if (record.Status == MaintenanceStatus.Completed)
            {
                TempData["Error"] =
                    "That work is already marked complete.";

                return RedirectToAction(nameof(Index));
            }

            record.Status = MaintenanceStatus.Completed;
            record.CompletedOn = DateTime.Today;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(AssetMaintenance),
                record.Id.ToString(),
                $"{record.Type} on {record.Asset.DeviceModel} " +
                "marked complete.");

            TempData["Success"] = "Marked complete.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var record = await _context.AssetMaintenances
                .AsNoTracking()
                .Include(m => m.Asset)
                .Include(m => m.Vendor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (record == null)
                return NotFound();

            return View(record);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var record = await _context.AssetMaintenances
                .Include(m => m.Asset)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (record == null)
                return NotFound();

            _context.AssetMaintenances.Remove(record);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(AssetMaintenance),
                record.Id.ToString(),
                $"{record.Type} record for {record.Asset.DeviceModel} " +
                "removed.");

            TempData["Success"] = "Maintenance record deleted.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadLookups()
        {
            var assets = await _context.Assets
                .Include(a => a.AssignedToEmployee)
                .OrderBy(a => a.DeviceModel)
                .ToListAsync();

            ViewBag.Assets = assets
                .Select(a => new SelectListItem(
                    a.DeviceModel +
                    (!string.IsNullOrWhiteSpace(a.SerialNumber)
                        ? " (" + a.SerialNumber + ")"
                        : ""),
                    a.Id.ToString()))
                .ToList();

            ViewBag.AssetOptions = assets
                .Select(a => new AssetOptionViewModel
                {
                    Id = a.Id,
                    Label = a.DeviceModel +
                        (!string.IsNullOrWhiteSpace(a.SerialNumber)
                            ? " (" + a.SerialNumber + ")"
                            : ""),
                    EmployeeId = a.AssignedToEmployeeId
                })
                .ToList();

            ViewBag.AssetHolders = assets
                .Where(a => a.AssignedToEmployee != null)
                .Select(a => a.AssignedToEmployee!)
                .DistinctBy(e => e.Id)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToList();

            ViewBag.UnassignedCount =
                assets.Count(a => a.AssignedToEmployeeId == null);

            ViewBag.Vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
                .ToListAsync();
        }
    }
}
