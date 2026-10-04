using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// An employee's own training: what they have been assigned,
    /// the materials for each course, marking a course complete
    /// and leaving feedback afterwards.
    ///
    /// Every action is scoped to the signed-in user's assignments,
    /// so one employee cannot open another's training or download
    /// materials for a course they were never assigned.
    /// </summary>
    [Authorize]
    public class MyTrainingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileStorageService _fileStorage;
        private readonly CurrentUserService _currentUser;
        private readonly AuditService _audit;

        public MyTrainingController(
            ApplicationDbContext context,
            FileStorageService fileStorage,
            CurrentUserService currentUser,
            AuditService audit)
        {
            _context = context;
            _fileStorage = fileStorage;
            _currentUser = currentUser;
            _audit = audit;
        }


        // ============================================================
        // INDEX
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var assignments = await _context.TrainingAssignments
                .Include(a => a.TrainingCourse)
                .Include(a => a.Feedback)
                .Where(a => a.EmployeeId == employeeId.Value)
                .OrderBy(a => a.IsCompleted)
                .ThenBy(a => a.DueDate)
                .ToListAsync();

            return View(assignments);
        }


        // ============================================================
        // DETAILS
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await LoadOwnAssignment(id.Value);

            if (assignment == null)
                return NotFound();

            return View(assignment);
        }


        // ============================================================
        // DOWNLOAD MATERIAL
        // ============================================================

        /// <summary>
        /// Serves a course material, but only to someone actually
        /// assigned to that course.
        /// </summary>
        public async Task<IActionResult> Material(int? id)
        {
            if (id == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var material = await _context.TrainingMaterials
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
                return NotFound();

            var isAssigned = await _context.TrainingAssignments
                .AnyAsync(a =>
                    a.EmployeeId == employeeId.Value &&
                    a.TrainingCourseId == material.TrainingCourseId);

            if (!isAssigned)
                return NotFound();

            var fullPath =
                _fileStorage.ResolveExistingPath(material.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] =
                    "That file is no longer on the server.";

                return RedirectToAction(nameof(Index));
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);

            return File(
                bytes,
                contentType ?? "application/octet-stream",
                material.OriginalFileName);
        }


        // ============================================================
        // COMPLETE
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var assignment = await LoadOwnAssignment(id);

            if (assignment == null)
                return NotFound();

            if (assignment.IsCompleted)
            {
                TempData["Error"] =
                    "You have already completed this training.";

                return RedirectToAction(nameof(Index));
            }

            assignment.IsCompleted = true;
            assignment.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.TrainingCompleted,
                nameof(TrainingAssignment),
                assignment.Id.ToString(),
                $"{assignment.TrainingCourse.Title} completed.");

            TempData["Success"] =
                "Training marked as completed. Please leave your " +
                "feedback.";

            return RedirectToAction(nameof(Feedback), new { id });
        }


        // ============================================================
        // FEEDBACK - GET
        // ============================================================

        public async Task<IActionResult> Feedback(int? id)
        {
            if (id == null)
                return NotFound();

            var assignment = await LoadOwnAssignment(id.Value);

            if (assignment == null)
                return NotFound();

            if (!assignment.IsCompleted)
            {
                TempData["Error"] =
                    "Complete the training before leaving feedback.";

                return RedirectToAction(nameof(Index));
            }

            if (assignment.Feedback != null)
            {
                TempData["Error"] =
                    "You have already given feedback for this course.";

                return RedirectToAction(nameof(Index));
            }

            return View(new MyTrainingFeedbackViewModel
            {
                TrainingAssignmentId = assignment.Id,
                CourseTitle = assignment.TrainingCourse.Title
            });
        }


        // ============================================================
        // FEEDBACK - POST
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Feedback(
            MyTrainingFeedbackViewModel model)
        {
            var assignment = await LoadOwnAssignment(
                model.TrainingAssignmentId);

            if (assignment == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.CourseTitle = assignment.TrainingCourse.Title;
                return View(model);
            }

            if (!assignment.IsCompleted)
            {
                TempData["Error"] =
                    "Complete the training before leaving feedback.";

                return RedirectToAction(nameof(Index));
            }

            // Feedback is one per assignment, and the employee may
            // have opened this page twice.
            if (assignment.Feedback != null)
            {
                TempData["Error"] =
                    "You have already given feedback for this course.";

                return RedirectToAction(nameof(Index));
            }

            _context.TrainingFeedbacks.Add(
                new TrainingFeedback
                {
                    TrainingAssignmentId = assignment.Id,
                    Rating = model.Rating,
                    Comments = model.Comments?.Trim(),
                    SubmittedAt = DateTime.UtcNow
                });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Thank you, your feedback has been recorded.";

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private async Task<TrainingAssignment?> LoadOwnAssignment(
            int id)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return null;

            return await _context.TrainingAssignments
                .Include(a => a.TrainingCourse)
                    .ThenInclude(c => c.Materials)
                .Include(a => a.Feedback)
                .FirstOrDefaultAsync(a =>
                    a.Id == id &&
                    a.EmployeeId == employeeId.Value);
        }
    }
}
