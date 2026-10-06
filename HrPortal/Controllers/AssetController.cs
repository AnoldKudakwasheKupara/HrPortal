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
    public class AssetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public AssetController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            int? employeeId,
            AssetCondition? condition,
            bool? inUse,
            string? company)
        {
            var query = _context.Assets
                .Include(a => a.AssignedToEmployee)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(company))
                query = query.Where(a => a.Company == company);

            if (employeeId.HasValue)
            {
                query = query.Where(a =>
                    a.AssignedToEmployeeId == employeeId.Value);
            }

            if (condition.HasValue)
                query = query.Where(a => a.Condition == condition.Value);

            if (inUse.HasValue)
                query = query.Where(a => a.IsInUse == inUse.Value);

            var assets = await query
                .OrderBy(a => a.AssignedToEmployee == null)
                .ThenBy(a => a.AssignedToEmployee!.FirstName)
                .ThenBy(a => a.DeviceModel)
                .ToListAsync();

            await LoadLookups();

            ViewBag.EmployeeFilter = employeeId;
            ViewBag.ConditionFilter = condition;
            ViewBag.InUseFilter = inUse;
            ViewBag.CompanyFilter = company;

            return View(assets);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var asset = await _context.Assets
                .Include(a => a.AssignedToEmployee)
                    .ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
                return NotFound();

            return View(asset);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

            return View(new AssetCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AssetCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var asset = new Asset
            {
                DeviceModel = model.DeviceModel.Trim(),
                Processor = model.Processor?.Trim(),
                SerialNumber = model.SerialNumber?.Trim(),
                ProcuredOn = model.ProcuredOn,
                Company = model.Company?.Trim(),
                Condition = model.Condition,
                Notes = model.Notes?.Trim(),
                IsInUse = model.IsInUse,
                AssignedToEmployeeId = model.AssignedToEmployeeId,
                AssignedOn = model.AssignedToEmployeeId == null
                    ? null
                    : DateTime.UtcNow
            };

            _context.Assets.Add(asset);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Asset),
                asset.Id.ToString(),
                $"{asset.DeviceModel} added to the asset register.");

            TempData["Success"] = "Asset added successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var asset = await _context.Assets.FindAsync(id);

            if (asset == null)
                return NotFound();

            await LoadLookups();

            return View(new AssetEditViewModel
            {
                Id = asset.Id,
                DeviceModel = asset.DeviceModel,
                Processor = asset.Processor,
                SerialNumber = asset.SerialNumber,
                ProcuredOn = asset.ProcuredOn,
                Company = asset.Company,
                Condition = asset.Condition,
                Notes = asset.Notes,
                IsInUse = asset.IsInUse,
                AssignedToEmployeeId = asset.AssignedToEmployeeId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            AssetEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var asset = await _context.Assets.FindAsync(id);

            if (asset == null)
                return NotFound();

            // Only stamp the assignment date when the holder changes,
            // so an edit to the notes does not rewrite the history.
            if (asset.AssignedToEmployeeId != model.AssignedToEmployeeId)
            {
                asset.AssignedOn = model.AssignedToEmployeeId == null
                    ? null
                    : DateTime.UtcNow;
            }

            asset.DeviceModel = model.DeviceModel.Trim();
            asset.Processor = model.Processor?.Trim();
            asset.SerialNumber = model.SerialNumber?.Trim();
            asset.ProcuredOn = model.ProcuredOn;
            asset.Company = model.Company?.Trim();
            asset.Condition = model.Condition;
            asset.Notes = model.Notes?.Trim();
            asset.IsInUse = model.IsInUse;
            asset.AssignedToEmployeeId = model.AssignedToEmployeeId;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Asset),
                asset.Id.ToString(),
                $"{asset.DeviceModel} updated.");

            TempData["Success"] = "Asset updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var asset = await _context.Assets
                .Include(a => a.AssignedToEmployee)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
                return NotFound();

            return View(asset);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var asset = await _context.Assets.FindAsync(id);

            if (asset == null)
                return NotFound();

            _context.Assets.Remove(asset);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Asset),
                asset.Id.ToString(),
                $"{asset.DeviceModel} removed from the asset register.");

            TempData["Success"] = "Asset deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadLookups()
        {
            var employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new
                {
                    e.Id,
                    FullName = e.FirstName + " " + e.LastName
                })
                .ToListAsync();

            ViewBag.Employees = new SelectList(
                employees,
                "Id",
                "FullName");

            ViewBag.Companies = await _context.Assets
                .Where(a => a.Company != null && a.Company != "")
                .Select(a => a.Company!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
