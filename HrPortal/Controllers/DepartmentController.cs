using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DepartmentController(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .Include(d => d.Employees)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(departments);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();

            return View(department);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public IActionResult Create()
        {
            return View();
        }


        // ============================================================
        // CREATE - POST
        // ============================================================

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


        // ============================================================
        // EDIT - GET
        // ============================================================

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


        // ============================================================
        // EDIT - POST
        // ============================================================

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


        // ============================================================
        // TOGGLE STATUS
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
                return NotFound();


            // Deactivating
            if (department.IsActive)
            {
                // Don't deactivate a department that still
                // has active employees.
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