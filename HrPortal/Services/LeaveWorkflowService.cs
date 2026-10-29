using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrPortal.Services
{
    public class LeaveWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly LeaveBalanceService _balances;
        private readonly NotificationService _notifications;
        private readonly AuditService _audit;
        private readonly WorkingCalendar _calendar;

        private readonly SupervisorService _supervisors;
        private readonly ApproverDirectory _approvers;

        public LeaveWorkflowService(
            ApplicationDbContext context,
            LeaveBalanceService balances,
            NotificationService notifications,
            AuditService audit,
            WorkingCalendar calendar,
            SupervisorService supervisors,
            ApproverDirectory approvers)
        {
            _context = context;
            _balances = balances;
            _notifications = notifications;
            _audit = audit;
            _calendar = calendar;
            _supervisors = supervisors;
            _approvers = approvers;
        }

        public async Task<LeaveValidationResult> ValidateRequestAsync(
            int employeeId,
            int leaveTypeId,
            DateTime startDate,
            DateTime endDate,
            int? excludeRequestId)
        {
            var result = new LeaveValidationResult();

            if (endDate.Date < startDate.Date)
            {
                result.Add(
                    LeaveValidationResult.EndDateField,
                    "The end date cannot be before the start date.");

                return result;
            }

            result.NumberOfDays = await _calendar.CountWorkingDaysAsync(
                startDate,
                endDate);

            if (result.NumberOfDays == 0)
            {
                result.Add(
                    LeaveValidationResult.EndDateField,
                    "That range has no working days in it, once " +
                    "weekends and public holidays are taken out.");

                return result;
            }

            var overlaps = await _context.LeaveRequests
                .AnyAsync(r =>
                    r.EmployeeId == employeeId &&
                    (excludeRequestId == null ||
                     r.Id != excludeRequestId.Value) &&
                    r.Status != LeaveRequestStatus.Rejected &&
                    r.Status != LeaveRequestStatus.HRRejected &&
                    r.Status != LeaveRequestStatus.Cancelled &&
                    r.StartDate <= endDate &&
                    r.EndDate >= startDate);

            if (overlaps)
            {
                result.Add(
                    LeaveValidationResult.FormField,
                    "You already have leave booked that overlaps " +
                    "these dates.");

                return result;
            }

            return result;
        }

        /// <summary>
        /// Sitting with the employee's own supervisor, which is
        /// where a request starts when they have one.
        /// </summary>
        public static bool IsAwaitingSupervisor(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.SupervisorReview;
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
                || status == LeaveRequestStatus.SupervisorRejected
                || status == LeaveRequestStatus.Cancelled;
        }

        public static bool IsWithEmployee(LeaveRequestStatus status)
        {
            return status == LeaveRequestStatus.Draft
                || status == LeaveRequestStatus.ReturnedToEmployee;
        }

        public static bool CanAct(
            LeaveRequestAction action,
            LeaveRequestStatus status) => action switch
            {
                LeaveRequestAction.Submitted or
                LeaveRequestAction.Resubmitted =>
                    IsWithEmployee(status),

                LeaveRequestAction.ApprovedBySupervisor or
                LeaveRequestAction.RejectedBySupervisor or
                LeaveRequestAction.ReturnedBySupervisor =>
                    IsAwaitingSupervisor(status),

                LeaveRequestAction.ApprovedByHR or
                LeaveRequestAction.RejectedByHR or
                LeaveRequestAction.ReturnedByHR =>
                    IsAwaitingHr(status),

                LeaveRequestAction.ApprovedByMD or
                LeaveRequestAction.RejectedByMD =>
                    IsAwaitingMd(status),

                LeaveRequestAction.Cancelled =>
                    !IsFinal(status),

                _ => false
            };

        private static void EnsureCanAct(
            LeaveRequestAction action,
            LeaveRequest request)
        {
            if (CanAct(action, request.Status))
                return;

            throw new InvalidOperationException(
                $"{request.RequestNumber} is {request.Status}, so the " +
                $"action {action} is not allowed from here.");
        }

        public async Task SubmitAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment,
            bool isResubmission)
        {
            EnsureCanAct(
                isResubmission
                    ? LeaveRequestAction.Resubmitted
                    : LeaveRequestAction.Submitted,
                request);

            var previous = request.Status;

            var isHrOfficersOwn = await _approvers.HoldsAsync(
                ApprovalRole.HROfficer, request.EmployeeId);

            var supervisor = await _supervisors
                .SupervisorOfAsync(request.EmployeeId);

            request.Status = supervisor != null
                ? LeaveRequestStatus.SupervisorReview
                : isHrOfficersOwn
                    ? LeaveRequestStatus.HRApproved
                    : LeaveRequestStatus.Submitted;

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
                supervisor != null
                    ? $"Sent to {supervisor.FirstName} " +
                      $"{supervisor.LastName} for supervisor approval."
                    : isHrOfficersOwn
                        ? "Sent straight to the Managing Director: the " +
                          "request belongs to the HR Officer."
                        : comment);

            if (supervisor != null)
            {
                _notifications.Notify(
                    supervisor.Id,
                    "Leave request awaiting your approval",
                    $"{request.RequestNumber} is waiting for your " +
                    "approval as supervisor.",
                    $"/LeaveApproval/Details/{request.Id}",
                    EmailKind.ActionNeeded);
            }
            else if (isHrOfficersOwn)
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.MD,
                    "Leave request awaiting final approval",
                    $"{request.RequestNumber} is the HR Officer's own " +
                    "request and needs your decision.",
                    $"/LeaveApproval/Details/{request.Id}",
                    EmailKind.ActionNeeded);
            }
            else
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.HROfficer,
                    "Leave request submitted",
                    $"{request.RequestNumber} is waiting for your review.",
                    $"/LeaveApproval/Details/{request.Id}",
                    EmailKind.ActionNeeded);
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveSubmitted,
                nameof(LeaveRequest),
                request.RequestNumber,
                $"{request.NumberOfDays:0.##} day(s) submitted " +
                (isHrOfficersOwn
                    ? "straight to the MD."
                    : "for review."));
        }

        /// <summary>
        /// The supervisor signs off and the request carries on into
        /// the flow it would have taken had there been no supervisor
        /// -- including the rule that sends the HR Officer's own
        /// request straight to the Managing Director.
        /// </summary>
        public async Task SupervisorApproveAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LeaveRequestAction.ApprovedBySupervisor, request);

            var previous = request.Status;

            var isHrOfficersOwn = await _approvers.HoldsAsync(
                ApprovalRole.HROfficer, request.EmployeeId);

            request.Status = isHrOfficersOwn
                ? LeaveRequestStatus.HRApproved
                : LeaveRequestStatus.Submitted;

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.ApprovedBySupervisor,
                previous,
                comment);

            await _notifications.NotifyApproverAsync(
                isHrOfficersOwn ? ApprovalRole.MD : ApprovalRole.HROfficer,
                "Leave request approved by supervisor",
                $"{request.RequestNumber} has been approved by the " +
                "employee's supervisor and is waiting for you.",
                $"/LeaveApproval/Details/{request.Id}",
                EmailKind.ActionNeeded);

            _notifications.Notify(
                request.EmployeeId,
                "Leave request approved by your supervisor",
                $"{request.RequestNumber} has gone on for review.",
                $"/MyLeave/Details/{request.Id}");

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveSubmitted,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Approved by the employee's supervisor.");
        }

        public async Task SupervisorRejectAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LeaveRequestAction.RejectedBySupervisor, request);

            var previous = request.Status;

            request.Status = LeaveRequestStatus.SupervisorRejected;
            request.CompletedAt = DateTime.UtcNow;

            await _balances.ReleaseAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.RejectedBySupervisor,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave request declined by your supervisor",
                $"{request.RequestNumber} was declined." +
                (string.IsNullOrWhiteSpace(comment)
                    ? ""
                    : $" {comment}"),
                $"/MyLeave/Details/{request.Id}",
                EmailKind.Declined);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveSubmitted,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Declined by the employee's supervisor.");
        }

        /// <summary>
        /// Sends it back for the employee to correct and resubmit,
        /// which is usually what a supervisor actually wants.
        /// </summary>
        public async Task SupervisorReturnAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LeaveRequestAction.ReturnedBySupervisor, request);

            var previous = request.Status;

            request.Status = LeaveRequestStatus.ReturnedToEmployee;

            await _balances.ReleaseAsync(request);

            AddHistory(
                request,
                actedByEmployeeId,
                LeaveRequestAction.ReturnedBySupervisor,
                previous,
                comment);

            _notifications.Notify(
                request.EmployeeId,
                "Leave request returned by your supervisor",
                $"{request.RequestNumber} needs a change before it can " +
                "go on." +
                (string.IsNullOrWhiteSpace(comment)
                    ? ""
                    : $" {comment}"),
                $"/MyLeave/Details/{request.Id}",
                EmailKind.Returned);

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveSubmitted,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Returned by the employee's supervisor.");
        }

        public async Task HrApproveAsync(
            LeaveRequest request,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LeaveRequestAction.ApprovedByHR, request);

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
                $"/LeaveApproval/Details/{request.Id}",
                EmailKind.ActionNeeded);

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
            EnsureCanAct(LeaveRequestAction.RejectedByHR, request);

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
                $"/MyLeave/Details/{request.Id}",
                EmailKind.Declined);

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
            EnsureCanAct(LeaveRequestAction.ReturnedByHR, request);

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
                $"/MyLeave/Edit/{request.Id}",
                EmailKind.Returned);

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
            EnsureCanAct(LeaveRequestAction.ApprovedByMD, request);

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
                $"/MyLeave/Details/{request.Id}",
                EmailKind.Approved);

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
            EnsureCanAct(LeaveRequestAction.RejectedByMD, request);

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
                $"/MyLeave/Details/{request.Id}",
                EmailKind.Declined);

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
            EnsureCanAct(LeaveRequestAction.Cancelled, request);

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

            if (IsAwaitingHr(previous))
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.HROfficer,
                    "Leave request withdrawn",
                    $"{request.RequestNumber} was withdrawn by the " +
                    "employee and no longer needs your review.",
                    "/LeaveApproval");
            }
            else if (IsAwaitingMd(previous))
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.MD,
                    "Leave request withdrawn",
                    $"{request.RequestNumber} was withdrawn by the " +
                    "employee and no longer needs your decision.",
                    "/LeaveApproval/FinalApproval");
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                AuditService.LeaveCancelled,
                nameof(LeaveRequest),
                request.RequestNumber,
                "Cancelled.");
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
