using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOnly)]
    public class ApprovalConfigurationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ApprovalConfigurationController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var configurations = await _context.ApprovalConfigurations
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .OrderBy(a => a.Role)
                .ThenByDescending(a => a.IsActive)
                .ToListAsync();

            return View(configurations);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var configuration = await _context.ApprovalConfigurations
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (configuration == null)
                return NotFound();

            return View(configuration);
        }

        public async Task<IActionResult> Create()
        {
            await LoadEmployees();

            return View(new ApprovalConfigurationCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ApprovalConfigurationCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadEmployees();
                return View(model);
            }

            var roleAlreadyFilled = await _context.ApprovalConfigurations
                .AnyAsync(a =>
                    a.Role == model.Role &&
                    a.IsActive);

            if (roleAlreadyFilled)
            {
                ModelState.AddModelError(
                    "Role",
                    "An active approver is already configured for " +
                    "this role. Deactivate the existing one first.");

                await LoadEmployees();
                return View(model);
            }

            var configuration = new ApprovalConfiguration
            {
                Role = model.Role,
                EmployeeId = model.EmployeeId,
                IsActive = true
            };

            _context.ApprovalConfigurations.Add(configuration);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Approval configuration created successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var configuration = await _context.ApprovalConfigurations
                .FindAsync(id);

            if (configuration == null)
                return NotFound();

            var model = new ApprovalConfigurationEditViewModel
            {
                Id = configuration.Id,
                Role = configuration.Role,
                EmployeeId = configuration.EmployeeId,
                IsActive = configuration.IsActive
            };

            await LoadEmployees();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ApprovalConfigurationEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadEmployees();
                return View(model);
            }

            var configuration = await _context.ApprovalConfigurations
                .FindAsync(id);

            if (configuration == null)
                return NotFound();

            if (model.IsActive)
            {
                var roleAlreadyFilled =
                    await _context.ApprovalConfigurations
                        .AnyAsync(a =>
                            a.Id != id &&
                            a.Role == model.Role &&
                            a.IsActive);

                if (roleAlreadyFilled)
                {
                    ModelState.AddModelError(
                        "Role",
                        "Another active approver is already " +
                        "configured for this role.");

                    await LoadEmployees();
                    return View(model);
                }
            }

            configuration.Role = model.Role;
            configuration.EmployeeId = model.EmployeeId;
            configuration.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Approval configuration updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var configuration = await _context.ApprovalConfigurations
                .FindAsync(id);

            if (configuration == null)
                return NotFound();

            if (!configuration.IsActive)
            {
                var roleAlreadyFilled =
                    await _context.ApprovalConfigurations
                        .AnyAsync(a =>
                            a.Id != id &&
                            a.Role == configuration.Role &&
                            a.IsActive);

                if (roleAlreadyFilled)
                {
                    TempData["Error"] =
                        "This approver cannot be activated because " +
                        "another approver is already active for the " +
                        "same role. Deactivate that one first.";

                    return RedirectToAction(nameof(Index));
                }
            }

            configuration.IsActive = !configuration.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                configuration.IsActive
                    ? "Approval configuration activated successfully."
                    : "Approval configuration deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadEmployees()
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

            ViewBag.Employees =
                new SelectList(
                    employees,
                    "Id",
                    "FullName");
        }
    }
}
