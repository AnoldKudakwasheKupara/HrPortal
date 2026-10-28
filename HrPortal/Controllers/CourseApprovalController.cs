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
    /// <summary>
    /// A supervisor is a relationship, not a role, so this controller
    /// cannot be gated on roles alone. The class guard asks only for
    /// a signed-in user; the HR and Managing Consultant queues and
    /// decisions keep their role attributes, and anything to do with
    /// one application is gated on standing.
    /// </summary>
    [Authorize]
    public class CourseApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CourseApplicationWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;

        private readonly CourseApplicationPdfService _pdf;
        private readonly ListDocumentPdfService _lists;

        public CourseApprovalController(
            ApplicationDbContext context,
            CourseApplicationWorkflowService workflow,
            CurrentUserService currentUser,
            CourseApplicationPdfService pdf,
            ListDocumentPdfService lists)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _pdf = pdf;
            _lists = lists;
        }

        /// <summary>
        /// Whether the person asking has any business with this
        /// application: HR, the Managing Consultant, or the
        /// applicant's own supervisor.
        /// </summary>
        private async Task<bool> HasStandingAsync(
            CourseApplication application)
        {
            if (User.IsInRole(Roles.HrOfficer) || User.IsInRole(Roles.Md))
                return true;

            return await IsSupervisorOfAsync(application);
        }

        private async Task<bool> IsSupervisorOfAsync(
            CourseApplication application)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return false;

            var supervisorId = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == application.EmployeeId)
                .Select(e => e.SupervisorId)
                .FirstOrDefaultAsync();

            return supervisorId == me.Value;
        }

        /// <summary>
        /// Which queue an application is sitting in. The name
        /// travels in the URL of a download, so it is spelled the
        /// way an approver would say it.
        /// </summary>
        public enum Stage
        {
            Supervisor,
            Hr,
            Md,
            Approved
        }

        /// <summary>
        /// What a queue is waiting on. The pages and their downloads
        /// all ask this, so a printed queue can never hold a
        /// different set of applications from the screen it came
        /// from.
        /// </summary>
        private IQueryable<CourseApplication> Queue(Stage stage, int? me)
        {
            var query = _context.CourseApplications
                .AsNoTracking()
                .Include(c => c.Employee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            if (stage == Stage.Approved)
            {
                return query
                    .Where(c =>
                        c.Status == CourseApplicationStatus.Approved)
                    .OrderByDescending(c => c.CompletedAt);
            }

            query = stage switch
            {
                // Compared as a plain int rather than the nullable:
                // somebody with no employee record would otherwise
                // match every employee who has no supervisor.
                Stage.Supervisor => query.Where(c =>
                    c.Status ==
                        CourseApplicationStatus.SupervisorReview &&
                    c.Employee.SupervisorId == me.GetValueOrDefault()),

                Stage.Hr => query.Where(c =>
                    c.Status == CourseApplicationStatus.Submitted),

                _ => query.Where(c =>
                    c.Status == CourseApplicationStatus.HrApproved)
            };

            return query.OrderBy(c => c.SubmittedAt ?? c.CreatedAt);
        }

        /// <summary>
        /// What the people reporting to me have sent up.
        /// </summary>
        public async Task<IActionResult> MyTeam()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            var applications = await Queue(Stage.Supervisor, me)
                .ToListAsync();

            ViewData["Title"] = "Courses Awaiting Your Approval";

            ViewBag.Stage = "supervisor approval";
            ViewBag.Queue = Stage.Supervisor;

            return View("Index", applications);
        }

        /// <summary>
        /// A queue as a printable list, for taking into a meeting or
        /// filing alongside the decisions.
        ///
        /// The pages carry their guard in an attribute. This one
        /// serves all of them, so it has to ask in the body -- and
        /// it asks for the same thing the page does, because a
        /// download nobody checks is how a queue leaves the
        /// building.
        /// </summary>
        public async Task<IActionResult> List(Stage stage)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            var allowed = stage switch
            {
                Stage.Hr or Stage.Approved =>
                    User.IsInRole(Roles.HrOfficer),
                Stage.Md => User.IsInRole(Roles.Md),
                _ => me != null
            };

            if (!allowed)
                return Forbid();

            var applications = await Queue(stage, me).ToListAsync();

            var list = new ListDocument(
                "Course Applications",
                stage switch
                {
                    Stage.Hr =>
                        "Awaiting HR review. Oldest first.",
                    Stage.Md =>
                        "Awaiting the Managing Consultant. " +
                        "Oldest first.",
                    Stage.Approved =>
                        "Approved applications, most recent first.",
                    _ =>
                        "Awaiting your approval as supervisor. " +
                        "Oldest first."
                });

            list.Column("Reference", 1.2f)
                .Column("Employee", 1.5f)
                .Column("Department", 1.3f)
                .Column("Programme", 2.2f)
                .Column("Sat before", 0.9f)
                .Column("Exam date", 1.1f)
                .Column("Submitted", 1.3f)
                .Column("Stage", 1.4f);

            foreach (var application in applications)
            {
                list.Row(
                    ListDocument.Text(application.ApplicationNumber),
                    $"{application.Employee.FirstName} " +
                    $"{application.Employee.LastName}",
                    ListDocument.Text(
                        application.Employee.Department?.Name),
                    ListDocument.Text(application.ProgramIntended),
                    ListDocument.Yes(application.HasSatBefore),
                    ListDocument.Date(application.TargetExamDate),
                    ListDocument.Stamp(application.SubmittedAt),
                    StatusNames.Of(application.Status).Label);
            }

            list.Total("Applications", applications.Count);

            list.EmptyMessage = "Nothing is waiting at this stage.";

            return File(
                _lists.Render(list),
                "application/pdf",
                list.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SupervisorApprove(
            int id,
            string? comment) =>
            ActAsSupervisorAsync(
                id,
                (application, actorId) =>
                    _workflow.SupervisorApproveAsync(
                        application, actorId, comment),
                "Approved and sent to HR.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SupervisorReject(
            int id,
            string? comment) =>
            ActAsSupervisorAsync(
                id,
                (application, actorId) =>
                    _workflow.SupervisorRejectAsync(
                        application, actorId, comment),
                "Application declined.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SupervisorReturn(
            int id,
            string? comment) =>
            ActAsSupervisorAsync(
                id,
                (application, actorId) =>
                    _workflow.SupervisorReturnAsync(
                        application, actorId, comment),
                "Application returned to the applicant.");

        /// <summary>
        /// Checked on the application rather than on a role: being
        /// somebody's supervisor is the whole permission.
        /// </summary>
        private async Task<IActionResult> ActAsSupervisorAsync(
            int id,
            Func<CourseApplication, int, Task> act,
            string success)
        {
            var application = await LoadAsync(id);

            if (application == null ||
                !await IsSupervisorOfAsync(application))
            {
                return NotFound();
            }

            if (!CourseApplicationWorkflowService.IsAwaitingSupervisor(
                application.Status))
            {
                TempData["Error"] =
                    "This application is not waiting for your approval.";

                return RedirectToAction(nameof(MyTeam));
            }

            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return Forbid();

            await act(application, me.Value);

            TempData["Success"] = success;

            return RedirectToAction(nameof(MyTeam));
        }

        /// <summary>
        /// What is waiting on the person looking: HR sees what it has
        /// to review, the Managing Consultant what has cleared HR.
        /// </summary>
        [Authorize(Roles = Roles.HrOrMd)]
        public async Task<IActionResult> Index()
        {
            var stage = User.IsInRole(Roles.HrOfficer)
                ? Stage.Hr
                : Stage.Md;

            ViewBag.Stage = stage == Stage.Hr
                ? "HR review"
                : "Managing Consultant";

            ViewBag.Queue = stage;

            var applications = await Queue(stage, null).ToListAsync();

            return View(applications);
        }

        /// <summary>
        /// Approved applications, which HR prints and takes to
        /// Finance.
        /// </summary>
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Approved()
        {
            var applications = await Queue(Stage.Approved, null)
                .ToListAsync();

            ViewBag.Queue = Stage.Approved;

            return View(applications);
        }
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var application = await LoadAsync(id.Value);

            if (application == null || !await HasStandingAsync(application))
                return NotFound();

            ViewBag.CanActAsSupervisor =
                await IsSupervisorOfAsync(application);

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

            if (application == null || !await HasStandingAsync(application))
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
                .Include(c => c.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}
