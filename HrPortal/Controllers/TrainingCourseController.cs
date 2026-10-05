using HrPortal.Data;
using HrPortal.DbContexts;
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
    [Authorize(Roles = Roles.HrOnly)]
    public class TrainingCourseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TrainingCourseController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var courses = await _context.TrainingCourses
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
                    await GetCurrentEmployeeId() ?? 0
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
                Trainer = model.Trainer?.Trim(),
                CreatedByEmployeeId = model.CreatedByEmployeeId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.TrainingCourses.Add(course);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Training course created successfully.";

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
                Trainer = course.Trainer,
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
            course.Trainer = model.Trainer?.Trim();
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

        private async Task<int?> GetCurrentEmployeeId()
        {
            var userId = _userManager.GetUserId(User);

            if (!int.TryParse(userId, out var parsedUserId))
                return null;

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == parsedUserId);

            return employee?.Id;
        }
    }
}
