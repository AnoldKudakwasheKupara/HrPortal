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
    [Authorize(Roles = Roles.AdminOrMd)]
    public class RequisitionApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RequisitionWorkflowService _workflow;
        private readonly CurrentUserService _currentUser;
        private readonly NotificationService _notifications;

        public RequisitionApprovalController(
            ApplicationDbContext context,
            RequisitionWorkflowService workflow,
            CurrentUserService currentUser,
            NotificationService notifications)
        {
            _context = context;
            _workflow = workflow;
            _currentUser = currentUser;
            _notifications = notifications;
        }

        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Index()
        {
            var requisitions = await Queue()
                .Where(r => r.Status == RequisitionStatus.Submitted)
                .ToListAsync();

            ViewData["Title"] = "Requisitions Awaiting Review";
            ViewBag.Stage = "Review";

            return View("Queue", requisitions);
        }

        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> FinalApproval()
        {
            var requisitions = await Queue()
                .Where(r => r.Status == RequisitionStatus.ReviewApproved)
                .ToListAsync();

            ViewData["Title"] = "Requisitions Awaiting Final Approval";
            ViewBag.Stage = "MD";

            return View("Queue", requisitions);
        }

        public async Task<IActionResult> All(RequisitionStatus? status)
        {
            var query = _context.Requisitions
                .Include(r => r.Employee)
                .Include(r => r.Vendor)
                .Include(r => r.Lines)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var requisitions = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.StatusFilter = status;

            ViewBag.AwaitingReview = await _context.Requisitions
                .CountAsync(r => r.Status == RequisitionStatus.Submitted);

            ViewBag.AwaitingMd = await _context.Requisitions
                .CountAsync(r => r.Status == RequisitionStatus.ReviewApproved);

            ViewBag.ApprovedThisYear = await _context.Requisitions
                .Where(r =>
                    r.Status == RequisitionStatus.Approved &&
                    r.CompletedAt != null &&
                    r.CompletedAt.Value.Year == DateTime.UtcNow.Year)
                .SumAsync(r => (decimal?)r.EstimatedTotal) ?? 0m;

            ViewBag.Threshold = _workflow.Threshold;
            ViewBag.Currency = _workflow.Currency;

            return View(requisitions);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var requisition = await LoadAsync(id.Value);

            if (requisition == null)
                return NotFound();

            return View(await ToDecisionModelAsync(requisition));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Approve(int id, string? comment)
        {
            var requisition = await LoadAsync(id);

            if (requisition == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
                return Forbid();

            if (!RequisitionWorkflowService.IsAwaitingReview(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is not awaiting review.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (requisition.EmployeeId == actedBy.Value)
            {
                TempData["Error"] =
                    "You cannot approve a requisition you raised yourself.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.ReviewApproveAsync(
                requisition,
                actedBy.Value,
                comment);

            TempData["Success"] = requisition.RequiresMdApproval
                ? $"{requisition.RequisitionNumber} approved and sent to " +
                  "the Managing Director."
                : $"{requisition.RequisitionNumber} approved.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Reject(int id, string? comment)
        {
            var requisition = await LoadAsync(id);

            if (requisition == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
                return Forbid();

            if (!RequisitionWorkflowService.IsAwaitingReview(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is not awaiting review.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.ReviewRejectAsync(
                requisition,
                actedBy.Value,
                comment);

            TempData["Success"] =
                $"{requisition.RequisitionNumber} rejected.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOnly)]
        public async Task<IActionResult> Return(int id, string? comment)
        {
            var requisition = await LoadAsync(id);

            if (requisition == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
                return Forbid();

            if (!RequisitionWorkflowService.IsAwaitingReview(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is not awaiting review.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] =
                    "Say what needs changing before sending it back.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.ReturnAsync(
                requisition,
                actedBy.Value,
                comment);

            TempData["Success"] =
                $"{requisition.RequisitionNumber} sent back to the requester.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> FinalApprove(int id, string? comment)
        {
            var requisition = await LoadAsync(id);

            if (requisition == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
                return Forbid();

            if (!RequisitionWorkflowService.IsAwaitingMd(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is not awaiting final approval.";

                return RedirectToAction(nameof(Details), new { id });
            }

            if (requisition.EmployeeId == actedBy.Value)
            {
                TempData["Error"] =
                    "You cannot approve a requisition you raised yourself.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.MdApproveAsync(
                requisition,
                actedBy.Value,
                comment);

            TempData["Success"] =
                $"{requisition.RequisitionNumber} approved.";

            return RedirectToAction(nameof(FinalApproval));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.MdOnly)]
        public async Task<IActionResult> FinalReject(int id, string? comment)
        {
            var requisition = await LoadAsync(id);

            if (requisition == null)
                return NotFound();

            var actedBy = await _currentUser.GetEmployeeIdAsync();

            if (actedBy == null)
                return Forbid();

            if (!RequisitionWorkflowService.IsAwaitingMd(requisition.Status))
            {
                TempData["Error"] =
                    "This requisition is not awaiting final approval.";

                return RedirectToAction(nameof(Details), new { id });
            }

            await _workflow.MdRejectAsync(
                requisition,
                actedBy.Value,
                comment);

            TempData["Success"] =
                $"{requisition.RequisitionNumber} rejected.";

            return RedirectToAction(nameof(FinalApproval));
        }

        private IQueryable<Requisition> Queue() =>
            _context.Requisitions
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.Vendor)
                .Include(r => r.Lines)
                .OrderBy(r => r.SubmittedAt);

        private async Task<Requisition?> LoadAsync(int id) =>
            await _context.Requisitions
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.Vendor)
                .Include(r => r.Lines)
                .Include(r => r.History)
                    .ThenInclude(h => h.ActionedByEmployee)
                .FirstOrDefaultAsync(r => r.Id == id);

        private async Task<RequisitionDecisionViewModel> ToDecisionModelAsync(
            Requisition requisition)
        {
            var actedBy = await _currentUser.GetEmployeeIdAsync();

            var reviewerId = await _notifications
                .GetApproverEmployeeId(ApprovalRole.RequisitionReviewer);

            var isOwn = actedBy != null && actedBy.Value == requisition.EmployeeId;

            return new RequisitionDecisionViewModel
            {
                Requisition = requisition,

                CanReview =
                    RequisitionWorkflowService.IsAwaitingReview(requisition.Status) &&
                    User.IsInRole(Roles.Administrator) &&
                    !isOwn,

                CanDecideAsMd =
                    RequisitionWorkflowService.IsAwaitingMd(requisition.Status) &&
                    User.IsInRole(Roles.Md) &&
                    !isOwn,

                IsRequesterTheReviewer =
                    reviewerId != null &&
                    reviewerId.Value == requisition.EmployeeId,

                Threshold = _workflow.Threshold,
                Currency = _workflow.Currency
            };
        }
    }
}
