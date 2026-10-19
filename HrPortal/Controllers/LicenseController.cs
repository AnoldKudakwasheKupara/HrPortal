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
    public class LicenseController : Controller
    {
        public const int ExpiringWindowDays = ExpiryWindows.LicenceDays;

        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public LicenseController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            LicenseType? type,
            int? vendorId,
            string? show)
        {
            var today = DateTime.Today;
            var soon = today.AddDays(ExpiringWindowDays);

            var query = _context.Licenses
                .AsNoTracking()
                .Include(l => l.Vendor)
                .Include(l => l.AssignedToEmployee)
                .AsQueryable();

            if (type.HasValue)
                query = query.Where(l => l.Type == type.Value);

            if (vendorId.HasValue)
                query = query.Where(l => l.VendorId == vendorId.Value);

            query = show switch
            {
                "expired" => query.Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate < today),

                "expiring" => query.Where(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate >= today &&
                    l.ExpiryDate <= soon),

                "cancelled" => query.Where(l =>
                    l.Status == LicenseStatus.Cancelled),

                _ => query.Where(l => l.Status == LicenseStatus.Active)
            };

            var licenses = await query
                .OrderBy(l => l.ExpiryDate)
                .ToListAsync();

            ViewBag.Expired = await _context.Licenses
                .CountAsync(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate < today);

            ViewBag.Expiring = await _context.Licenses
                .CountAsync(l =>
                    l.Status == LicenseStatus.Active &&
                    l.ExpiryDate >= today &&
                    l.ExpiryDate <= soon);

            ViewBag.Active = await _context.Licenses
                .CountAsync(l => l.Status == LicenseStatus.Active);

            ViewBag.AnnualCost = await _context.Licenses
                .Where(l => l.Status == LicenseStatus.Active)
                .SumAsync(l => (decimal?)l.Cost) ?? 0m;

            await LoadLookups();

            ViewBag.TypeFilter = type;
            ViewBag.VendorFilter = vendorId;
            ViewBag.Show = show;

            return View(licenses);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var license = await _context.Licenses
                .Include(l => l.Vendor)
                .Include(l => l.AssignedToEmployee)
                    .ThenInclude(e => e!.Department)
                .Include(l => l.Renewals)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (license == null)
                return NotFound();

            license.Renewals = license.Renewals
                .OrderByDescending(r => r.RenewedOn)
                .ToList();

            return View(license);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

            return View(new LicenseCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LicenseCreateViewModel model)
        {
            ValidateDates(model.StartDate, model.ExpiryDate);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var license = new License
            {
                Name = model.Name.Trim(),
                Type = model.Type,
                Status = LicenseStatus.Active,
                Reference = model.Reference?.Trim(),
                VendorId = model.VendorId,
                AssignedToEmployeeId = model.AssignedToEmployeeId,
                Seats = model.Seats,
                StartDate = model.StartDate,
                ExpiryDate = model.ExpiryDate,
                Cost = model.Cost,
                AutoRenews = model.AutoRenews,
                Notes = model.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Licenses.Add(license);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(License),
                license.Id.ToString(),
                $"{license.Name} added, expiring " +
                $"{license.ExpiryDate:dd MMM yyyy}.");

            TempData["Success"] = "Licence added.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var license = await _context.Licenses.FindAsync(id);

            if (license == null)
                return NotFound();

            await LoadLookups();

            return View(new LicenseEditViewModel
            {
                Id = license.Id,
                Name = license.Name,
                Type = license.Type,
                Status = license.Status,
                Reference = license.Reference,
                VendorId = license.VendorId,
                AssignedToEmployeeId = license.AssignedToEmployeeId,
                Seats = license.Seats,
                StartDate = license.StartDate,
                ExpiryDate = license.ExpiryDate,
                Cost = license.Cost,
                AutoRenews = license.AutoRenews,
                Notes = license.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            LicenseEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            ValidateDates(model.StartDate, model.ExpiryDate);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var license = await _context.Licenses.FindAsync(id);

            if (license == null)
                return NotFound();

            license.Name = model.Name.Trim();
            license.Type = model.Type;
            license.Status = model.Status;
            license.Reference = model.Reference?.Trim();
            license.VendorId = model.VendorId;
            license.AssignedToEmployeeId = model.AssignedToEmployeeId;
            license.Seats = model.Seats;
            license.StartDate = model.StartDate;
            license.ExpiryDate = model.ExpiryDate;
            license.Cost = model.Cost;
            license.AutoRenews = model.AutoRenews;
            license.Notes = model.Notes?.Trim();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(License),
                license.Id.ToString(),
                $"{license.Name} updated.");

            TempData["Success"] = "Licence updated.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Renew(int? id)
        {
            if (id == null)
                return NotFound();

            var license = await _context.Licenses.FindAsync(id);

            if (license == null)
                return NotFound();

            return View(new LicenseRenewViewModel
            {
                Id = license.Id,
                Name = license.Name,
                CurrentExpiry = license.ExpiryDate,

                NewExpiry = (license.ExpiryDate > DateTime.Today
                        ? license.ExpiryDate
                        : DateTime.Today)
                    .AddYears(1),

                RenewedOn = DateTime.Today,
                Cost = license.Cost
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Renew(
            int id,
            LicenseRenewViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var license = await _context.Licenses.FindAsync(id);

            if (license == null)
                return NotFound();

            if (model.NewExpiry <= license.ExpiryDate)
            {
                ModelState.AddModelError(
                    nameof(model.NewExpiry),
                    "The new expiry date must be after the current one " +
                    $"({license.ExpiryDate:dd MMM yyyy}).");
            }

            if (!ModelState.IsValid)
            {
                model.Name = license.Name;
                model.CurrentExpiry = license.ExpiryDate;

                return View(model);
            }

            _context.LicenseRenewals.Add(new LicenseRenewal
            {
                LicenseId = license.Id,
                RenewedOn = model.RenewedOn,
                PreviousExpiry = license.ExpiryDate,
                NewExpiry = model.NewExpiry,
                Cost = model.Cost,
                Notes = model.Notes?.Trim()
            });

            license.ExpiryDate = model.NewExpiry;

            if (model.Cost.HasValue)
                license.Cost = model.Cost;

            license.Status = LicenseStatus.Active;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(License),
                license.Id.ToString(),
                $"{license.Name} renewed to " +
                $"{model.NewExpiry:dd MMM yyyy}.");

            TempData["Success"] =
                $"{license.Name} renewed to " +
                $"{model.NewExpiry:dd MMM yyyy}.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var license = await _context.Licenses
                .Include(l => l.Vendor)
                .Include(l => l.Renewals)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (license == null)
                return NotFound();

            return View(license);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var license = await _context.Licenses.FindAsync(id);

            if (license == null)
                return NotFound();

            _context.Licenses.Remove(license);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(License),
                license.Id.ToString(),
                $"{license.Name} removed from the licence register.");

            TempData["Success"] = "Licence deleted.";

            return RedirectToAction(nameof(Index));
        }

        private void ValidateDates(DateTime? start, DateTime expiry)
        {
            if (start.HasValue && expiry <= start.Value)
            {
                ModelState.AddModelError(
                    "ExpiryDate",
                    "The expiry date must be after the start date.");
            }
        }

        private async Task LoadLookups()
        {
            ViewBag.Vendors = await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToListAsync();
        }
    }
}
