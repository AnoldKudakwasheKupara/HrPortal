using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
    public class RequisitionWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;
        private readonly RequisitionOptions _options;

        public RequisitionWorkflowService(
            ApplicationDbContext context,
            NotificationService notifications,
            AuditService audit,
            IOptionsSnapshot<RequisitionOptions> options)
        {
            _context = context;
            _notifications = notifications;
            _audit = audit;
            _options = options.Value;
        }

        public decimal Threshold => _options.MdApprovalThreshold;

        public string Currency => _options.CurrencyCode;

        public static bool IsAwaitingReview(RequisitionStatus status) =>
            status == RequisitionStatus.Submitted;

        public static bool IsAwaitingMd(RequisitionStatus status) =>
            status == RequisitionStatus.ReviewApproved;

        public static bool IsWithRequester(RequisitionStatus status) =>
            status == RequisitionStatus.Draft ||
            status == RequisitionStatus.ReturnedToRequester;

        public static bool IsFinal(RequisitionStatus status) =>
            status == RequisitionStatus.Approved ||
            status == RequisitionStatus.Rejected ||
            status == RequisitionStatus.ReviewRejected ||
            status == RequisitionStatus.Cancelled;

        public static decimal TotalOf(Requisition requisition) =>
            requisition.Lines.Sum(l => l.Quantity * (l.EstimatedUnitCost ?? 0m));

        public async Task SubmitAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment,
            bool isResubmission)
        {
            var previous = requisition.Status;

            requisition.EstimatedTotal = TotalOf(requisition);
            requisition.RequiresMdApproval =
                requisition.EstimatedTotal >= _options.MdApprovalThreshold;

            var reviewerId = await _notifications
                .GetApproverEmployeeId(ApprovalRole.RequisitionReviewer);

            var mdId = await _notifications
                .GetApproverEmployeeId(ApprovalRole.MD);

            var isReviewersOwn =
                reviewerId != null && reviewerId.Value == requisition.EmployeeId;

            var isMdsOwn =
                mdId != null && mdId.Value == requisition.EmployeeId;

            if (isReviewersOwn && !isMdsOwn)
            {
                requisition.Status = RequisitionStatus.ReviewApproved;
                requisition.RequiresMdApproval = true;
            }
            else
            {
                requisition.Status = RequisitionStatus.Submitted;

                if (isMdsOwn)
                    requisition.RequiresMdApproval = false;
            }

            requisition.SubmittedAt = DateTime.UtcNow;

            AddHistory(
                requisition,
                actedByEmployeeId,
                isResubmission
                    ? RequisitionAction.Resubmitted
                    : RequisitionAction.Submitted,
                previous,
                comment);

            if (isReviewersOwn)
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.MD,
                    "Requisition awaiting approval",
                    $"{requisition.RequisitionNumber} " +
                    $"({Money(requisition.EstimatedTotal)}) was raised by " +
                    "the requisition reviewer and needs your decision.",
                    $"/RequisitionApproval/Details/{requisition.Id}");
            }
            else
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.RequisitionReviewer,
                    "Requisition awaiting review",
                    $"{requisition.RequisitionNumber} " +
                    $"({Money(requisition.EstimatedTotal)}) has been " +
                    "submitted and needs your review.",
                    $"/RequisitionApproval/Details/{requisition.Id}");
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionSubmitted,
                nameof(Requisition),
                requisition.RequisitionNumber,
                isResubmission
                    ? "Resubmitted for review."
                    : $"Submitted for review at " +
                      $"{Money(requisition.EstimatedTotal)}.");
        }

        public async Task ReviewApproveAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = requisition.Status;

            if (requisition.RequiresMdApproval)
            {
                requisition.Status = RequisitionStatus.ReviewApproved;

                AddHistory(
                    requisition,
                    actedByEmployeeId,
                    RequisitionAction.ApprovedByReviewer,
                    previous,
                    comment);

                _notifications.Notify(
                    requisition.EmployeeId,
                    "Requisition passed review",
                    $"{requisition.RequisitionNumber} has cleared review " +
                    "and has gone to the Managing Director for final " +
                    "approval.",
                    $"/MyRequisitions/Details/{requisition.Id}");

                await _notifications.NotifyApproverAsync(
                    ApprovalRole.MD,
                    "Requisition awaiting final approval",
                    $"{requisition.RequisitionNumber} " +
                    $"({Money(requisition.EstimatedTotal)}) has cleared " +
                    "review and needs your decision.",
                    $"/RequisitionApproval/Details/{requisition.Id}");

                await _context.SaveChangesAsync();

                await _audit.LogAsync(
                    AuditService.RequisitionApproved,
                    nameof(Requisition),
                    requisition.RequisitionNumber,
                    "Approved at review and sent to the MD.");

                return;
            }

            requisition.Status = RequisitionStatus.Approved;
            requisition.CompletedAt = DateTime.UtcNow;

            AddHistory(
                requisition,
                actedByEmployeeId,
                RequisitionAction.ApprovedByReviewer,
                previous,
                comment);

            var underThreshold =
                requisition.EstimatedTotal < _options.MdApprovalThreshold;

            var reason = underThreshold
                ? $"It fell below the {Money(_options.MdApprovalThreshold)} " +
                  "threshold, so no further approval was needed."
                : "Review was the final approval, as the Managing " +
                  "Director cannot approve their own requisition.";

            _notifications.Notify(
                requisition.EmployeeId,
                "Requisition approved",
                $"{requisition.RequisitionNumber} has been approved. {reason}",
                $"/MyRequisitions/Details/{requisition.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionApproved,
                nameof(Requisition),
                requisition.RequisitionNumber,
                underThreshold
                    ? $"Approved at review. Below the " +
                      $"{Money(_options.MdApprovalThreshold)} threshold, so " +
                      "no MD approval was required."
                    : "Approved at review as the final approval, the " +
                      "requester being the MD.");
        }

        public async Task ReviewRejectAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = requisition.Status;

            requisition.Status = RequisitionStatus.ReviewRejected;
            requisition.CompletedAt = DateTime.UtcNow;

            AddHistory(
                requisition,
                actedByEmployeeId,
                RequisitionAction.RejectedByReviewer,
                previous,
                comment);

            _notifications.Notify(
                requisition.EmployeeId,
                "Requisition rejected",
                $"{requisition.RequisitionNumber} was not approved at " +
                "review." + Because(comment),
                $"/MyRequisitions/Details/{requisition.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionRejected,
                nameof(Requisition),
                requisition.RequisitionNumber,
                "Rejected at review.");
        }

        public async Task ReturnAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = requisition.Status;

            requisition.Status = RequisitionStatus.ReturnedToRequester;

            AddHistory(
                requisition,
                actedByEmployeeId,
                RequisitionAction.ReturnedByReviewer,
                previous,
                comment);

            _notifications.Notify(
                requisition.EmployeeId,
                "Requisition returned to you",
                $"{requisition.RequisitionNumber} has been sent back for " +
                "amendment." + Because(comment),
                $"/MyRequisitions/Edit/{requisition.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionReturned,
                nameof(Requisition),
                requisition.RequisitionNumber,
                "Returned to the requester for amendment.");
        }

        public async Task MdApproveAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = requisition.Status;

            requisition.Status = RequisitionStatus.Approved;
            requisition.CompletedAt = DateTime.UtcNow;

            AddHistory(
                requisition,
                actedByEmployeeId,
                RequisitionAction.ApprovedByMD,
                previous,
                comment);

            _notifications.Notify(
                requisition.EmployeeId,
                "Requisition approved",
                $"{requisition.RequisitionNumber} has been approved by " +
                "the Managing Director.",
                $"/MyRequisitions/Details/{requisition.Id}");

            await _notifications.NotifyApproverAsync(
                ApprovalRole.RequisitionReviewer,
                "Requisition approved, ready to order",
                $"{requisition.RequisitionNumber} " +
                $"({Money(requisition.EstimatedTotal)}) has been approved " +
                "by the MD and can be placed.",
                $"/RequisitionApproval/Details/{requisition.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionApproved,
                nameof(Requisition),
                requisition.RequisitionNumber,
                $"Approved by the MD at " +
                $"{Money(requisition.EstimatedTotal)}.");
        }

        public async Task MdRejectAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = requisition.Status;

            requisition.Status = RequisitionStatus.Rejected;
            requisition.CompletedAt = DateTime.UtcNow;

            AddHistory(
                requisition,
                actedByEmployeeId,
                RequisitionAction.RejectedByMD,
                previous,
                comment);

            _notifications.Notify(
                requisition.EmployeeId,
                "Requisition rejected",
                $"{requisition.RequisitionNumber} was not approved by the " +
                "Managing Director." + Because(comment),
                $"/MyRequisitions/Details/{requisition.Id}");

            await _notifications.NotifyApproverAsync(
                ApprovalRole.RequisitionReviewer,
                "Requisition rejected by the MD",
                $"{requisition.RequisitionNumber} was not approved.",
                $"/RequisitionApproval/Details/{requisition.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionRejected,
                nameof(Requisition),
                requisition.RequisitionNumber,
                "Rejected by the MD.");
        }

        public async Task CancelAsync(
            Requisition requisition,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = requisition.Status;

            requisition.Status = RequisitionStatus.Cancelled;
            requisition.CompletedAt = DateTime.UtcNow;

            AddHistory(
                requisition,
                actedByEmployeeId,
                RequisitionAction.Cancelled,
                previous,
                comment);

            if (IsAwaitingReview(previous))
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.RequisitionReviewer,
                    "Requisition withdrawn",
                    $"{requisition.RequisitionNumber} was withdrawn by the " +
                    "requester and no longer needs your review.",
                    "/RequisitionApproval");
            }
            else if (IsAwaitingMd(previous))
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.MD,
                    "Requisition withdrawn",
                    $"{requisition.RequisitionNumber} was withdrawn by the " +
                    "requester and no longer needs your decision.",
                    "/RequisitionApproval/FinalApproval");
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.RequisitionCancelled,
                nameof(Requisition),
                requisition.RequisitionNumber,
                "Withdrawn by the requester.");
        }

        public async Task<string> GenerateRequisitionNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"REQ-{year}-";

            var lastNumber = await _context.Requisitions
                .Where(r => r.RequisitionNumber.StartsWith(prefix))
                .OrderByDescending(r => r.RequisitionNumber)
                .Select(r => r.RequisitionNumber)
                .FirstOrDefaultAsync();

            var next = 1;

            if (lastNumber != null &&
                int.TryParse(
                    lastNumber.Substring(prefix.Length),
                    out var parsed))
            {
                next = parsed + 1;
            }

            return $"{prefix}{next:D4}";
        }

        public string Money(decimal amount) =>
            $"{_options.CurrencyCode} {amount:N2}";

        private static string Because(string? comment) =>
            string.IsNullOrWhiteSpace(comment)
                ? string.Empty
                : $" Reason given: {comment.Trim()}";

        private void AddHistory(
            Requisition requisition,
            int actedByEmployeeId,
            RequisitionAction action,
            RequisitionStatus previousStatus,
            string? comment)
        {
            _context.RequisitionHistories.Add(
                new RequisitionHistory
                {
                    RequisitionId = requisition.Id,
                    ActionedByEmployeeId = actedByEmployeeId,
                    Action = action,
                    PreviousStatus = previousStatus,
                    NewStatus = requisition.Status,
                    Comment = string.IsNullOrWhiteSpace(comment)
                        ? null
                        : comment.Trim(),
                    ActionDate = DateTime.UtcNow
                });
        }
    }
}
