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
        private readonly EmployeeRecordService _records;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;
        private readonly FileStorageService _fileStorage;

        public EmployeeController(
            ApplicationDbContext context,
            EmployeeRecordService records,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            FileStorageService fileStorage)
        {
            _context = context;
            _records = records;
            _userManager = userManager;
            _audit = audit;
            _fileStorage = fileStorage;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Position)
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
                .Include(e => e.Position)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();

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
                await LoadLookups();
                return View(model);
            }

            foreach (var error in await _records.ValidateAsync(
                model.EmployeeNumber,
                model.DateJoined,
                model.DateOfBirth,
                model.ContractEndDate,
                null,
                true,
                null))
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups();
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

                await LoadLookups();
                return View(model);
            }

            var employee = new Employee
            {
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PositionId = model.PositionId,
                PhoneNumber = model.PhoneNumber,
                NationalId = model.NationalId,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                MaritalStatus = model.MaritalStatus,
                Address = model.Address?.Trim(),
                EmergencyContactName = model.EmergencyContactName?.Trim(),
                EmergencyContactPhone = model.EmergencyContactPhone?.Trim(),
                EmergencyContactRelationship =
                    model.EmergencyContactRelationship?.Trim(),
                DateJoined = model.DateJoined,
                EmploymentType = model.EmploymentType,
                ContractEndDate = model.ContractEndDate,
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

                await LoadLookups();
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

                await LoadLookups();
                    return View(model);
                }

                employee.UserId = user.Id;

                _context.Employees.Update(employee);

                await _context.SaveChangesAsync();

                var roleResult = await _userManager.AddToRoleAsync(
                    user,
                    Roles.Employee);

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

                await LoadLookups();
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
                PositionId = employee.PositionId,
                PhoneNumber = employee.PhoneNumber,
                NationalId = employee.NationalId,
                DateOfBirth = employee.DateOfBirth,
                Gender = employee.Gender,
                MaritalStatus = employee.MaritalStatus,
                Address = employee.Address,
                EmergencyContactName = employee.EmergencyContactName,
                EmergencyContactPhone = employee.EmergencyContactPhone,
                EmergencyContactRelationship =
                    employee.EmergencyContactRelationship,
                DateJoined = employee.DateJoined,
                EmploymentType = employee.EmploymentType,
                ContractEndDate = employee.ContractEndDate,
                DepartmentId = employee.DepartmentId,
                IsActive = employee.IsActive,
                EndDate = employee.EndDate,
                LeavingReason = employee.LeavingReason
            };

            await LoadLookups();

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

            foreach (var error in await _records.ValidateAsync(
                model.EmployeeNumber,
                model.DateJoined,
                model.DateOfBirth,
                model.ContractEndDate,
                model.EndDate,
                model.IsActive,
                id))
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups();
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
            employee.PositionId = model.PositionId;
            employee.PhoneNumber = model.PhoneNumber;
            employee.NationalId = model.NationalId;
            employee.DateOfBirth = model.DateOfBirth;
            employee.Gender = model.Gender;
            employee.MaritalStatus = model.MaritalStatus;
            employee.Address = model.Address?.Trim();
            employee.EmergencyContactName =
                model.EmergencyContactName?.Trim();
            employee.EmergencyContactPhone =
                model.EmergencyContactPhone?.Trim();
            employee.EmergencyContactRelationship =
                model.EmergencyContactRelationship?.Trim();
            employee.DateJoined = model.DateJoined;
            employee.EmploymentType = model.EmploymentType;
            employee.ContractEndDate = model.ContractEndDate;
            employee.DepartmentId = model.DepartmentId;
            employee.IsActive = model.IsActive;

            employee.EndDate = model.IsActive ? null : model.EndDate;

            employee.LeavingReason = model.IsActive
                ? null
                : model.LeavingReason?.Trim();

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
                .Include(e => e.Position)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            ViewBag.Blockers = await _records.DeleteBlockersAsync(employee.Id);
            ViewBag.Removals = await _records.DeleteRemovalsAsync(employee.Id);

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

            var blockers = await _records.DeleteBlockersAsync(id);

            if (blockers.Count > 0)
            {
                TempData["Error"] =
                    "This employee cannot be deleted because their " +
                    "record is referenced elsewhere: " +
                    string.Join("; ", blockers) +
                    ". Mark them as having left instead, on the edit " +
                    "page, which keeps their file.";

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

        private async Task LoadLookups()
        {
            await LoadDepartments();
            await LoadPositions();
        }

        private async Task LoadPositions()
        {
            ViewBag.Positions = await _context.Positions
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Title)
                .Select(p => new SelectListItem(p.Title, p.Id.ToString()))
                .ToListAsync();
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
