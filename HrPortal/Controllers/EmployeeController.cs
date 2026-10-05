using HrPortal.DbContexts;
using HrPortal.Data;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using HrPortal.Services;
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
        private readonly AuditService _audit;
        private readonly FileStorageService _fileStorage;

        public EmployeeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            FileStorageService fileStorage)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
            _fileStorage = fileStorage;
        }

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

        public async Task<IActionResult> Create()
        {
            await LoadDepartments();

            return View(new EmployeeCreateViewModel
            {
                DateJoined = DateTime.Today
            });
        }

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

                employee.UserId = user.Id;

                _context.Employees.Update(employee);

                await _context.SaveChangesAsync();

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

            await _audit.LogAsync(
                AuditService.Created,
                nameof(Employee),
                employee.EmployeeNumber,
                $"{employee.FirstName} {employee.LastName} added" +
                (model.CreateLoginAccount
                    ? " with a login account."
                    : "."));

            TempData["Success"] =
                "Employee created successfully.";

            return RedirectToAction(nameof(Index));
        }

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

            if (employee.User != null)
            {
                employee.User.Email = model.Email;
                employee.User.UserName = model.Email;
                employee.User.FullName =
                    $"{model.FirstName} {model.LastName}";

                await _userManager.UpdateAsync(employee.User);
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.Updated,
                nameof(Employee),
                employee.EmployeeNumber,
                $"{employee.FirstName} {employee.LastName} updated.");

            TempData["Success"] =
                "Employee updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ResetPassword(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            if (employee.User == null)
            {
                TempData["Error"] =
                    "That employee has no sign-in account, so there " +
                    "is no password to reset.";

                return RedirectToAction(nameof(Index));
            }

            return View(new EmployeeResetPasswordViewModel
            {
                Id = employee.Id,
                FullName =
                    $"{employee.FirstName} {employee.LastName}",
                EmployeeNumber = employee.EmployeeNumber,
                Email = employee.User.Email ?? employee.Email
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            int id,
            EmployeeResetPasswordViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            if (employee.User == null)
            {
                TempData["Error"] =
                    "That employee has no sign-in account, so there " +
                    "is no password to reset.";

                return RedirectToAction(nameof(Index));
            }

            model.FullName =
                $"{employee.FirstName} {employee.LastName}";
            model.EmployeeNumber = employee.EmployeeNumber;
            model.Email = employee.User.Email ?? employee.Email;

            if (!ModelState.IsValid)
                return View(model);

            var token = await _userManager
                .GeneratePasswordResetTokenAsync(employee.User);

            var result = await _userManager.ResetPasswordAsync(
                employee.User,
                token,
                model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        nameof(model.NewPassword),
                        error.Description);
                }

                return View(model);
            }

            await _userManager.UpdateSecurityStampAsync(employee.User);

            await _audit.LogAsync(
                AuditService.PasswordReset,
                nameof(Employee),
                employee.EmployeeNumber,
                $"Password reset for {model.FullName}.");

            TempData["Success"] =
                $"Password reset for {model.FullName}. They will be " +
                "signed out of any open sessions and must use the " +
                "new password.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            ViewBag.Blockers = await GetDeleteBlockers(employee.Id);
            ViewBag.Removals = await GetDeleteRemovals(employee.Id);

            return View(employee);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.User)
                .Include(e => e.Documents)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            var blockers = await GetDeleteBlockers(id);

            if (blockers.Count > 0)
            {
                TempData["Error"] =
                    "This employee cannot be deleted because their " +
                    "record is referenced elsewhere: " +
                    string.Join("; ", blockers) +
                    ". Mark them inactive instead.";

                return RedirectToAction(nameof(Index));
            }

            var filePaths = employee.Documents
                .Select(d => d.FilePath)
                .ToList();

            var user = employee.User;

            var description =
                $"{employee.FirstName} {employee.LastName} " +
                "and their balances, training assignments, " +
                "documents and notifications were deleted.";

            _context.Employees.Remove(employee);

            await _context.SaveChangesAsync();

            foreach (var filePath in filePaths)
            {
                _fileStorage.Delete(filePath);
            }

            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            await _audit.LogAsync(
                AuditService.Deleted,
                nameof(Employee),
                employee.EmployeeNumber,
                description);

            TempData["Success"] =
                "Employee deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<string>> GetDeleteBlockers(int employeeId)
        {
            var blockers = new List<string>();

            void Add(int count, string label)
            {
                if (count > 0)
                    blockers.Add($"{count} {label}");
            }

            Add(
                await _context.LeaveRequests
                    .CountAsync(r => r.EmployeeId == employeeId),
                "leave request(s)");

            Add(
                await _context.LeaveRequestHistories
                    .CountAsync(h => h.ActionedByEmployeeId == employeeId),
                "leave approval action(s)");

            Add(
                await _context.TrainingCourses
                    .CountAsync(c => c.CreatedByEmployeeId == employeeId),
                "training course(s) they created");

            Add(
                await _context.TrainingMaterials
                    .CountAsync(m => m.UploadedByEmployeeId == employeeId),
                "training material(s) they uploaded");

            Add(
                await _context.EmployeeDocuments
                    .CountAsync(d => d.UploadedByEmployeeId == employeeId),
                "document(s) they uploaded for others");

            Add(
                await _context.ApprovalConfigurations
                    .CountAsync(a => a.EmployeeId == employeeId),
                "approval role assignment(s)");

            return blockers;
        }

        private async Task<List<string>> GetDeleteRemovals(int employeeId)
        {
            var removals = new List<string>();

            void Add(int count, string label)
            {
                if (count > 0)
                    removals.Add($"{count} {label}");
            }

            Add(
                await _context.LeaveBalances
                    .CountAsync(b => b.EmployeeId == employeeId),
                "leave balance(s)");

            Add(
                await _context.TrainingAssignments
                    .CountAsync(a => a.EmployeeId == employeeId),
                "training assignment(s)");

            Add(
                await _context.EmployeeDocuments
                    .CountAsync(d => d.EmployeeId == employeeId),
                "document(s) held on file");

            Add(
                await _context.Notifications
                    .CountAsync(n => n.EmployeeId == employeeId),
                "notification(s)");

            return removals;
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
