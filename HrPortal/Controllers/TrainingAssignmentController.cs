using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.ViewModels;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOnly)]
    public class TrainingAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;
        private readonly TrainingAssignmentService _assignments;
        private readonly RecordScope _scope;

        private readonly RecordDocumentPdfService _pdf;

        public TrainingAssignmentController(
            ApplicationDbContext context,
            NotificationService notifications,
            AuditService audit,
            TrainingAssignmentService assignments,
            RecordScope scope,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _notifications = notifications;
            _audit = audit;
            _assignments = assignments;
            _scope = scope;
            _pdf = pdf;
        }

        /// <summary>
        /// One person's assignment to one course, as a printable
        /// record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await _context.TrainingAssignments
                .AsNoTracking()
                .Include(a => a.TrainingCourse)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .Include(a => a.Feedback)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            var who = $"{assignment.Employee.FirstName} " +
                      $"{assignment.Employee.LastName}";

            var late =
                !assignment.IsCompleted &&
                assignment.DueDate != null &&
                assignment.DueDate < DateTime.Today;

            var record = new RecordDocument(
                "Training Assignment",
                assignment.Employee.EmployeeNumber)
            {
                Headline = who,
                Caption = assignment.TrainingCourse.Title,
                Chip = assignment.IsCompleted
                    ? "Completed"
                    : late ? "Overdue" : "Outstanding"
            };

            record.Section("Employee", "Who was assigned the course")
                .Add("Name", who)
                .Add(
                    "Employee number",
                    assignment.Employee.EmployeeNumber)
                .Add("Department", assignment.Employee.Department?.Name);

            record.Section("Course", "What they were assigned")
                .Add("Title", assignment.TrainingCourse.Title)
                .Add("Trainer", assignment.TrainingCourse.TrainerName)
                .Add(
                    "Training date",
                    assignment.TrainingCourse.TrainingDate);

            record.Section("Progress", "Where the assignment stands")
                .AddStamp("Assigned", assignment.AssignedAt)
                .Add("Due date", assignment.DueDate)
                .Add("Completed", assignment.IsCompleted)
                .AddStamp("Completed on", assignment.CompletedAt)
                .Add(
                    "Feedback given",
                    assignment.Feedback != null);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index(int? courseId)
        {
            var query = await _scope.VisibleToAsync(
                _context.TrainingAssignments
                    .AsNoTracking()
                    .Include(a => a.TrainingCourse)
                    .Include(a => a.Employee)
                    .Include(a => a.Feedback),
                User);

            if (courseId.HasValue)
            {
                query = query.Where(a =>
                    a.TrainingCourseId == courseId.Value);
            }

            var assignments = await query
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            await LoadCourses();

            ViewBag.SelectedCourseId = courseId;

            return View(assignments);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await _context.TrainingAssignments
                .AsNoTracking()
                .Include(a => a.TrainingCourse)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .Include(a => a.Feedback)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            return View(assignment);
        }

        public async Task<IActionResult> Create(int? courseId)
        {
            await LoadCourses();
            await LoadEmployees();

            return View(new TrainingAssignmentCreateViewModel
            {
                TrainingCourseId = courseId ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TrainingAssignmentCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            if (!model.EmployeeIds.Any())
            {
                ModelState.AddModelError(
                    "EmployeeIds",
                    "Select at least one employee to assign.");

                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }
            var course = await _context.TrainingCourses
                .FirstOrDefaultAsync(c =>
                    c.Id == model.TrainingCourseId);

            if (course == null)
                return NotFound();

            var outcome = await _assignments.AssignAsync(
                course, model.EmployeeIds, model.DueDate);

            if (outcome.DidNothing)
            {
                ModelState.AddModelError(
                    "EmployeeIds",
                    "Every employee selected is already assigned " +
                    "to this course.");

                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            TempData["Success"] =
                outcome.AlreadyAssigned > 0
                    ? $"{outcome.Assigned} employee(s) assigned. " +
                      $"{outcome.AlreadyAssigned} were already assigned."
                    : $"{outcome.Assigned} employee(s) assigned successfully.";

            return RedirectToAction(
                nameof(Index),
                new { courseId = model.TrainingCourseId });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await _context.TrainingAssignments
                .FindAsync(id);

            if (assignment == null)
                return NotFound();

            var model = new TrainingAssignmentEditViewModel
            {
                Id = assignment.Id,
                TrainingCourseId = assignment.TrainingCourseId,
                EmployeeId = assignment.EmployeeId,
                DueDate = assignment.DueDate,
                IsCompleted = assignment.IsCompleted,
                CompletedAt = assignment.CompletedAt
            };

            await LoadCourses();
            await LoadEmployees();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TrainingAssignmentEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            var assignment = await _context.TrainingAssignments
                .FindAsync(id);

            if (assignment == null)
                return NotFound();

            var duplicateExists = await _context.TrainingAssignments
                .AnyAsync(a =>
                    a.Id != id &&
                    a.TrainingCourseId == model.TrainingCourseId &&
                    a.EmployeeId == model.EmployeeId);

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "",
                    "That employee is already assigned to this course.");

                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            assignment.TrainingCourseId = model.TrainingCourseId;
            assignment.EmployeeId = model.EmployeeId;
            assignment.DueDate = model.DueDate;
            assignment.IsCompleted = model.IsCompleted;

            assignment.CompletedAt =
                model.IsCompleted
                    ? model.CompletedAt ?? DateTime.UtcNow
                    : null;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training assignment updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCompletion(int id)
        {
            var assignment = await _context.TrainingAssignments
                .FindAsync(id);

            if (assignment == null)
                return NotFound();

            assignment.IsCompleted = !assignment.IsCompleted;

            assignment.CompletedAt =
                assignment.IsCompleted
                    ? DateTime.UtcNow
                    : null;

            await _context.SaveChangesAsync();

            if (assignment.IsCompleted)
            {
                await _audit.LogAsync(
                    AuditService.TrainingCompleted,
                    nameof(TrainingAssignment),
                    assignment.Id.ToString(),
                    "Marked as completed.");
            }

            TempData["Success"] =
                assignment.IsCompleted
                    ? "Training marked as completed."
                    : "Training marked as outstanding.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await _context.TrainingAssignments
                .AsNoTracking()
                .Include(a => a.TrainingCourse)
                .Include(a => a.Employee)
                .Include(a => a.Feedback)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            return View(assignment);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.TrainingAssignments
                .FindAsync(id);

            if (assignment == null)
                return NotFound();

            _context.TrainingAssignments.Remove(assignment);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training assignment deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadCourses()
        {
            var courses = await _context.TrainingCourses
                .Where(c => c.IsActive)
                .OrderBy(c => c.Title)
                .ToListAsync();

            ViewBag.Courses =
                new SelectList(
                    courses,
                    "Id",
                    "Title");
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
