using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecordDocumentPdfService _pdf;
        private readonly AuditService _audit;
        private readonly CompanyService _companies;

        public DepartmentController(
            ApplicationDbContext context,
            RecordDocumentPdfService pdf,
            AuditService audit,
            CompanyService companies)
        {
            _context = context;
            _pdf = pdf;
            _audit = audit;
            _companies = companies;
        }

        private async Task LoadCompaniesAsync()
        {
            ViewBag.Companies = (await _companies.AllAsync())
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                    CompanyService.NameOf(c),
                    c.Id.ToString()))
                .ToList();
        }

        /// <summary>
        /// Whether the company chosen is a real one, or empty for the
        /// whole group, and the name is not already used there.
        /// </summary>
        private async Task CheckAsync(string name, int? companyId, int? existingId)
        {
            if (companyId != null &&
                !(await _companies.AllAsync()).Any(c => c.Id == companyId))
            {
                ModelState.AddModelError("CompanyId", "Choose a company from the list.");
            }

            var lowered = name.ToLower();

            if (await _context.Departments.AnyAsync(d =>
                    d.Id != existingId &&
                    d.CompanyId == companyId &&
                    d.Name.ToLower() == lowered))
            {
                ModelState.AddModelError(
                    "Name",
                    companyId == null
                        ? "A department for the whole group already uses this name."
                        : "That company already has a department with this name.");
            }
        }

        /// <summary>
        /// The department and everyone currently in it, as a
        /// printable record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.DepartmentsHere
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Company)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            var active = department.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToList();

            var record = new RecordDocument("Department Record")
            {
                Headline = department.Name,
                Chip = department.IsActive ? "Active" : "Inactive"
            };

            record.Section("Department", "How the department is set up")
                .Add("Name", department.Name)
                .Add("Active", department.IsActive)
                .Add("People on strength", active.Count)
                .Add("People on record", department.Employees.Count);

            var people = record.Section(
                "People",
                "Everyone currently on strength");

            foreach (var employee in active)
            {
                people.Add(
                    $"{employee.FirstName} {employee.LastName}",
                    employee.JobTitle);
            }

            if (active.Count == 0)
                people.Add("Nobody", "This department has no active staff");

            record.Note(department.Description);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index()
        {
            var departments = await _context.DepartmentsHere
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Company)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(departments);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.DepartmentsHere
                .AsNoTracking()
                .Include(d => d.Employees)
                .Include(d => d.Company)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            return View(department);
        }

        public async Task<IActionResult> Create()
        {
            await LoadCompaniesAsync();

            return View(new DepartmentCreateViewModel
            {
                CompanyId = await _companies.CurrentIdAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            DepartmentCreateViewModel model)
        {
            var name = model.Name?.Trim() ?? string.Empty;

            if (ModelState.IsValid)
                await CheckAsync(name, model.CompanyId, null);

            if (!ModelState.IsValid)
            {
                await LoadCompaniesAsync();
                return View(model);
            }

            var department = new Department
            {
                Name = name,
                Description = model.Description?.Trim(),
                CompanyId = model.CompanyId,
                IsActive = true
            };

            _context.Departments.Add(department);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Department created successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.DepartmentsHere
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            var model = new DepartmentEditViewModel
            {
                Id = department.Id,
                Name = department.Name,
                Description = department.Description,
                CompanyId = department.CompanyId,
                IsActive = department.IsActive
            };

            await LoadCompaniesAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            DepartmentEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var department = await _context.DepartmentsHere
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            var name = model.Name?.Trim() ?? string.Empty;

            if (ModelState.IsValid)
                await CheckAsync(name, model.CompanyId, id);

            if (!ModelState.IsValid)
            {
                await LoadCompaniesAsync();
                return View(model);
            }

            department.Name = name;
            department.Description = model.Description?.Trim();
            department.CompanyId = model.CompanyId;
            department.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Department updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Removes a department nobody is in. One with people in it,
        /// including people who have left, is refused: their records
        /// still name it, so they have to be moved first.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _context.DepartmentsHere
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            if (department.Employees.Count > 0)
            {
                TempData["Error"] =
                    $"{department.Name} cannot be deleted because " +
                    $"{department.Employees.Count} employee(s) are still " +
                    "in it, counting anyone who has left. Move them to " +
                    "another department first.";

                return RedirectToAction(nameof(Index));
            }

            _context.Departments.Remove(department);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Department),
                department.Id.ToString(),
                $"{department.Name} deleted.");

            TempData["Success"] = $"{department.Name} deleted.";

            return RedirectToAction(nameof(Index));
        }
    }
}
