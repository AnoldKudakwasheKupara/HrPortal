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
    [Authorize(Roles = Roles.HrOrMd)]
    public class LeaveApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LeaveWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;

        private readonly LeaveApplicationPdfService _pdf;

        public LeaveApprovalController(
            ApplicationDbContext context,
            LeaveWorkflowService workflow,
            CurrentUserService currentUser,
            LeaveApplicationPdfService pdf)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _pdf = pdf;
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

            if (request == null)
                return NotFound();

            return File(
                await _pdf.RenderAsync(request),
                "application/pdf",
                $"Leave application - {request.RequestNumber}.pdf");
        }

        [Authorize(Roles = Roles.HrOnly)]
        public async Task<IActionResult> Index()
        {
            var requests = await _context.LeaveRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .Include(r => r.LeaveType)
                .Where(r =>
                    r.Status == LeaveRequestStatus.Submitted ||
                    r.Status == LeaveRequestStatus.HRReview)
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();

            ViewData["Title"] = "Leave Awaiting HR Review";
            ViewBag.Stage = "HR";

            return View("Queue", requests);
        }

        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> FinalApproval()
        {
            var requests = await _context.LeaveRequests
                .Include(r => r.Employee)
                .Include(r => r.LeaveType)
                .Where(r =>
                    r.Status == LeaveRequestStatus.HRApproved ||
                    r.Status == LeaveRequestStatus.MDReview)
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync();

            ViewData["Title"] = "Leave Awaiting Final Approval";
            ViewBag.Stage = "MD";

            return View("Queue", requests);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var request = await LoadRequest(id.Value);

            if (request == null)
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
                    User.IsInRole(Roles.Md)
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
