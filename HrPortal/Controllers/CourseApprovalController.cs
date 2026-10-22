using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Course applications are reviewed by HR and decided by the
    /// Managing Consultant, and HR prints an approved one for
    /// Finance, so all three live here.
    /// </summary>
    [Authorize(Roles = Roles.HrOrMd)]
    public class CourseApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CourseApplicationWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;

        private readonly CourseApplicationPdfService _pdf;

        public CourseApprovalController(
            ApplicationDbContext context,
            CourseApplicationWorkflowService workflow,
            CurrentUserService currentUser,
            CourseApplicationPdfService pdf)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _pdf = pdf;
        }

        /// <summary>
        /// What is waiting on the person looking: HR sees what it has
        /// to review, the Managing Consultant what has cleared HR.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var waitingFor = User.IsInRole(Roles.HrOfficer)
                ? CourseApplicationStatus.Submitted
                : CourseApplicationStatus.HrApproved;

            ViewBag.Stage = User.IsInRole(Roles.HrOfficer)
                ? "HR review"
                : "Managing Consultant";

            var applications = await _context.CourseApplications
                .AsNoTracking()
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .Where(c => c.Status == waitingFor)
                .OrderBy(c => c.SubmittedAt ?? c.CreatedAt)
                .ToListAsync();

            return View(applications);
        }

        /// <summary>
        /// Approved applications, which HR prints and takes to
        /// Finance.
        /// </summary>
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Approved()
        {
            var applications = await _context.CourseApplications
                .AsNoTracking()
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .Where(c => c.Status == CourseApplicationStatus.Approved)
                .OrderByDescending(c => c.CompletedAt)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAsync(id.Value);

            if (application == null)
                return NotFound();

            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> HrApprove(int id, string? comment)
            => await ActAsync(
                id,
                comment,
                CourseApplicationAction.ApprovedByHr);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> HrReject(int id, string? comment)
            => await ActAsync(
                id,
                comment,
                CourseApplicationAction.RejectedByHr);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> HrReturn(int id, string? comment)
            => await ActAsync(
                id,
                comment,
                CourseApplicationAction.ReturnedByHr);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> Approve(int id, string? comment)
            => await ActAsync(
                id,
                comment,
                CourseApplicationAction.ApprovedByManagingConsultant);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> Reject(int id, string? comment)
            => await ActAsync(
                id,
                comment,
                CourseApplicationAction.RejectedByManagingConsultant);


        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAsync(id.Value);

            if (application == null)
                return NotFound();

            return File(
                _pdf.Render(application),
                "application/pdf",
                $"Course application - " +
                $"{application.ApplicationNumber}.pdf");
        }
        private async Task<IActionResult> ActAsync(
            int id,
            string? comment,
            CourseApplicationAction action)
        {
            var application = await LoadAsync(id);

            if (application == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
            {
                TempData["Error"] =
                    "Your sign-in is not linked to an employee " +
                    "record, so the decision cannot be recorded.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!CourseApplicationWorkflowService.CanAct(
                    action,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status}, so that is not possible " +
                    "from here.";

                return RedirectToAction(nameof(Details), new { id });
            }

            switch (action)
            {
                case CourseApplicationAction.ApprovedByHr:
                    await _workflow.HrApproveAsync(
                        application, actedBy.Value, comment);
                    TempData["Success"] =
                        $"{application.ApplicationNumber} sent to the " +
                        "Managing Consultant.";
                    break;

                case CourseApplicationAction.RejectedByHr:
                    await _workflow.HrRejectAsync(
                        application, actedBy.Value, comment);
                    TempData["Success"] =
                        $"{application.ApplicationNumber} declined.";
                    break;

                case CourseApplicationAction.ReturnedByHr:
                    await _workflow.HrReturnAsync(
                        application, actedBy.Value, comment);
                    TempData["Success"] =
                        $"{application.ApplicationNumber} returned to " +
                        "the applicant.";
                    break;

                case CourseApplicationAction.ApprovedByManagingConsultant:
                    await _workflow.ApproveAsync(
                        application, actedBy.Value, comment);
                    TempData["Success"] =
                        $"{application.ApplicationNumber} approved. " +
                        "HR can print it for Finance.";
                    break;

                default:
                    await _workflow.RejectAsync(
                        application, actedBy.Value, comment);
                    TempData["Success"] =
                        $"{application.ApplicationNumber} declined.";
                    break;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<CourseApplication?> LoadAsync(int id)
        {
            return await _context.CourseApplications
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Position)
                .Include(c => c.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}
