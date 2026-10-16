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
    /// <summary>
    /// Qualifications people hold, and when they lapse.
    /// </summary>
    [Authorize(Roles = Roles.HrOnly)]
    public class CertificationController : Controller
    {
        /// <summary>How far ahead counts as expiring soon.</summary>
        public const int ExpiringWindowDays = 60;

        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public CertificationController(
            ApplicationDbContext context,
            AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(int? employeeId, string? show)
        {
            var today = DateTime.Today;
            var soon = today.AddDays(ExpiringWindowDays);

            var query = _context.Certifications
                .AsNoTracking()
                .Include(c => c.Employee)
                .Include(c => c.TrainingCourse)
                .AsQueryable();

            if (employeeId.HasValue)
                query = query.Where(c => c.EmployeeId == employeeId.Value);

            query = show switch
            {
                "expired" => query.Where(c =>
                    c.ExpiryDate != null && c.ExpiryDate < today),

                "expiring" => query.Where(c =>
                    c.ExpiryDate != null &&
                    c.ExpiryDate >= today &&
                    c.ExpiryDate <= soon),

                "current" => query.Where(c =>
                    c.ExpiryDate == null || c.ExpiryDate >= today),

                _ => query
            };

            var certifications = await query
                .OrderBy(c => c.ExpiryDate == null)
                .ThenBy(c => c.ExpiryDate)
                .ThenBy(c => c.Name)
                .ToListAsync();

            ViewBag.Show = show;
            ViewBag.EmployeeFilter = employeeId;

            ViewBag.Total = await _context.Certifications.CountAsync();

            ViewBag.Expired = await _context.Certifications
                .CountAsync(c => c.ExpiryDate != null && c.ExpiryDate < today);

            ViewBag.Expiring = await _context.Certifications
                .CountAsync(c =>
                    c.ExpiryDate != null &&
                    c.ExpiryDate >= today &&
                    c.ExpiryDate <= soon);

            ViewBag.NeverExpires = await _context.Certifications
                .CountAsync(c => c.ExpiryDate == null);

            await LoadLookups();

            return View(certifications);
        }

        public async Task<IActionResult> Create(int? employeeId)
        {
            await LoadLookups();

            return View(new CertificationViewModel
            {
                EmployeeId = employeeId ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CertificationViewModel model)
        {
            Validate(model);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var certification = new Certification
            {
                EmployeeId = model.EmployeeId,
                Name = model.Name.Trim(),
                IssuedBy = model.IssuedBy?.Trim(),
                Reference = model.Reference?.Trim(),
                IssuedOn = model.IssuedOn,
                ExpiryDate = model.ExpiryDate,
                TrainingCourseId = model.TrainingCourseId,
                Notes = model.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Certifications.Add(certification);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Certification),
                certification.Id.ToString(),
                $"{certification.Name} recorded.");

            TempData["Success"] = "Certification recorded.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var certification = await _context.Certifications.FindAsync(id);

            if (certification == null)
                return NotFound();

            await LoadLookups();

            return View(new CertificationViewModel
            {
                Id = certification.Id,
                EmployeeId = certification.EmployeeId,
                Name = certification.Name,
                IssuedBy = certification.IssuedBy,
                Reference = certification.Reference,
                IssuedOn = certification.IssuedOn,
                ExpiryDate = certification.ExpiryDate,
                TrainingCourseId = certification.TrainingCourseId,
                Notes = certification.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            CertificationViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            Validate(model);

            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(model);
            }

            var certification = await _context.Certifications.FindAsync(id);

            if (certification == null)
                return NotFound();

            certification.EmployeeId = model.EmployeeId;
            certification.Name = model.Name.Trim();
            certification.IssuedBy = model.IssuedBy?.Trim();
            certification.Reference = model.Reference?.Trim();
            certification.IssuedOn = model.IssuedOn;
            certification.ExpiryDate = model.ExpiryDate;
            certification.TrainingCourseId = model.TrainingCourseId;
            certification.Notes = model.Notes?.Trim();

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Certification),
                certification.Id.ToString(),
                $"{certification.Name} updated.");

            TempData["Success"] = "Certification updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var certification = await _context.Certifications.FindAsync(id);

            if (certification == null)
                return NotFound();

            _context.Certifications.Remove(certification);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Certification),
                certification.Id.ToString(),
                $"{certification.Name} removed.");

            TempData["Success"] = "Certification removed.";

            return RedirectToAction(nameof(Index));
        }

        private void Validate(CertificationViewModel model)
        {
            if (model.ExpiryDate.HasValue &&
                model.ExpiryDate.Value.Date <= model.IssuedOn.Date)
            {
                ModelState.AddModelError(
                    nameof(model.ExpiryDate),
                    "It cannot expire on or before the day it was issued.");
            }

            if (model.IssuedOn.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(model.IssuedOn),
                    "The issue date cannot be in the future.");
            }
        }

        private async Task LoadLookups()
        {
            ViewBag.Employees = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .Select(e => new SelectListItem(
                    e.FirstName + " " + e.LastName,
                    e.Id.ToString()))
                .ToListAsync();

            ViewBag.Courses = await _context.TrainingCourses
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem(c.Title, c.Id.ToString()))
                .ToListAsync();
        }
    }
}
