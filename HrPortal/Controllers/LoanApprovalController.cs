using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// Loans are sanctioned by the Managing Director, so this is the
    /// only place a loan decision is taken.
    /// </summary>
/// <summary>
/// A supervisor is a relationship, not a role, so this controller
/// cannot be gated on roles alone. The class guard asks only for a
/// signed-in user; the Managing Director's queue and decisions keep
/// their role attribute, and anything to do with one application is
/// gated on whether the person asking has standing on it.
/// </summary>
    [Authorize]
    public class LoanApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LoanWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;
        private readonly FileStorageService _fileStorage;

        private readonly LoanApplicationPdfService _pdf;
        private readonly ListDocumentPdfService _lists;

        public LoanApprovalController(
            ApplicationDbContext context,
            LoanWorkflowService workflow,
            CurrentUserService currentUser,
            FileStorageService fileStorage,
            LoanApplicationPdfService pdf,
            ListDocumentPdfService lists)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _fileStorage = fileStorage;
            _pdf = pdf;
            _lists = lists;
        }

        /// <summary>
        /// Whether the person asking has any business with this
        /// application: the Managing Director, or the applicant's own
        /// supervisor.
        /// </summary>
        private async Task<bool> HasStandingAsync(
            LoanApplication application)
        {
            if (User.IsInRole(Roles.Md))
                return true;

            return await IsSupervisorOfAsync(application);
        }

        private async Task<bool> IsSupervisorOfAsync(
            LoanApplication application)
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
            Md
        }

        /// <summary>
        /// What a queue is waiting on, oldest first. The pages and
        /// their downloads all ask this, so a printed queue can
        /// never hold a different set of applications from the
        /// screen it came from.
        /// </summary>
        private IQueryable<LoanApplication> Queue(
            Stage stage,
            int? me,
            bool settled)
        {
            var query = _context.LoanApplications
                .AsNoTracking()
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            query = stage == Stage.Supervisor
                // Compared as a plain int rather than the nullable:
                // somebody with no employee record would otherwise
                // match every employee who has no supervisor.
                ? query.Where(l =>
                    l.Status == LoanApplicationStatus.SupervisorReview &&
                    l.Employee.SupervisorId == me.GetValueOrDefault())
                : settled
                    ? query.Where(l =>
                        l.Status != LoanApplicationStatus.Submitted)
                    : query.Where(l =>
                        l.Status == LoanApplicationStatus.Submitted);

            return query.OrderBy(l => l.SubmittedAt ?? l.CreatedAt);
        }

        /// <summary>
        /// What the people reporting to me have sent up.
        /// </summary>
        public async Task<IActionResult> MyTeam()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            var applications = await Queue(Stage.Supervisor, me, false)
                .ToListAsync();

            ViewData["Title"] = "Loans Awaiting Your Approval";
            ViewBag.Stage = "Supervisor";
            ViewBag.Queue = Stage.Supervisor;

            return View("Index", applications);
        }

        /// <summary>
        /// A queue as a printable list, for taking into a meeting or
        /// filing alongside the decisions.
        ///
        /// The pages carry their guard in an attribute. This one
        /// serves both, so it has to ask in the body -- and it asks
        /// for the same thing the page does, because a download
        /// nobody checks is how a queue leaves the building.
        /// </summary>
        public async Task<IActionResult> List(
            Stage stage,
            bool settled = false)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            var allowed = stage == Stage.Md
                ? User.IsInRole(Roles.Md)
                : me != null;

            if (!allowed)
                return Forbid();

            var applications = await Queue(stage, me, settled)
                .ToListAsync();

            var list = new ListDocument(
                "Loan Applications",
                stage == Stage.Supervisor
                    ? "Awaiting your approval as supervisor. " +
                      "Oldest first."
                    : settled
                        ? "Sanctioned, declined, returned and " +
                          "cancelled applications."
                        : "Awaiting the Managing Director's " +
                          "decision. Oldest first.");

            list.Column("Reference", 1.2f)
                .Column("Employee", 1.5f)
                .Column("Department", 1.3f)
                .Column("Purpose", 2f)
                .Column("Requested", 1.1f, alignRight: true)
                .Column("Authorised", 1.1f, alignRight: true)
                .Column("Months", 0.7f, alignRight: true)
                .Column("Submitted", 1.3f)
                .Column("Stage", 1.3f);

            foreach (var application in applications)
            {
                var currency = application.Currency.ToString()
                    .ToUpperInvariant();

                list.Row(
                    ListDocument.Text(application.ApplicationNumber),
                    $"{application.Employee.FirstName} " +
                    $"{application.Employee.LastName}",
                    ListDocument.Text(
                        application.Employee.Department?.Name),
                    ListDocument.Text(application.Purpose),
                    $"{currency} " +
                    $"{application.AmountRequested:N2}",
                    application.AmountAuthorised == null
                        ? null
                        : $"{currency} " +
                          $"{application.AmountAuthorised.Value:N2}",
                    application.RepaymentMonths.ToString("N0"),
                    ListDocument.Stamp(application.SubmittedAt),
                    StatusNames.Of(application.Status).Label);
            }

            list.Total("Applications", applications.Count)
                .Total(
                    "Requested",
                    applications
                        .Sum(a => a.AmountRequested)
                        .ToString("N2"))
                .Total(
                    "Authorised",
                    applications
                        .Sum(a => a.AmountAuthorised ?? 0m)
                        .ToString("N2"));

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
                "Approved and sent to the Managing Director.");

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
            Func<LoanApplication, int, Task> act,
            string success)
        {
            var application = await LoadAsync(id);

            if (application == null ||
                !await IsSupervisorOfAsync(application))
            {
                return NotFound();
            }

            if (!LoanWorkflowService.IsAwaitingSupervisor(
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

        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> Index(bool settled = false)
        {
            var applications = await Queue(Stage.Md, null, settled)
                .ToListAsync();

            ViewBag.Settled = settled;
            ViewBag.Queue = Stage.Md;

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

            ViewBag.Decision = new LoanDecisionViewModel
            {
                Id = application.Id,
                AmountAuthorised = application.AmountRequested,
                RepaymentStartDate = StartOfNextMonth()
            };

            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> Approve(
            int id,
            LoanDecisionViewModel model)
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

            if (!LoanWorkflowService.CanAct(
                    LoanApplicationAction.Approved,
                    application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status} and cannot be sanctioned.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Decision = model;
                return View(nameof(Details), application);
            }

            await _workflow.ApproveAsync(
                application,
                actedBy.Value,
                model.AmountAuthorised,
                model.RepaymentStartDate,
                model.Comment);

            TempData["Success"] =
                $"{application.ApplicationNumber} sanctioned for " +
                $"{model.AmountAuthorised:N2}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> Reject(int id, string? comment)
        {
            return await DecideAsync(
                id,
                comment,
                LoanApplicationAction.Rejected,
                "declined");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> Return(int id, string? comment)
        {
            return await DecideAsync(
                id,
                comment,
                LoanApplicationAction.Returned,
                "sent back to the applicant");
        }


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
                $"Loan application - " +
                $"{application.ApplicationNumber}.pdf");
        }
        public async Task<IActionResult> Attachment(
            int id,
            int attachmentId)
        {
            var application = await LoadAsync(id);

            if (application == null || !await HasStandingAsync(application))
                return NotFound();

            var attachment = application.Attachments
                .FirstOrDefault(a => a.Id == attachmentId);

            if (attachment == null)
                return NotFound();

            var fullPath =
                _fileStorage.ResolveExistingPath(attachment.FilePath);

            if (fullPath == null)
            {
                TempData["Error"] =
                    "That file is no longer on the server.";

                return RedirectToAction(nameof(Details), new { id });
            }

            new FileExtensionContentTypeProvider()
                .TryGetContentType(fullPath, out var contentType);

            return PhysicalFile(
                fullPath,
                contentType ?? "application/octet-stream",
                attachment.OriginalFileName);
        }

        private async Task<IActionResult> DecideAsync(
            int id,
            string? comment,
            LoanApplicationAction action,
            string described)
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

            if (!LoanWorkflowService.CanAct(action, application.Status))
            {
                TempData["Error"] =
                    $"{application.ApplicationNumber} is " +
                    $"{application.Status}, so it cannot be " +
                    $"{described}.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (action == LoanApplicationAction.Rejected)
            {
                await _workflow.RejectAsync(
                    application,
                    actedBy.Value,
                    comment);
            }
            else
            {
                await _workflow.ReturnAsync(
                    application,
                    actedBy.Value,
                    comment);
            }

            TempData["Success"] =
                $"{application.ApplicationNumber} {described}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Deductions normally begin with the next payroll run, so the
        /// form opens on the first of next month.
        /// </summary>
        private static DateTime StartOfNextMonth()
        {
            var today = DateTime.Today;

            return new DateTime(today.Year, today.Month, 1)
                .AddMonths(1);
        }

        private async Task<LoanApplication?> LoadAsync(int id)
        {
            return await _context.LoanApplications
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Attachments)
                .Include(l => l.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(l => l.Id == id);
        }
    }
}
