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

        private readonly NextOfKinPdfService _nextOfKinPdf;
        private readonly RecordDocumentPdfService _pdf;
        private readonly ListDocumentPdfService _lists;
        private readonly SupervisorService _supervisors;

        public EmployeeController(
            ApplicationDbContext context,
            EmployeeRecordService records,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            FileStorageService fileStorage,
            NextOfKinPdfService nextOfKinPdf,
            RecordDocumentPdfService pdf,
            ListDocumentPdfService lists,
            SupervisorService supervisors)
        {
            _context = context;
            _records = records;
            _userManager = userManager;
            _audit = audit;
            _fileStorage = fileStorage;
            _nextOfKinPdf = nextOfKinPdf;
            _pdf = pdf;
            _lists = lists;
            _supervisors = supervisors;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.User)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            return View(employees);
        }

        /// <summary>
        /// The staff list as a printable register. The same people in
        /// the same order as the page, so what comes out of the
        /// printer can be checked against what is on the screen.
        /// </summary>
        public async Task<IActionResult> List()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Supervisor)
                .Include(e => e.User)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .ToListAsync();

            var list = new ListDocument(
                "Employee List",
                "Everybody on record, active and left.");

            list.Column("Number", 1.1f)
                .Column("Name", 1.7f)
                .Column("Job title", 1.8f)
                .Column("Department", 1.5f)
                .Column("Supervisor", 1.5f)
                .Column("Joined", 1f)
                .Column("Sign-in", 0.9f)
                .Column("Status", 0.9f);

            foreach (var employee in employees)
            {
                list.Row(
                    ListDocument.Text(employee.EmployeeNumber),
                    $"{employee.FirstName} {employee.LastName}".Trim(),
                    ListDocument.Text(employee.JobTitle),
                    ListDocument.Text(employee.Department?.Name),
                    employee.Supervisor == null
                        ? null
                        : $"{employee.Supervisor.FirstName} " +
                          $"{employee.Supervisor.LastName}",
                    ListDocument.Date(employee.DateJoined),
                    employee.UserId == null ? "Not linked" : "Linked",
                    employee.IsActive ? "Active" : "Left");
            }

            list.Total("On record", employees.Count)
                .Total("Active", employees.Count(e => e.IsActive))
                .Total("Left", employees.Count(e => !e.IsActive))
                .Total(
                    "Without a sign-in",
                    employees.Count(e => e.UserId == null));

            list.EmptyMessage = "Nobody is on record yet.";

            return File(
                _lists.Render(list),
                "application/pdf",
                list.FileName);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Supervisor)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }

        /// <summary>
        /// The whole employee record as a printable document: the
        /// employment particulars, the personal ones, and the
        /// emergency contact. The next of kin form stays separate --
        /// that one is a form somebody signs, this is the record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.Supervisor)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            var who = $"{employee.FirstName} {employee.LastName}";

            var record = new RecordDocument(
                "Employee Record",
                employee.EmployeeNumber)
            {
                Headline = who,
                Caption = employee.Department?.Name,
                Chip = employee.IsActive ? "Active" : "Left"
            };

            record.Section("Employment", "Post, department and dates")
                .Add("Employee number", employee.EmployeeNumber)
                .Add("Job title", employee.JobTitle)
                .Add("Department", employee.Department?.Name)
                .Add(
                    "Reports to",
                    employee.Supervisor == null
                        ? null
                        : $"{employee.Supervisor.FirstName} " +
                          $"{employee.Supervisor.LastName}")
                .Add("Employment type", employee.EmploymentType.ToString())
                .Add("Date joined", employee.DateJoined)
                .Add("Contract end date", employee.ContractEndDate)
                .Add("Active", employee.IsActive)
                .Add("End date", employee.EndDate)
                .Add("Leaving reason", employee.LeavingReason);

            record.Section("Personal", "What the record holds")
                .Add("First name(s)", employee.FirstName)
                .Add("Surname", employee.LastName)
                .Add("National ID", employee.NationalId)
                .Add("Date of birth", employee.DateOfBirth)
                .Add("Gender", employee.Gender.ToString())
                .Add("Marital status", employee.MaritalStatus.ToString())
                .Add("Address", employee.Address);

            record.Section("Contact", "How to reach them")
                .Add("Email", employee.Email)
                .Add("Phone number", employee.PhoneNumber);

            record.Section("In an emergency", "Who we call")
                .Add("Name", employee.EmergencyContactName)
                .Add(
                    "Relationship",
                    employee.EmergencyContactRelationship)
                .Add("Phone number", employee.EmergencyContactPhone)
                .Add("Address", employee.EmergencyContactAddress)
                .AddStamp(
                    "Confirmed by the employee",
                    employee.NextOfKinConfirmedAt);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> NextOfKinForm(int? id)
        {
            if (id == null)
                return NotFound();

            var employee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return File(
                _nextOfKinPdf.Render(employee),
                "application/pdf",
                MyProfileController.FileName(employee));
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
                    .AnyAsync(e => e.Email.ToLower() == model.Email.ToLower());

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
                JobTitle = model.JobTitle?.Trim(),
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

                EmergencyContactAddress =
                    model.EmergencyContactAddress?.Trim(),
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
                JobTitle = employee.JobTitle,
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

                EmergencyContactAddress =
                    employee.EmergencyContactAddress,
                DateJoined = employee.DateJoined,
                EmploymentType = employee.EmploymentType,
                ContractEndDate = employee.ContractEndDate,
                DepartmentId = employee.DepartmentId,
                SupervisorId = employee.SupervisorId,
                IsActive = employee.IsActive,
                EndDate = employee.EndDate,
                LeavingReason = employee.LeavingReason
            };

            await LoadLookups(employee.Id);

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

            var supervisorProblem = await _supervisors
                .RejectionReasonAsync(id, model.SupervisorId);

            if (supervisorProblem != null)
            {
                ModelState.AddModelError(
                    nameof(model.SupervisorId),
                    supervisorProblem);
            }

            if (!ModelState.IsValid)
            {
                await LoadLookups(id);
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
            employee.JobTitle = model.JobTitle?.Trim();
            employee.PhoneNumber = model.PhoneNumber;
            employee.NationalId = model.NationalId;
            employee.DateOfBirth = model.DateOfBirth;
            employee.Gender = model.Gender;
            employee.MaritalStatus = model.MaritalStatus;
            employee.Address = model.Address?.Trim();
            var emergencyChanged =
                employee.EmergencyContactName !=
                    model.EmergencyContactName?.Trim() ||
                employee.EmergencyContactPhone !=
                    model.EmergencyContactPhone?.Trim() ||
                employee.EmergencyContactRelationship !=
                    model.EmergencyContactRelationship?.Trim() ||
                employee.EmergencyContactAddress !=
                    model.EmergencyContactAddress?.Trim();

            employee.EmergencyContactName =
                model.EmergencyContactName?.Trim();
            employee.EmergencyContactPhone =
                model.EmergencyContactPhone?.Trim();
            employee.EmergencyContactRelationship =
                model.EmergencyContactRelationship?.Trim();
            employee.EmergencyContactAddress =
                model.EmergencyContactAddress?.Trim();

            if (emergencyChanged)
                employee.NextOfKinConfirmedAt = null;

            employee.DateJoined = model.DateJoined;
            employee.EmploymentType = model.EmploymentType;
            employee.ContractEndDate = model.ContractEndDate;
            employee.DepartmentId = model.DepartmentId;
            employee.SupervisorId = model.SupervisorId;
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
                .AsNoTracking()
                .Include(e => e.Department)
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

        private async Task LoadLookups(int? employeeId = null)
        {
            await LoadDepartments();

            var candidates = employeeId == null
                ? await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync()
                : await _supervisors.CandidatesAsync(employeeId.Value);

            ViewBag.Supervisors = candidates
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = $"{e.FirstName} {e.LastName}" +
                           (string.IsNullOrWhiteSpace(e.JobTitle)
                               ? ""
                               : $" — {e.JobTitle}")
                })
                .ToList();
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
