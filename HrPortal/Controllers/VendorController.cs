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
    [Authorize(Roles = Roles.AdminOnly)]
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly RecordDocumentPdfService _pdf;

        public VendorController(
            ApplicationDbContext context,
            AuditService audit,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _audit = audit;
            _pdf = pdf;
        }

        /// <summary>
        /// The vendor and how to reach them, as a printable record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var vendor = await _context.Vendors
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                return NotFound();

            var record = new RecordDocument("Vendor Record")
            {
                Headline = vendor.Name,
                Caption = vendor.ProductOrService,
                Chip = vendor.IsActive ? "Active" : "Inactive"
            };

            record.Section("Vendor", "What they supply")
                .Add("Name", vendor.Name)
                .Add("Product or service", vendor.ProductOrService)
                .Add("Category", vendor.Category)
                .Add("Active", vendor.IsActive);

            record.Section("Contact", "How to reach them")
                .Add("Contact person", vendor.ContactPerson)
                .Add("Email", vendor.Email)
                .Add("Phone number", vendor.PhoneNumber)
                .Add("Alternate phone", vendor.AlternatePhone)
                .Add("Website", vendor.Website)
                .Add("Address", vendor.Address);

            record.Note(vendor.Notes);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index(
            string? search,
            string? category,
            bool? active)
        {
            var query = _context.Vendors.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();

                query = query.Where(v =>
                    v.Name.ToLower().Contains(term) ||
                    v.ProductOrService.ToLower().Contains(term) ||
                    (v.ContactPerson != null &&
                     v.ContactPerson.ToLower().Contains(term)) ||
                    (v.Email != null && v.Email.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(v => v.Category == category);

            if (active.HasValue)
                query = query.Where(v => v.IsActive == active.Value);

            var vendors = await query
                .OrderBy(v => v.Name)
                .ToListAsync();

            await LoadCategories();

            ViewBag.Search = search;
            ViewBag.CategoryFilter = category;
            ViewBag.ActiveFilter = active;

            return View(vendors);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var vendor = await _context.Vendors
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                return NotFound();

            return View(vendor);
        }

        public async Task<IActionResult> Create()
        {
            await LoadCategories();

            return View(new VendorCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VendorCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategories();
                return View(model);
            }

            var vendor = new Vendor
            {
                Name = model.Name.Trim(),
                ProductOrService = model.ProductOrService.Trim(),
                Category = model.Category?.Trim(),
                ContactPerson = model.ContactPerson?.Trim(),
                Email = model.Email?.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                AlternatePhone = model.AlternatePhone?.Trim(),
                Website = model.Website?.Trim(),
                Address = model.Address?.Trim(),
                Notes = model.Notes?.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vendors.Add(vendor);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Vendor),
                vendor.Id.ToString(),
                $"{vendor.Name} added to the vendor register.");

            TempData["Success"] = "Vendor added successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var vendor = await _context.Vendors.FindAsync(id);

            if (vendor == null)
                return NotFound();

            await LoadCategories();

            return View(new VendorEditViewModel
            {
                Id = vendor.Id,
                Name = vendor.Name,
                ProductOrService = vendor.ProductOrService,
                Category = vendor.Category,
                ContactPerson = vendor.ContactPerson,
                Email = vendor.Email,
                PhoneNumber = vendor.PhoneNumber,
                AlternatePhone = vendor.AlternatePhone,
                Website = vendor.Website,
                Address = vendor.Address,
                Notes = vendor.Notes,
                IsActive = vendor.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            VendorEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadCategories();
                return View(model);
            }

            var vendor = await _context.Vendors.FindAsync(id);

            if (vendor == null)
                return NotFound();

            vendor.Name = model.Name.Trim();
            vendor.ProductOrService = model.ProductOrService.Trim();
            vendor.Category = model.Category?.Trim();
            vendor.ContactPerson = model.ContactPerson?.Trim();
            vendor.Email = model.Email?.Trim();
            vendor.PhoneNumber = model.PhoneNumber?.Trim();
            vendor.AlternatePhone = model.AlternatePhone?.Trim();
            vendor.Website = model.Website?.Trim();
            vendor.Address = model.Address?.Trim();
            vendor.Notes = model.Notes?.Trim();
            vendor.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Vendor),
                vendor.Id.ToString(),
                $"{vendor.Name} updated.");

            TempData["Success"] = "Vendor updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var vendor = await _context.Vendors
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vendor == null)
                return NotFound();

            return View(vendor);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);

            if (vendor == null)
                return NotFound();

            _context.Vendors.Remove(vendor);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Vendor),
                vendor.Id.ToString(),
                $"{vendor.Name} removed from the vendor register.");

            TempData["Success"] = "Vendor deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadCategories()
        {
            ViewBag.Categories = await _context.Vendors
                .Where(v => v.Category != null && v.Category != "")
                .Select(v => v.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}
