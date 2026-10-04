using HrPortal.DbContexts;
using HrPortal.Data;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.AdminOrHr)]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.User)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            return View(employees);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public async Task<IActionResult> Create()
        {
            await LoadDepartments();

            return View(new EmployeeCreateViewModel
            {
                DateJoined = DateTime.Today
            });
        }


        // ============================================================
        // CREATE - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            EmployeeCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDepartments();
                return View(model);
            }

            // Check employee number
            var employeeNumberExists =
                await _context.Employees
                    .AnyAsync(e =>
                        e.EmployeeNumber == model.EmployeeNumber);

            if (employeeNumberExists)
            {
                ModelState.AddModelError(
                    "EmployeeNumber",
                    "This employee number already exists.");

                await LoadDepartments();
                return View(model);
            }

            // Check email
            var emailExists =
                await _context.Employees
                    .AnyAsync(e => e.Email == model.Email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "An employee with this email already exists.");

                await LoadDepartments();
                return View(model);
            }


            // ========================================================
            // CREATE EMPLOYEE
            // ========================================================

            var employee = new Employee
            {
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                NationalId = model.NationalId,
                DateJoined = model.DateJoined,
                DepartmentId = model.DepartmentId,
                IsActive = true
            };

            _context.Employees.Add(employee);

            await _context.SaveChangesAsync();


            // ========================================================
            // CREATE LOGIN ACCOUNT
            // ========================================================

            if (model.CreateLoginAccount)
            {
                if (string.IsNullOrWhiteSpace(
                    model.TemporaryPassword))
                {
                    ModelState.AddModelError(
                        "TemporaryPassword",
                        "A temporary password is required.");

                    _context.Employees.Remove(employee);
                    await _context.SaveChangesAsync();

                    await LoadDepartments();
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName =
                        $"{model.FirstName} {model.LastName}",
                    EmailConfirmed = true
                };

                var result =
                    await _userManager.CreateAsync(
                        user,
                        model.TemporaryPassword);

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(
                            "",
                            error.Description);
                    }

                    _context.Employees.Remove(employee);
                    await _context.SaveChangesAsync();

                    await LoadDepartments();
                    return View(model);
                }

                // Connect User to Employee
                employee.UserId = user.Id;

                _context.Employees.Update(employee);

                await _context.SaveChangesAsync();

                // New employees are regular Employees
                var roleResult = await _userManager.AddToRoleAsync(
                    user,
                    "Employee");

                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(
                            "",
                            error.Description);
                    }

                    await _userManager.DeleteAsync(user);

                    _context.Employees.Remove(employee);
                    await _context.SaveChangesAsync();

                    await LoadDepartments();
                    return View(model);
                }
            }

            TempData["Success"] =
                "Employee created successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // EDIT - GET
        // ============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .FindAsync(id);

            if (employee == null)
                return NotFound();

            var model = new EmployeeEditViewModel
            {
                Id = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                NationalId = employee.NationalId,
                DateJoined = employee.DateJoined,
                DepartmentId = employee.DepartmentId,
                IsActive = employee.IsActive
            };

            await LoadDepartments();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EmployeeEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadDepartments();
                return View(model);
            }

            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();


            employee.EmployeeNumber = model.EmployeeNumber;
            employee.FirstName = model.FirstName;
            employee.LastName = model.LastName;
            employee.Email = model.Email;
            employee.PhoneNumber = model.PhoneNumber;
            employee.NationalId = model.NationalId;
            employee.DateJoined = model.DateJoined;
            employee.DepartmentId = model.DepartmentId;
            employee.IsActive = model.IsActive;


            // Update Identity user email/name
            if (employee.User != null)
            {
                employee.User.Email = model.Email;
                employee.User.UserName = model.Email;
                employee.User.FullName =
                    $"{model.FirstName} {model.LastName}";

                await _userManager.UpdateAsync(employee.User);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Employee updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            employee.IsActive = !employee.IsActive;

            await _context.SaveChangesAsync();

            // EmployeeSignInManager refuses sign-in while the
            // employee is inactive. Changing the security stamp
            // additionally invalidates any cookie already issued,
            // so a user deactivated mid-session is signed out
            // rather than keeping access until it expires.
            if (employee.User != null)
            {
                await _userManager.UpdateSecurityStampAsync(
                    employee.User);
            }

            TempData["Success"] =
                employee.IsActive
                    ? "Employee activated successfully."
                    : "Employee deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDepartments()
        {
            var departments = await _context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.Departments =
                new SelectList(
                    departments,
                    "Id",
                    "Name");
        }
    }
}