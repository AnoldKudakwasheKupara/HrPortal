using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    public class LeaveWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly LeaveBalanceService _balances;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;

        public LeaveWorkflowService(
            ApplicationDbContext context,
            LeaveBalanceService balances,
            NotificationService notifications,
            AuditService audit)
        {
            _context = context;
            _balances = balances;
            _notifications = notifications;
            _audit = audit;
        }

        public static bool IsAwaitingHr(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.Submitted
                || status == LeaveRequestStatus.HRReview;
        }

        public static bool IsAwaitingMd(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.HRApproved
                || status == LeaveRequestStatus.MDReview;
        }

        public static bool IsFinal(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.Approved
                || status == LeaveRequestStatus.Rejected
                || status == LeaveRequestStatus.HRRejected
                || status == LeaveRequestStatus.Cancelled;
        }

        public static bool IsWithEmployee(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.Draft
                || status == LeaveRequestStatus.ReturnedToEmployee;
        }

        public async Task SubmitAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment,
            bool isResubmission)
        {
            var previous = request.Status;

            request.Status = LeaveRequestStatus.Submitted;
            request.SubmittedAt = DateTime.UtcNow;
            request.CompletedAt = null;

            await _balances.HoldAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                isResubmission
                    ? LeaveRequestAction.Resubmitted
                    : LeaveRequestAction.Submitted,
                previous,
                comment);

            await _notifications.NotifyApproverAsync(
                ApprovalRole.HROfficer,
                "Leave request submitted",
                $"{request.RequestNumber} is waiting for your review.",
                $"/LeaveApproval/Details/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveSubmitted,
                nameof(LeaveRequest),
                request.RequestNumber,
                $"{request.NumberOfDays:0.##} day(s) submitted for review.");
        }

        public async Task HrApproveAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = request.Status;

            request.Status = LeaveRequestStatus.HRApproved;

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.ApprovedByHR,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave approved by HR",
                $"{request.RequestNumber} has been approved by HR and " +
                "sent to the Managing Director.",
                $"/MyLeave/Details/{request.Id}");

            await _notifications.NotifyApproverAsync(
                ApprovalRole.MD,
                "Leave request awaiting final approval",
                $"{request.RequestNumber} has cleared HR and needs " +
                "your decision.",
                $"/LeaveApproval/Details/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveApproved,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Approved by HR and sent to the MD.");
        }

        public async Task HrRejectAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = request.Status;

            request.Status = LeaveRequestStatus.HRRejected;
            request.CompletedAt = DateTime.UtcNow;

            await _balances.ReleaseAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.RejectedByHR,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave rejected by HR",
                $"{request.RequestNumber} was rejected by HR.",
                $"/MyLeave/Details/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveRejected,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Rejected by HR.");
        }

        public async Task HrReturnAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = request.Status;

            request.Status = LeaveRequestStatus.ReturnedToEmployee;

            await _balances.ReleaseAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.ReturnedByHR,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave request returned",
                $"{request.RequestNumber} was returned to you for " +
                "correction. Update it and send it again.",
                $"/MyLeave/Edit/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveReturned,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Returned to the employee for correction.");
        }

        public async Task MdApproveAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = request.Status;

            request.Status = LeaveRequestStatus.Approved;
            request.CompletedAt = DateTime.UtcNow;

            await _balances.ConsumeAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.ApprovedByMD,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave approved",
                $"{request.RequestNumber} has been approved by the " +
                "Managing Director.",
                $"/MyLeave/Details/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveApproved,
                nameof(LeaveRequest),
                request.RequestNumber,
                $"Approved by the MD. {request.NumberOfDays:0.##} " +
                "day(s) deducted from the balance.");
        }

        public async Task MdRejectAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = request.Status;

            request.Status = LeaveRequestStatus.Rejected;
            request.CompletedAt = DateTime.UtcNow;

            await _balances.ReleaseAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.RejectedByMD,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave rejected",
                $"{request.RequestNumber} was rejected by the " +
                "Managing Director.",
                $"/MyLeave/Details/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveRejected,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Rejected by the MD.");
        }

        public async Task CancelAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            var previous = request.Status;

            if (LeaveBalanceService.HoldsPendingDays(previous))
                await _balances.ReleaseAsync(request);

            request.Status = LeaveRequestStatus.Cancelled;
            request.CompletedAt = DateTime.UtcNow;

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.Cancelled,
                previous,
                comment);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveCancelled,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Cancelled.");
        }

        public static decimal CountWorkingDays(
            DateTime startDate,
            DateTime endDate)
        {
            var days = 0;

            for (var date = startDate.Date;
                 date <= endDate.Date;
                 date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday)
                {
                    days++;
                }
            }

            return days;
        }

        public async Task<string> GenerateRequestNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"LR-{year}-";

            var lastNumber = await _context.LeaveRequests
                .Where(r => r.RequestNumber.StartsWith(prefix))
                .OrderByDescending(r => r.RequestNumber)
                .Select(r => r.RequestNumber)
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

        private void AddHistory(
            LeaveRequest request,
            int actedByEmployeeId,
            LeaveRequestAction action,
            LeaveRequestStatus previousStatus,
            string? comment)
        {
            _context.LeaveRequestHistories.Add(
                new LeaveRequestHistory
                {
                    LeaveRequestId = request.Id,
                    ActionedByEmployeeId = actedByEmployeeId,
                    Action = action,
                    PreviousStatus = previousStatus,
                    NewStatus = request.Status,
                    Comment = string.IsNullOrWhiteSpace(comment)
                        ? null
                        : comment.Trim(),
                    ActionDate = DateTime.UtcNow
                });
        }
    }
}
