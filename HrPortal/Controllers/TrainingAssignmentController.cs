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
    [Authorize(Roles = Roles.AdminOrHr)]
    public class TrainingAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;
        private readonly RecordScope _scope;

        public TrainingAssignmentController(
            ApplicationDbContext context,
            NotificationService notifications,
            AuditService audit,
            RecordScope scope)
        {
            _context = context;
            _notifications = notifications;
            _audit = audit;
            _scope = scope;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index(int? courseId)
        {
            var query = await _scope.VisibleToAsync(
                _context.TrainingAssignments
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


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await _context.TrainingAssignments
                .Include(a => a.TrainingCourse)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .Include(a => a.Feedback)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            return View(assignment);
        }


        // ============================================================
        // CREATE - GET
        // ============================================================

        public async Task<IActionResult> Create(int? courseId)
        {
            await LoadCourses();
            await LoadEmployees();

            return View(new TrainingAssignmentCreateViewModel
            {
                TrainingCourseId = courseId ?? 0
            });
        }


        // ============================================================
        // CREATE - POST
        // ============================================================

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

            // Skip anyone already assigned to this course so the
            // unique course/employee pair is never violated.
            var alreadyAssigned = await _context.TrainingAssignments
                .Where(a => a.TrainingCourseId == model.TrainingCourseId)
                .Select(a => a.EmployeeId)
                .ToListAsync();

            var toAssign = model.EmployeeIds
                .Distinct()
                .Where(id => !alreadyAssigned.Contains(id))
                .ToList();

            if (!toAssign.Any())
            {
                ModelState.AddModelError(
                    "EmployeeIds",
                    "Every employee selected is already assigned " +
                    "to this course.");

                await LoadCourses();
                await LoadEmployees();
                return View(model);
            }

            var course = await _context.TrainingCourses
                .FirstOrDefaultAsync(c =>
                    c.Id == model.TrainingCourseId);

            foreach (var employeeId in toAssign)
            {
                _context.TrainingAssignments.Add(
                    new TrainingAssignment
                    {
                        TrainingCourseId = model.TrainingCourseId,
                        EmployeeId = employeeId,
                        DueDate = model.DueDate,
                        IsCompleted = false,
                        AssignedAt = DateTime.UtcNow
                    });

                _notifications.Notify(
                    employeeId,
                    "Training assigned",
                    $"You have been assigned \"{course?.Title}\"" +
                    (model.DueDate.HasValue
                        ? $", due {model.DueDate.Value:dd MMM yyyy}."
                        : "."),
                    "/MyTraining");
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.TrainingAssigned,
                nameof(TrainingCourse),
                course?.Title,
                $"Assigned to {toAssign.Count} employee(s).");

            var skipped = model.EmployeeIds.Distinct().Count()
                - toAssign.Count;

            TempData["Success"] =
                skipped > 0
                    ? $"{toAssign.Count} employee(s) assigned. " +
                      $"{skipped} were already assigned."
                    : $"{toAssign.Count} employee(s) assigned successfully.";

            return RedirectToAction(
                nameof(Index),
                new { courseId = model.TrainingCourseId });
        }


        // ============================================================
        // EDIT - GET
        // ============================================================

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


        // ============================================================
        // EDIT - POST
        // ============================================================

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

            // Keep the completion date consistent with the flag.
            assignment.CompletedAt =
                model.IsCompleted
                    ? model.CompletedAt ?? DateTime.UtcNow
                    : null;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training assignment updated successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // TOGGLE COMPLETION
        // ============================================================

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


        // ============================================================
        // DELETE - GET
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await _context.TrainingAssignments
                .Include(a => a.TrainingCourse)
                .Include(a => a.Employee)
                .Include(a => a.Feedback)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            return View(assignment);
        }


        // ============================================================
        // DELETE - POST
        // ============================================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.TrainingAssignments
                .FindAsync(id);

            if (assignment == null)
                return NotFound();

            // Feedback is cascade deleted with the assignment.
            _context.TrainingAssignments.Remove(assignment);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training assignment deleted successfully.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // HELPERS
        // ============================================================

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
