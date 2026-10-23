using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    [Authorize]
    public class MyCoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CourseApplicationWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;
        private readonly IAuthorizationService _authorization;

        private readonly CourseApplicationPdfService _pdf;

        public MyCoursesController(
            ApplicationDbContext context,
            CourseApplicationWorkflowService workflow,
            CurrentUserService currentUser,
            IAuthorizationService authorization,
            CourseApplicationPdfService pdf)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _authorization = authorization;
            _pdf = pdf;
        }

        public async Task<IActionResult> Index()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            var applications = await _context.CourseApplications
                .AsNoTracking()
                .Where(c => c.EmployeeId == employeeId.Value)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAuthorizedAsync(
                id.Value,
                Operations.View);

            if (application == null)
                return NotFound();

            return View(application);
        }


        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAuthorizedAsync(
                id.Value,
                Operations.View);

            if (application == null)
                return NotFound();

            return File(
                _pdf.Render(application),
                "application/pdf",
                $"Course application - " +
                $"{application.ApplicationNumber}.pdf");
        }
        public async Task<IActionResult> Create()
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            await LoadApplicantAsync();

            return View(new MyCourseApplicationViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            MyCourseApplicationViewModel model)
        {
            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            Check(model);

            if (!ModelState.IsValid)
            {
                await LoadApplicantAsync();
                return View(model);
            }

            var application = new CourseApplication
            {
                ApplicationNumber =
                    await _workflow.GenerateApplicationNumberAsync(),
                EmployeeId = employeeId.Value,
                Status = CourseApplicationStatus.Draft,
                ProgramIntended = model.ProgramIntended.Trim(),
                Justification = model.Justification.Trim(),
                HasSatBefore = model.HasSatBefore,
                Sitting = model.HasSatBefore
                    ? model.Sitting
                    : CourseSitting.FirstAttempt,
                TargetExamDate = model.TargetExamDate,
                PledgedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.CourseApplications.Add(application);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{application.ApplicationNumber} saved as a draft. " +
                "Send it when you are ready.";

            return RedirectToAction(
                nameof(Details),
                new { id = application.Id });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAuthorizedAsync(
                id.Value,
                Operations.Manage);

            if (application == null)
                return NotFound();

            if (!CourseApplicationWorkflowService.IsWithApplicant(
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be changed.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await LoadApplicantAsync();

            return View(new MyCourseApplicationViewModel
            {
                Id = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                ProgramIntended = application.ProgramIntended,
                Justification = application.Justification,
                HasSatBefore = application.HasSatBefore,
                Sitting = application.Sitting,
                TargetExamDate = application.TargetExamDate,
                Pledged = application.PledgedAt != null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            MyCourseApplicationViewModel model)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            if (!CourseApplicationWorkflowService.IsWithApplicant(
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be changed.";

                return RedirectToAction(nameof(Details), new { id });
            }

            Check(model);

            if (!ModelState.IsValid)
            {
                await LoadApplicantAsync();
                return View(model);
            }

            application.ProgramIntended = model.ProgramIntended.Trim();
            application.Justification = model.Justification.Trim();
            application.HasSatBefore = model.HasSatBefore;
            application.Sitting = model.HasSatBefore
                ? model.Sitting
                : CourseSitting.FirstAttempt;
            application.TargetExamDate = model.TargetExamDate;
            application.PledgedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{application.ApplicationNumber} updated.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int id)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            if (application.PledgedAt == null)
            {
                TempData["Error"] =
                    "Make the pledge before sending the application.";

                return RedirectToAction(nameof(Edit), new { id });
            }

            if (!CourseApplicationWorkflowService.CanAct(
                    CourseApplicationAction.Submitted,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be sent.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var isResubmission =
                application.Status ==
                    CourseApplicationStatus.ReturnedToApplicant;

            await _workflow.SubmitAsync(
                application,
                employeeId.Value,
                isResubmission);

            TempData["Success"] =
                $"{application.ApplicationNumber} has gone to HR.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var application = await LoadAuthorizedAsync(
                id,
                Operations.Manage);

            if (application == null)
                return NotFound();

            var employeeId = await _currentUser.GetEmployeeIdAsync();

            if (employeeId == null)
                return View("NoEmployeeRecord");

            if (!CourseApplicationWorkflowService.CanAct(
                    CourseApplicationAction.Cancelled,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be cancelled.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.CancelAsync(
                application,
                employeeId.Value,
                null);

            TempData["Success"] =
                $"{application.ApplicationNumber} cancelled.";

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// The pledge stands in place of the applicant's signature, so
        /// an application cannot be made without it.
        /// </summary>
        private void Check(MyCourseApplicationViewModel model)
        {
            if (!model.Pledged)
            {
                ModelState.AddModelError(
                    nameof(model.Pledged),
                    "Make the pledge to continue.");
            }

            if (model.HasSatBefore &&
                model.Sitting == CourseSitting.FirstAttempt)
            {
                ModelState.AddModelError(
                    nameof(model.Sitting),
                    "Say whether you are going up a level or " +
                    "re-writing.");
            }
        }

        private async Task LoadApplicantAsync()
        {
            ViewBag.Applicant = await _currentUser.GetEmployeeAsync();
        }

        private async Task<CourseApplication?> LoadAuthorizedAsync(
            int id,
            OperationAuthorizationRequirement operation)
        {
            var application = await _context.CourseApplications
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .Include(c => c.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (application == null)
                return null;

            var result = await _authorization.AuthorizeAsync(
                User,
                application,
                operation);

            return result.Succeeded ? application : null;
        }
    }
}
