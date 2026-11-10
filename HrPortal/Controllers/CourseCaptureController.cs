using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Records course requests that were approved on paper before the
    /// portal, on the employee's behalf. For HR Officers and the IT
    /// department.
    /// </summary>
    [Authorize]
    public class CourseCaptureController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CourseCaptureAccess _access;
        private readonly CurrentUserService _currentUser;
        private readonly CourseApplicationWorkflowService _workflow;
        private readonly NotificationService _notifications;
        private readonly CourseApplicationPdfService _pdf;

        public CourseCaptureController(
            ApplicationDbContext context,
            CourseCaptureAccess access,
            CurrentUserService currentUser,
            CourseApplicationWorkflowService workflow,
            NotificationService notifications,
            CourseApplicationPdfService pdf)
        {
            _context = context;
            _access = access;
            _currentUser = currentUser;
            _workflow = workflow;
            _notifications = notifications;
            _pdf = pdf;
        }

        public async Task<IActionResult> Index()
        {
            if (!await _access.CanCaptureAsync(User))
                return Forbid();

            var captured = await _context.CourseApplicationHistories
                .AsNoTracking()
                .Where(h => h.Action == CourseApplicationAction.CapturedAsApproved)
                .Include(h => h.CourseApplication)
                    .ThenInclude(c => c.Employee)
                .Include(h => h.ActionedByEmployee)
                .OrderByDescending(h => h.ActionDate)
                .ToListAsync();

            return View(captured);
        }

        /// <summary>One captured application, read only.</summary>
        public async Task<IActionResult> Details(int id)
        {
            if (!await _access.CanCaptureAsync(User))
                return Forbid();

            var application = await LoadCapturedAsync(id);

            if (application == null)
                return NotFound();

            return View(application);
        }

        public async Task<IActionResult> Form(int id)
        {
            if (!await _access.CanCaptureAsync(User))
                return Forbid();

            var application = await LoadCapturedAsync(id);

            if (application == null)
                return NotFound();

            return File(
                _pdf.Render(application, hidePersonal: !User.IsInRole(Roles.HrOfficer)),
                "application/pdf",
                $"Course application - {application.ApplicationNumber}.pdf");
        }

        /// <summary>
        /// Only applications that were captured here. Ones that went through
        /// the normal approval steps stay with HR and the approvers.
        /// </summary>
        private Task<CourseApplication?> LoadCapturedAsync(int id) =>
            _context.CourseApplications
                .AsNoTracking()
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .Include(c => c.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.History.Any(h => h.Action == CourseApplicationAction.CapturedAsApproved));

        public async Task<IActionResult> Create()
        {
            if (!await _access.CanCaptureAsync(User))
                return Forbid();

            await LoadEmployeesAsync();

            return View(new CaptureCourseViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CaptureCourseViewModel model)
        {
            if (!await _access.CanCaptureAsync(User))
                return Forbid();

            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
            {
                ModelState.AddModelError(string.Empty,
                    "Your sign-in is not linked to an employee record, so the capture " +
                    "cannot be put in your name. Ask an administrator to link it.");
            }

            foreach (var (field, error) in CourseCapture.Check(model, DateTime.Today))
                ModelState.AddModelError(field, error);

            var employee = model.EmployeeId == null
                ? null
                : await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == model.EmployeeId && e.IsActive);

            if (model.EmployeeId != null && employee == null)
                ModelState.AddModelError(nameof(model.EmployeeId), "Choose an active employee.");

            if (!ModelState.IsValid)
            {
                await LoadEmployeesAsync();
                return View(model);
            }

            var application = CourseCapture.Build(
                model,
                await _workflow.GenerateApplicationNumberAsync(),
                me!.Value,
                DateTime.UtcNow);

            _context.CourseApplications.Add(application);

            await _context.SaveChangesAsync();

            _notifications.Notify(
                application.EmployeeId,
                "Approved course recorded",
                $"Your approved request for {application.ProgramIntended} has been " +
                $"recorded in the portal as {application.ApplicationNumber}.",
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{application.ApplicationNumber} captured as approved for " +
                $"{employee!.FirstName} {employee.LastName}. It now shows on their My Courses.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadEmployeesAsync()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new
                {
                    e.Id,
                    Name = e.FirstName + " " + e.LastName + " (" + e.EmployeeNumber + ")"
                })
                .ToListAsync();

            ViewBag.Employees = new SelectList(employees, "Id", "Name");
        }
    }
}
