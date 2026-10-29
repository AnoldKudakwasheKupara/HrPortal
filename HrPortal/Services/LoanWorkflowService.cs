using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Moves a loan application between states. A loan is sanctioned
    /// by the Managing Director, so there is one decision point
    /// rather than the two that leave goes through.
    /// </summary>
    public class LoanWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly SupervisorService _supervisors;

        public LoanWorkflowService(
            ApplicationDbContext context,
            NotificationService notifications,
            SupervisorService supervisors)
        {
            _context = context;
            _notifications = notifications;
            _supervisors = supervisors;
        }

        /// <summary>
        /// Sitting with the applicant's own supervisor, which is
        /// where an application starts when they have one.
        /// </summary>
        public static bool IsAwaitingSupervisor(
            LoanApplicationStatus status) =>
            status is LoanApplicationStatus.SupervisorReview;

        public static bool IsWithApplicant(LoanApplicationStatus status) =>
            status is LoanApplicationStatus.Draft
                or LoanApplicationStatus.ReturnedToApplicant;

        public static bool IsAwaitingMd(LoanApplicationStatus status) =>
            status is LoanApplicationStatus.Submitted;

        public static bool IsFinal(LoanApplicationStatus status) =>
            status is LoanApplicationStatus.Approved
                or LoanApplicationStatus.Rejected
                or LoanApplicationStatus.SupervisorRejected
                or LoanApplicationStatus.Cancelled;

        /// <summary>
        /// Which actions a given state allows. Everything that changes
        /// an application goes through here, so a decision cannot be
        /// taken twice or taken on something already settled.
        /// </summary>
        public static bool CanAct(
            LoanApplicationAction action,
            LoanApplicationStatus status) => action switch
            {
                LoanApplicationAction.Submitted or
                LoanApplicationAction.Resubmitted =>
                    IsWithApplicant(status),

                LoanApplicationAction.ApprovedBySupervisor or
                LoanApplicationAction.RejectedBySupervisor or
                LoanApplicationAction.ReturnedBySupervisor =>
                    IsAwaitingSupervisor(status),

                LoanApplicationAction.Approved or
                LoanApplicationAction.Rejected or
                LoanApplicationAction.Returned =>
                    IsAwaitingMd(status),

                LoanApplicationAction.Cancelled =>
                    !IsFinal(status),

                _ => false
            };

        private static void EnsureCanAct(
            LoanApplicationAction action,
            LoanApplication application)
        {
            if (CanAct(action, application.Status))
                return;

            throw new InvalidOperationException(
                $"{application.ApplicationNumber} is " +
                $"{application.Status}, so the action {action} is not " +
                "allowed from here.");
        }

        public async Task SubmitAsync(
            LoanApplication application,
            int actedByEmployeeId,
            bool isResubmission)
        {
            EnsureCanAct(
                isResubmission
                    ? LoanApplicationAction.Resubmitted
                    : LoanApplicationAction.Submitted,
                application);

            var previous = application.Status;

            var supervisor = await _supervisors
                .SupervisorOfAsync(application.EmployeeId);

            application.Status = supervisor != null
                ? LoanApplicationStatus.SupervisorReview
                : LoanApplicationStatus.Submitted;

            application.SubmittedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                isResubmission
                    ? LoanApplicationAction.Resubmitted
                    : LoanApplicationAction.Submitted,
                previous,
                supervisor == null
                    ? null
                    : $"Sent to {supervisor.FirstName} " +
                      $"{supervisor.LastName} for supervisor approval.");

            if (supervisor != null)
            {
                _notifications.Notify(
                    supervisor.Id,
                    "Loan application awaiting your approval",
                    $"{application.ApplicationNumber} from " +
                    $"{Who(application)} is waiting for your approval " +
                    "as supervisor.",
                    $"/LoanApproval/Details/{application.Id}",
                    EmailKind.ActionNeeded);
            }
            else
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.MD,
                    "Loan application to sanction",
                    $"{application.ApplicationNumber} from " +
                    $"{Who(application)} for " +
                    LoanCurrencies.Format(
                        application.Currency,
                        application.AmountRequested) + ".",
                    $"/LoanApproval/Details/{application.Id}",
                    EmailKind.ActionNeeded);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// The supervisor signs off and the application carries on to
        /// the Managing Director, where it would have gone anyway.
        /// </summary>
        public async Task SupervisorApproveAsync(
            LoanApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                LoanApplicationAction.ApprovedBySupervisor,
                application);

            var previous = application.Status;

            application.Status = LoanApplicationStatus.Submitted;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.ApprovedBySupervisor,
                previous,
                comment);

            await _notifications.NotifyApproverAsync(
                ApprovalRole.MD,
                "Loan application to sanction",
                $"{application.ApplicationNumber} from " +
                $"{Who(application)} has been approved by their " +
                "supervisor and is waiting for you.",
                $"/LoanApproval/Details/{application.Id}",
                EmailKind.ActionNeeded);

            _notifications.Notify(
                application.EmployeeId,
                "Loan application approved by your supervisor",
                $"{application.ApplicationNumber} has gone on to the " +
                "Managing Director.",
                $"/MyLoans/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task SupervisorRejectAsync(
            LoanApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                LoanApplicationAction.RejectedBySupervisor,
                application);

            var previous = application.Status;

            application.Status = LoanApplicationStatus.SupervisorRejected;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.RejectedBySupervisor,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Loan application declined by your supervisor",
                $"{application.ApplicationNumber} was declined." +
                (string.IsNullOrWhiteSpace(comment) ? "" : $" {comment}"),
                $"/MyLoans/Details/{application.Id}",
                EmailKind.Declined);

            await _context.SaveChangesAsync();
        }

        public async Task SupervisorReturnAsync(
            LoanApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                LoanApplicationAction.ReturnedBySupervisor,
                application);

            var previous = application.Status;

            application.Status =
                LoanApplicationStatus.ReturnedToApplicant;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.ReturnedBySupervisor,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Loan application returned by your supervisor",
                $"{application.ApplicationNumber} needs a change " +
                "before it can go on." +
                (string.IsNullOrWhiteSpace(comment) ? "" : $" {comment}"),
                $"/MyLoans/Details/{application.Id}",
                EmailKind.Returned);

            await _context.SaveChangesAsync();
        }

        public async Task ApproveAsync(
            LoanApplication application,
            int actedByEmployeeId,
            decimal amountAuthorised,
            DateTime repaymentStartDate,
            string? comment)
        {
            EnsureCanAct(LoanApplicationAction.Approved, application);

            var previous = application.Status;

            application.Status = LoanApplicationStatus.Approved;
            application.AmountAuthorised = amountAuthorised;
            application.RepaymentStartDate = repaymentStartDate;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.Approved,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Loan sanctioned",
                $"{application.ApplicationNumber} was sanctioned for " +
                LoanCurrencies.Format(
                    application.Currency,
                    amountAuthorised) + ", repayable over " +
                $"{application.RepaymentMonths} months from " +
                $"{repaymentStartDate:d MMM yyyy}.",
                $"/MyLoans/Details/{application.Id}",
                EmailKind.Approved);

            await _context.SaveChangesAsync();
        }

        public async Task RejectAsync(
            LoanApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LoanApplicationAction.Rejected, application);

            var previous = application.Status;

            application.Status = LoanApplicationStatus.Rejected;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.Rejected,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Loan application declined",
                $"{application.ApplicationNumber} was not approved." +
                Because(comment),
                $"/MyLoans/Details/{application.Id}",
                EmailKind.Declined);

            await _context.SaveChangesAsync();
        }

        public async Task ReturnAsync(
            LoanApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LoanApplicationAction.Returned, application);

            var previous = application.Status;

            application.Status =
                LoanApplicationStatus.ReturnedToApplicant;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.Returned,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Loan application sent back",
                $"{application.ApplicationNumber} needs changes before " +
                "it can be looked at again." + Because(comment),
                $"/MyLoans/Edit/{application.Id}",
                EmailKind.Returned);

            await _context.SaveChangesAsync();
        }

        public async Task CancelAsync(
            LoanApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(LoanApplicationAction.Cancelled, application);

            var previous = application.Status;

            application.Status = LoanApplicationStatus.Cancelled;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                LoanApplicationAction.Cancelled,
                previous,
                comment);

            await _context.SaveChangesAsync();
        }

        public async Task<string> GenerateApplicationNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"LN-{year}-";

            var lastNumber = await _context.LoanApplications
                .Where(l => l.ApplicationNumber.StartsWith(prefix))
                .OrderByDescending(l => l.ApplicationNumber)
                .Select(l => l.ApplicationNumber)
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

        private static string Who(LoanApplication application) =>
            application.Employee == null
                ? "a member of staff"
                : $"{application.Employee.FirstName} " +
                  $"{application.Employee.LastName}";

        private static string Because(string? comment) =>
            string.IsNullOrWhiteSpace(comment)
                ? string.Empty
                : $" {comment}";

        private void AddHistory(
            LoanApplication application,
            int actedByEmployeeId,
            LoanApplicationAction action,
            LoanApplicationStatus previousStatus,
            string? comment)
        {
            _context.LoanApplicationHistories.Add(
                new LoanApplicationHistory
                {
                    LoanApplication = application,
                    ActionedByEmployeeId = actedByEmployeeId,
                    Action = action,
                    PreviousStatus = previousStatus,
                    NewStatus = application.Status,
                    Comment = string.IsNullOrWhiteSpace(comment)
                        ? null
                        : comment.Trim(),
                    ActionDate = DateTime.UtcNow
                });
        }
    }
}
