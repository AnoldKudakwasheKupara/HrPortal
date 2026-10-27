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

        public DepartmentController(
            ApplicationDbContext context,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        /// <summary>
        /// The department and everyone currently in it, as a
        /// printable record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
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
            var departments = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(departments);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            return View(department);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            DepartmentCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var name = model.Name.Trim();

            var exists = await _context.Departments
                .AnyAsync(d => d.Name.ToLower() == name.ToLower());

            if (exists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A department with this name already exists.");

                return View(model);
            }

            var department = new Department
            {
                Name = name,
                Description = model.Description?.Trim(),
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

            var department = await _context.Departments
                .FindAsync(id);

            if (department == null)
                return NotFound();

            var model = new DepartmentEditViewModel
            {
                Id = department.Id,
                Name = department.Name,
                Description = department.Description,
                IsActive = department.IsActive
            };

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

            if (!ModelState.IsValid)
                return View(model);

            var department = await _context.Departments
                .FindAsync(id);

            if (department == null)
                return NotFound();

            var name = model.Name.Trim();

            var duplicateExists = await _context.Departments
                .AnyAsync(d =>
                    d.Id != id &&
                    d.Name.ToLower() == name.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "Name",
                    "Another department already uses this name.");

                return View(model);
            }

            department.Name = name;
            department.Description = model.Description?.Trim();
            department.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Department updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            if (department.IsActive)
            {
                var hasActiveEmployees =
                    department.Employees
                        .Any(e => e.IsActive);

                if (hasActiveEmployees)
                {
                    TempData["Error"] =
                        "This department cannot be deactivated " +
                        "because it still has active employees. " +
                        "Move or deactivate the employees first.";

                    return RedirectToAction(nameof(Index));
                }
            }

            department.IsActive = !department.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                department.IsActive
                    ? "Department activated successfully."
                    : "Department deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}
