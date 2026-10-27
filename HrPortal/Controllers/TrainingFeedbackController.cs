using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services;
using HrPortal.ViewModels;
using HrPortal.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize(Roles = Roles.HrOnly)]
    public class TrainingFeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RecordDocumentPdfService _pdf;

        public TrainingFeedbackController(
            ApplicationDbContext context,
            RecordDocumentPdfService pdf)
        {
            _context = context;
            _pdf = pdf;
        }

        /// <summary>
        /// One person's verdict on one course, as a printable
        /// record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var feedback = await _context.TrainingFeedbacks
                .AsNoTracking()
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.TrainingCourse)
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.Employee)
                        .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
                return NotFound();

            var assignment = feedback.TrainingAssignment;

            var who = $"{assignment.Employee.FirstName} " +
                      $"{assignment.Employee.LastName}";

            var record = new RecordDocument("Training Feedback")
            {
                Headline = assignment.TrainingCourse.Title,
                Caption = who,
                Chip = $"{feedback.Rating} out of 5"
            };

            record.Section("Course", "What was attended")
                .Add("Title", assignment.TrainingCourse.Title)
                .Add("Trainer", assignment.TrainingCourse.Trainer)
                .Add(
                    "Training date",
                    assignment.TrainingCourse.TrainingDate);

            record.Section("Respondent", "Who gave the feedback")
                .Add("Name", who)
                .Add(
                    "Employee number",
                    assignment.Employee.EmployeeNumber)
                .Add("Department", assignment.Employee.Department?.Name);

            record.Section("Verdict", "What they said")
                .Add("Rating", $"{feedback.Rating} out of 5")
                .AddStamp("Submitted", feedback.SubmittedAt);

            record.Note(feedback.Comments);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index()
        {
            var feedback = await _context.TrainingFeedbacks
                .AsNoTracking()
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.TrainingCourse)
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.Employee)
                .OrderByDescending(f => f.SubmittedAt)
                .ToListAsync();

            return View(feedback);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var feedback = await _context.TrainingFeedbacks
                .AsNoTracking()
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.TrainingCourse)
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.Employee)
                        .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
                return NotFound();

            return View(feedback);
        }

        public async Task<IActionResult> Create()
        {
            await LoadAssignmentsWithoutFeedback();

            return View(new TrainingFeedbackCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TrainingFeedbackCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadAssignmentsWithoutFeedback();
                return View(model);
            }

            var alreadyHasFeedback = await _context.TrainingFeedbacks
                .AnyAsync(f =>
                    f.TrainingAssignmentId == model.TrainingAssignmentId);

            if (alreadyHasFeedback)
            {
                ModelState.AddModelError(
                    "TrainingAssignmentId",
                    "Feedback has already been recorded for this " +
                    "assignment. Edit the existing feedback instead.");

                await LoadAssignmentsWithoutFeedback();
                return View(model);
            }

            var feedback = new TrainingFeedback
            {
                TrainingAssignmentId = model.TrainingAssignmentId,
                Rating = model.Rating,
                Comments = model.Comments?.Trim(),
                SubmittedAt = DateTime.UtcNow
            };

            _context.TrainingFeedbacks.Add(feedback);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training feedback recorded successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var feedback = await _context.TrainingFeedbacks
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.TrainingCourse)
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.Employee)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
                return NotFound();

            var model = new TrainingFeedbackEditViewModel
            {
                Id = feedback.Id,
                TrainingAssignmentId = feedback.TrainingAssignmentId,
                AssignmentSummary =
                    feedback.TrainingAssignment.TrainingCourse.Title +
                    " - " +
                    feedback.TrainingAssignment.Employee.FirstName +
                    " " +
                    feedback.TrainingAssignment.Employee.LastName,
                Rating = feedback.Rating,
                Comments = feedback.Comments
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TrainingFeedbackEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var feedback = await _context.TrainingFeedbacks
                .FindAsync(id);

            if (feedback == null)
                return NotFound();

            feedback.Rating = model.Rating;
            feedback.Comments = model.Comments?.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training feedback updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var feedback = await _context.TrainingFeedbacks
                .AsNoTracking()
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.TrainingCourse)
                .Include(f => f.TrainingAssignment)
                    .ThenInclude(a => a.Employee)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (feedback == null)
                return NotFound();

            return View(feedback);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var feedback = await _context.TrainingFeedbacks
                .FindAsync(id);

            if (feedback == null)
                return NotFound();

            _context.TrainingFeedbacks.Remove(feedback);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training feedback deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadAssignmentsWithoutFeedback()
        {
            var assignments = await _context.TrainingAssignments
                .Include(a => a.TrainingCourse)
                .Include(a => a.Employee)
                .Where(a => a.Feedback == null)
                .OrderBy(a => a.TrainingCourse.Title)
                .ThenBy(a => a.Employee.FirstName)
                .Select(a => new
                {
                    a.Id,
                    Summary =
                        a.TrainingCourse.Title +
                        " - " +
                        a.Employee.FirstName +
                        " " +
                        a.Employee.LastName
                })
                .ToListAsync();

            ViewBag.Assignments =
                new SelectList(
                    assignments,
                    "Id",
                    "Summary");
        }
    }
}
