using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Security;
using HrPortal.Services;
using HrPortal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Controllers
{
    /// <summary>
    /// A supervisor is a relationship, not a role: any employee may
    /// be one, so this controller cannot be gated on roles alone.
    /// The class guard asks only for a signed-in user and every
    /// action states who it is for -- the HR and MD queues by role,
    /// and anything to do with one request by whether the person
    /// asking has standing on that request.
    /// </summary>
    [Authorize]
    public class LeaveApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LeaveWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;

        private readonly LeaveApplicationPdfService _pdf;
        private readonly ListDocumentPdfService _lists;

        public LeaveApprovalController(
            ApplicationDbContext context,
            LeaveWorkflowService workflow,
            CurrentUserService currentUser,
            LeaveApplicationPdfService pdf,
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
        /// request: HR, the MD, or the employee's own supervisor.
        /// </summary>
        private async Task<bool> HasStandingAsync(LeaveRequest request)
        {
            if (User.IsInRole(Roles.HrOfficer) || User.IsInRole(Roles.Md))
                return true;

            return await IsSupervisorOfAsync(request);
        }

        private async Task<bool> IsSupervisorOfAsync(LeaveRequest request)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            if (me == null)
                return false;

            var supervisorId = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == request.EmployeeId)
                .Select(e => e.SupervisorId)
                .FirstOrDefaultAsync();

            return supervisorId == me.Value;
        }

        /// <summary>
        /// The leave application form behind the request being
        /// approved. The same document the applicant can download,
        /// so an approver prints what they are deciding on rather
        /// than a summary of it.
        /// </summary>
        public async Task<IActionResult> Form(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await LoadRequest(id.Value);

            if (request == null || !await HasStandingAsync(request))
                return NotFound();

            return File(
                await _pdf.RenderAsync(request),
                "application/pdf",
                $"Leave application - {request.RequestNumber}.pdf");
        }

        /// <summary>
        /// Which queue a request is sitting in. The name travels in
        /// the URL of a download, so it is spelled the way an
        /// approver would say it.
        /// </summary>
        public enum Stage
        {
            Supervisor,
            Hr,
            Md
        }

        /// <summary>
        /// What a queue is waiting on, oldest first. The pages and
        /// their downloads all ask this, so a printed queue can never
        /// hold a different set of requests from the screen it came
        /// from.
        /// </summary>
        private IQueryable<LeaveRequest> Queue(Stage stage, int? me)
        {
            var requests = _context.LeaveRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.LeaveType)
                .AsQueryable();

            requests = stage switch
            {
                Stage.Supervisor => requests.Where(r =>
                    r.Status == LeaveRequestStatus.SupervisorReview &&
                    r.Employee.SupervisorId == me.GetValueOrDefault()),

                Stage.Hr => requests.Where(r =>
                    r.Status == LeaveRequestStatus.Submitted ||
                    r.Status == LeaveRequestStatus.HRReview),

                _ => requests.Where(r =>
                    r.Status == LeaveRequestStatus.HRApproved ||
                    r.Status == LeaveRequestStatus.MDReview)
            };

            return requests.OrderBy(r => r.SubmittedAt);
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Index()
        {
            var requests = await Queue(Stage.Hr, null).ToListAsync();

            ViewData["Title"] = "Leave Awaiting HR Review";
            ViewBag.Stage = "HR";
            ViewBag.Queue = Stage.Hr;

            return View("Queue", requests);
        }

        /// <summary>
        /// What the people who report to me have sent up. Open to
        /// anybody signed in, because it shows only their own
        /// reports' requests and is empty for somebody who
        /// supervises nobody.
        /// </summary>
        public async Task<IActionResult> MyTeam()
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            var requests = await Queue(Stage.Supervisor, me)
                .ToListAsync();

            ViewData["Title"] = "Leave Awaiting Your Approval";
            ViewBag.Stage = "Supervisor";
            ViewBag.Queue = Stage.Supervisor;

            return View("Queue", requests);
        }

        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> FinalApproval()
        {
            var requests = await Queue(Stage.Md, null).ToListAsync();

            ViewData["Title"] = "Leave Awaiting Final Approval";
            ViewBag.Stage = "MD";
            ViewBag.Queue = Stage.Md;

            return View("Queue", requests);
        }

        /// <summary>
        /// A queue as a printable list, for taking into a meeting or
        /// filing alongside the decisions.
        ///
        /// The three pages carry their guard in an attribute. This
        /// one serves all three, so it has to ask in the body -- and
        /// it asks for the same thing the page does, because a
        /// download nobody checks is how a queue leaves the building.
        /// </summary>
        public async Task<IActionResult> List(Stage stage)
        {
            var me = await _currentUser.GetEmployeeIdAsync();

            var allowed = stage switch
            {
                Stage.Hr => User.IsInRole(Roles.HrOfficer),
                Stage.Md => User.IsInRole(Roles.Md),
                _ => me != null
            };

            if (!allowed)
                return Forbid();

            var requests = await Queue(stage, me).ToListAsync();

            var list = new ListDocument(
                "Leave Approvals",
                stage switch
                {
                    Stage.Hr =>
                        "Awaiting HR review. Oldest first.",
                    Stage.Md =>
                        "Awaiting the Managing Director. Oldest first.",
                    _ =>
                        "Awaiting your approval as supervisor. " +
                        "Oldest first."
                });

            list.Column("Reference", 1.2f)
                .Column("Employee", 1.6f)
                .Column("Department", 1.4f)
                .Column("Leave type", 1.3f)
                .Column("From", 1f)
                .Column("To", 1f)
                .Column("Days", 0.6f, alignRight: true)
                .Column("Submitted", 1.5f);

            foreach (var request in requests)
            {
                list.Row(
                    ListDocument.Text(request.RequestNumber),
                    $"{request.Employee.FirstName} " +
                    $"{request.Employee.LastName}",
                    ListDocument.Text(request.Employee.Department?.Name),
                    ListDocument.Text(request.LeaveType?.Name),
                    ListDocument.Date(request.StartDate),
                    ListDocument.Date(request.EndDate),
                    request.NumberOfDays.ToString("0.##"),
                    ListDocument.Stamp(request.SubmittedAt));
            }

            list.Total("Waiting", requests.Count)
                .Total(
                    "Days requested",
                    requests.Sum(r => r.NumberOfDays).ToString("0.##"));

            list.EmptyMessage = "Nothing is waiting at this stage.";

            return File(
                _lists.Render(list),
                "application/pdf",
                list.FileName);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await LoadRequest(id.Value);

            if (request == null || !await HasStandingAsync(request))
                return NotFound();

            var balance = await _context.LeaveBalances
                .AsNoTracking()
                .Include(b => b.LeaveType)
                .FirstOrDefaultAsync(b =>
                    b.EmployeeId == request.EmployeeId &&
                    b.LeaveTypeId == request.LeaveTypeId &&
                    b.Year == request.StartDate.Year);

            var model = new LeaveApprovalViewModel
            {
                Request = request,
                Balance = balance,
                CanActAsHr =
                    LeaveWorkflowService.IsAwaitingHr(request.Status) &&
                    User.IsInRole(Roles.HrOfficer),
                CanActAsMd =
                    LeaveWorkflowService.IsAwaitingMd(request.Status) &&
                    User.IsInRole(Roles.Md),
                CanActAsSupervisor =
                    LeaveWorkflowService.IsAwaitingSupervisor(
                        request.Status) &&
                    await IsSupervisorOfAsync(request)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> HrApprove(
            int id,
            string? comment)
        {
            return await ActAsHr(
                id,
                comment,
                (request, actorId) =>
                    _workflow.HrApproveAsync(request, actorId, comment),
                "Leave request approved and sent to the MD.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> HrReject(
            int id,
            string? comment)
        {
            return await ActAsHr(
                id,
                comment,
                (request, actorId) =>
                    _workflow.HrRejectAsync(request, actorId, comment),
                "Leave request rejected.",
                commentRequired: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> HrReturn(
            int id,
            string? comment)
        {
            return await ActAsHr(
                id,
                comment,
                (request, actorId) =>
                    _workflow.HrReturnAsync(request, actorId, comment),
                "Leave request returned to the employee.",
                commentRequired: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> MdApprove(
            int id,
            string? comment)
        {
            return await ActAsMd(
                id,
                comment,
                (request, actorId) =>
                    _workflow.MdApproveAsync(request, actorId, comment),
                "Leave request approved.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> MdReject(
            int id,
            string? comment)
        {
            return await ActAsMd(
                id,
                comment,
                (request, actorId) =>
                    _workflow.MdRejectAsync(request, actorId, comment),
                "Leave request rejected.",
                commentRequired: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupervisorApprove(
            int id,
            string? comment)
        {
            return await ActAsSupervisor(
                id,
                comment,
                (request, actorId) => _workflow.SupervisorApproveAsync(
                    request, actorId, comment),
                "Approved and sent on for review.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupervisorReject(
            int id,
            string? comment)
        {
            return await ActAsSupervisor(
                id,
                comment,
                (request, actorId) => _workflow.SupervisorRejectAsync(
                    request, actorId, comment),
                "Request declined.",
                commentRequired: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupervisorReturn(
            int id,
            string? comment)
        {
            return await ActAsSupervisor(
                id,
                comment,
                (request, actorId) => _workflow.SupervisorReturnAsync(
                    request, actorId, comment),
                "Request returned to the employee.",
                commentRequired: true);
        }

        /// <summary>
        /// Checked on the request rather than on a role: being
        /// somebody's supervisor is the whole permission.
        /// </summary>
        private async Task<IActionResult> ActAsSupervisor(
            int id,
            string? comment,
            Func<LeaveRequest, int, Task> action,
            string successMessage,
            bool commentRequired = false)
        {
            var request = await LoadRequest(id);

            if (request == null || !await IsSupervisorOfAsync(request))
                return NotFound();

            return await Act(
                id,
                comment,
                action,
                successMessage,
                commentRequired,
                LeaveWorkflowService.IsAwaitingSupervisor,
                "This request is not waiting for your approval.",
                nameof(MyTeam));
        }

        private async Task<IActionResult> ActAsHr(
            int id,
            string? comment,
            Func<LeaveRequest, int, Task> action,
            string successMessage,
            bool commentRequired = false)
        {
            return await Act(
                id,
                comment,
                action,
                successMessage,
                commentRequired,
                LeaveWorkflowService.IsAwaitingHr,
                "This request is not waiting for HR review.",
                nameof(Index));
        }

        private async Task<IActionResult> ActAsMd(
            int id,
            string? comment,
            Func<LeaveRequest, int, Task> action,
            string successMessage,
            bool commentRequired = false)
        {
            return await Act(
                id,
                comment,
                action,
                successMessage,
                commentRequired,
                LeaveWorkflowService.IsAwaitingMd,
                "This request has not yet cleared HR review.",
                nameof(FinalApproval));
        }

        private async Task<IActionResult> Act(
            int id,
            string? comment,
            Func<LeaveRequest, int, Task> action,
            string successMessage,
            bool commentRequired,
            Func<LeaveRequestStatus, bool> stageCheck,
            string wrongStageMessage,
            string queueAction)
        {
            var request = await _context.LeaveRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            if (!stageCheck(request.Status))
            {
                TempData["Error"] = wrongStageMessage;

                return RedirectToAction(queueAction);
            }

            if (commentRequired &&
                string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] =
                    "A comment is required when rejecting or " +
                    "returning a request.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var actorId = await _currentUser.GetEmployeeIdAsync();

            if (actorId == null)
            {
                TempData["Error"] =
                    "Your account is not linked to an employee " +
                    "record, so the action cannot be attributed. " +
                    "Link it on the employee record first.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await action(request, actorId.Value);

            TempData["Success"] = successMessage;

            return RedirectToAction(queueAction);
        }

        private async Task<LeaveRequest?> LoadRequest(int id)
        {
            return await _context.LeaveRequests
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.LeaveType)
                .Include(r => r.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(r => r.Id == id);
        }
    }
}
