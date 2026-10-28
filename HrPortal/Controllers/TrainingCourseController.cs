using HrPortal.Data;
using HrPortal.DbContexts;
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
    [Authorize(Roles = Roles.HrOnly)]
    public class TrainingCourseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrentUserService _currentUser;

        private readonly RecordDocumentPdfService _pdf;
        private readonly TrainingAssignmentService _assignments;

        public TrainingCourseController(
            ApplicationDbContext context,
            CurrentUserService currentUser,
            RecordDocumentPdfService pdf,
            TrainingAssignmentService assignments)
        {
            _context = context;
            _currentUser = currentUser;
            _pdf = pdf;
            _assignments = assignments;
        }

        /// <summary>
        /// The course and how far its assignments have got, as a
        /// printable record.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.TrainingCourses
                .AsNoTracking()
                .Include(c => c.CreatedByEmployee)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            var assignments = await _context.TrainingAssignments
                .AsNoTracking()
                .Where(a => a.TrainingCourseId == course.Id)
                .ToListAsync();

            var done = assignments.Count(a => a.IsCompleted);

            var overdue = assignments.Count(a =>
                !a.IsCompleted &&
                a.DueDate != null &&
                a.DueDate < DateTime.Today);

            var author = course.CreatedByEmployee;

            var record = new RecordDocument("Training Course")
            {
                Headline = course.Title,
                Caption = course.TrainerName,
                Chip = course.IsActive ? "Active" : "Inactive"
            };

            record.Section("Course", "What is taught, and by whom")
                .Add("Title", course.Title)
                .Add("Trainer", course.TrainerName)
                .Add("Training date", course.TrainingDate)
                .Add("Content due", course.MaterialsDueDate)
                .Add("Active", course.IsActive)
                .Add(
                    "Created by",
                    author == null
                        ? null
                        : $"{author.FirstName} {author.LastName}")
                .AddStamp("Created", course.CreatedAt);

            record.Section("Take-up", "How far the assignments have got")
                .Add("Assigned to", assignments.Count)
                .Add("Completed", done)
                .Add("Outstanding", assignments.Count - done)
                .Add("Past the due date", overdue);

            record.Note(course.Description);

            return File(
                _pdf.Render(record),
                "application/pdf",
                record.FileName);
        }

        public async Task<IActionResult> Index()
        {
            var courses = await _context.TrainingCourses
                .AsNoTracking()
                .Include(c => c.CreatedByEmployee)
                .Include(c => c.Assignments)
                .Include(c => c.Materials)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(courses);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.TrainingCourses
                .AsNoTracking()
                .Include(c => c.CreatedByEmployee)
                .Include(c => c.Materials)
                .Include(c => c.Assignments)
                    .ThenInclude(a => a.Employee)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            return View(course);
        }

        public async Task<IActionResult> Create()
        {
            await LoadEmployees();

            return View(new TrainingCourseCreateViewModel
            {
                CreatedByEmployeeId =
                    await _currentUser.GetEmployeeIdAsync() ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TrainingCourseCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadEmployees();
                return View(model);
            }

            if (TrainerIsAmbiguous(
                    model.TrainerEmployeeId, model.Trainer))
            {
                await LoadEmployees();
                return View(model);
            }

            var title = model.Title.Trim();

            var exists = await _context.TrainingCourses
                .AnyAsync(c => c.Title.ToLower() == title.ToLower());

            if (exists)
            {
                ModelState.AddModelError(
                    "Title",
                    "A training course with this title already exists.");

                await LoadEmployees();
                return View(model);
            }

            var course = new TrainingCourse
            {
                Title = title,
                Description = model.Description?.Trim(),
                TrainingDate = model.TrainingDate,
                MaterialsDueDate = model.MaterialsDueDate,
                Trainer = model.TrainerEmployeeId == null
                    ? model.Trainer?.Trim()
                    : null,
                TrainerEmployeeId = model.TrainerEmployeeId,
                CreatedByEmployeeId = model.CreatedByEmployeeId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.TrainingCourses.Add(course);

            await _context.SaveChangesAsync();

            var outcome = await _assignments.AssignNewCourseAsync(
                course, model.AssignmentDueDate);

            TempData["Success"] = outcome.DidNothing
                ? "Training course created. There is nobody on the " +
                  "staff list to assign it to yet."
                : $"Training course created and assigned to " +
                  $"{outcome.Assigned} employee(s).";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var course = await _context.TrainingCourses
                .FindAsync(id);

            if (course == null)
                return NotFound();

            var model = new TrainingCourseEditViewModel
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                TrainingDate = course.TrainingDate,
                MaterialsDueDate = course.MaterialsDueDate,
                Trainer = course.Trainer,
                TrainerEmployeeId = course.TrainerEmployeeId,
                CreatedByEmployeeId = course.CreatedByEmployeeId,
                IsActive = course.IsActive
            };

            await LoadEmployees();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TrainingCourseEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadEmployees();
                return View(model);
            }

            var course = await _context.TrainingCourses
                .FindAsync(id);

            if (course == null)
                return NotFound();

            if (TrainerIsAmbiguous(
                    model.TrainerEmployeeId, model.Trainer))
            {
                await LoadEmployees();
                return View(model);
            }

            var title = model.Title.Trim();

            var duplicateExists = await _context.TrainingCourses
                .AnyAsync(c =>
                    c.Id != id &&
                    c.Title.ToLower() == title.ToLower());

            if (duplicateExists)
            {
                ModelState.AddModelError(
                    "Title",
                    "Another training course already uses this title.");

                await LoadEmployees();
                return View(model);
            }

            course.Title = title;
            course.Description = model.Description?.Trim();
            course.TrainingDate = model.TrainingDate;
            course.MaterialsDueDate = model.MaterialsDueDate;
            course.Trainer = model.TrainerEmployeeId == null
                ? model.Trainer?.Trim()
                : null;

            course.TrainerEmployeeId = model.TrainerEmployeeId;
            course.CreatedByEmployeeId = model.CreatedByEmployeeId;
            course.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training course updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var course = await _context.TrainingCourses
                .Include(c => c.Assignments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            if (course.IsActive)
            {
                var hasOutstandingAssignments =
                    course.Assignments
                        .Any(a => !a.IsCompleted);

                if (hasOutstandingAssignments)
                {
                    TempData["Error"] =
                        "This course cannot be deactivated because " +
                        "some employees have not completed it yet. " +
                        "Remove or complete their assignments first.";

                    return RedirectToAction(nameof(Index));
                }
            }

            course.IsActive = !course.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                course.IsActive
                    ? "Training course activated successfully."
                    : "Training course deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// A course has one trainer. Naming a member of staff and an
        /// outside provider at once leaves nothing to say which of
        /// them is turning up, so the form comes back rather than
        /// one of the two being quietly dropped.
        /// </summary>
        private bool TrainerIsAmbiguous(
            int? trainerEmployeeId,
            string? provider)
        {
            if (trainerEmployeeId == null ||
                string.IsNullOrWhiteSpace(provider))
            {
                return false;
            }

            ModelState.AddModelError(
                nameof(TrainingCourseCreateViewModel.Trainer),
                "Pick a member of staff or name an outside " +
                "provider, not both.");

            return true;
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
