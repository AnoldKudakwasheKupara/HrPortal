using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.Services.Notifications;
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
        private readonly RecordDocumentPdfService _pdf;
        private readonly ListDocumentPdfService _lists;
        private readonly NotificationService _notifications;

        public AssetController(
            ApplicationDbContext context,
            AuditService audit,
            RecordDocumentPdfService pdf,
            ListDocumentPdfService lists,
            NotificationService notifications)
        {
            _context = context;
            _audit = audit;
            _pdf = pdf;
            _lists = lists;
            _notifications = notifications;
        }

        /// <summary>
        /// The asset record as a printable document, for a handover
        /// note or an audit file.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var asset = await _context.Assets
                .AsNoTracking()
                .Include(a => a.AssignedToEmployee)
                    .ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
                return NotFound();

            var holder = asset.AssignedToEmployee;

            var record = new RecordDocument(
                "Asset Record",
                asset.SerialNumber)
            {
                Headline = asset.DeviceModel,
                Caption = asset.Company,
                Chip = asset.IsInUse ? "In use" : "Not in use"
            };

            record.Section("Device", "What the asset is and its condition")
                .Add("Device model", asset.DeviceModel)
                .Add("Serial number", asset.SerialNumber)
                .Add("Processor", asset.Processor)
                .Add("Company", asset.Company)
                .Add("Condition", asset.Condition.ToString())
                .Add("In use", asset.IsInUse)
                .Add("Procured on", asset.ProcuredOn);

            record.Section("Assignment", "Who holds it, and since when")
                .Add(
                    "Assigned to",
                    holder == null
                        ? null
                        : $"{holder.FirstName} {holder.LastName}")
                .Add("Department", holder?.Department?.Name)
                .Add("Assigned on", asset.AssignedOn);

            record.Note(asset.Notes);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        /// <summary>
        /// The register, narrowed the way the page is. The list and
        /// the download ask this the same question, so what comes out
        /// of the printer can never be a different set of assets from
        /// the screen it was asked for.
        /// </summary>
        private IQueryable<Asset> Filtered(
            int? employeeId,
            AssetCondition? condition,
            bool? inUse,
            string? company)
        {
            var query = _context.Assets
                .AsNoTracking()
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

            return query;
        }

        public async Task<IActionResult> Index(
            int? employeeId,
            AssetCondition? condition,
            bool? inUse,
            string? company)
        {
            var assets = await Filtered(
                    employeeId, condition, inUse, company)
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

        /// <summary>
        /// The register as a printable document, narrowed the same
        /// way the page is. The filter is named on the sheet: six
        /// assets on a page that does not say it is showing only the
        /// ones in use is a register that reads as the whole estate.
        /// </summary>
        public async Task<IActionResult> List(
            int? employeeId,
            AssetCondition? condition,
            bool? inUse,
            string? company)
        {
            var assets = await Filtered(
                    employeeId, condition, inUse, company)
                .OrderBy(a => a.AssignedToEmployee == null)
                .ThenBy(a => a.AssignedToEmployee!.FirstName)
                .ThenBy(a => a.DeviceModel)
                .ToListAsync();

            var narrowed = new List<string>();

            if (!string.IsNullOrWhiteSpace(company))
                narrowed.Add(company);

            if (employeeId.HasValue)
            {
                var holder = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.Id == employeeId.Value)
                    .Select(e => e.FirstName + " " + e.LastName)
                    .FirstOrDefaultAsync();

                narrowed.Add($"held by {holder ?? "one employee"}");
            }

            if (condition.HasValue)
                narrowed.Add($"{condition.Value} condition");

            if (inUse.HasValue)
                narrowed.Add(inUse.Value ? "in use" : "not in use");

            var total = narrowed.Count == 0
                ? assets.Count
                : await _context.Assets.CountAsync();

            var list = new ListDocument(
                "Asset Register",
                narrowed.Count == 0
                    ? "Every machine on the register."
                    : $"Filtered to {string.Join(", ", narrowed)}. " +
                      $"{assets.Count:N0} of {total:N0} assets.");

            list.Column("Serial", 1.3f)
                .Column("Device / model", 1.8f)
                .Column("Processor", 1.6f)
                .Column("Company", 1.4f)
                .Column("Held by", 1.4f)
                .Column("Procured", 1f)
                .Column("Condition", 1f)
                .Column("In use", 0.8f);

            foreach (var asset in assets)
            {
                var holder = asset.AssignedToEmployee;

                list.Row(
                    ListDocument.Text(asset.SerialNumber),
                    ListDocument.Text(asset.DeviceModel),
                    ListDocument.Text(asset.Processor),
                    ListDocument.Text(asset.Company),
                    holder == null
                        ? null
                        : $"{holder.FirstName} {holder.LastName}",
                    ListDocument.Date(asset.ProcuredOn),
                    asset.Condition.ToString(),
                    ListDocument.Yes(asset.IsInUse));
            }

            list.Total("Assets", assets.Count)
                .Total("In use", assets.Count(a => a.IsInUse))
                .Total(
                    "Unassigned",
                    assets.Count(a => a.AssignedToEmployeeId == null));

            list.EmptyMessage = "No assets match this filter.";

            return File(
                _lists.Render(list),
                "application/pdf",
                list.FileName);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var asset = await _context.Assets
                .AsNoTracking()
                .Include(a => a.AssignedToEmployee)
                    .ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
                return NotFound();

            ViewBag.Maintenance = await _context.AssetMaintenances
                .Include(m => m.Vendor)
                .Where(m => m.AssetId == asset.Id)
                .OrderBy(m => m.Status == MaintenanceStatus.Scheduled ? 0 : 1)
                .ThenByDescending(m => m.ScheduledFor)
                .ToListAsync();

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

            TellHolder(asset);

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

            var reassigned =
                asset.AssignedToEmployeeId != model.AssignedToEmployeeId;

            if (reassigned)
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

            if (reassigned)
                TellHolder(asset);

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
                .AsNoTracking()
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

        /// <summary>
        /// Tells the person an asset has just been handed to that it
        /// is now theirs to look after. Nothing goes when it is taken
        /// back, since there is nothing for them to do.
        /// </summary>
        private void TellHolder(Asset asset)
        {
            if (asset.AssignedToEmployeeId is not int holder)
                return;

            var serial = string.IsNullOrWhiteSpace(asset.SerialNumber)
                ? string.Empty
                : $" (serial number {asset.SerialNumber})";

            _notifications.Notify(
                holder,
                $"{asset.DeviceModel} has been assigned to you",
                $"{asset.DeviceModel}{serial} is now recorded against " +
                "you in the asset register. Please look after it, and " +
                "tell IT if anything about it is wrong or it needs " +
                "attention.",
                "/MyAssets",
                EmailKind.AssetAssigned);
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
