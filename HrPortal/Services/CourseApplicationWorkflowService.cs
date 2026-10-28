using HrPortal.DbContexts;
using HrPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrPortal.Services
{
    /// <summary>
    /// Moves a course application between states. Human Resources
    /// reviews it and the Managing Consultant gives the final word,
    /// which is the portal's share of the form's three signatures;
    /// the manager's line is signed on the printed copy.
    /// </summary>
    public class CourseApplicationWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;

        private readonly SupervisorService _supervisors;

        public CourseApplicationWorkflowService(
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
            CourseApplicationStatus status) =>
            status is CourseApplicationStatus.SupervisorReview;

        public static bool IsWithApplicant(
            CourseApplicationStatus status) =>
            status is CourseApplicationStatus.Draft
                or CourseApplicationStatus.ReturnedToApplicant;

        public static bool IsAwaitingHr(CourseApplicationStatus status) =>
            status is CourseApplicationStatus.Submitted;

        public static bool IsAwaitingManagingConsultant(
            CourseApplicationStatus status) =>
            status is CourseApplicationStatus.HrApproved;

        public static bool IsFinal(CourseApplicationStatus status) =>
            status is CourseApplicationStatus.Approved
                or CourseApplicationStatus.Rejected
                or CourseApplicationStatus.HrRejected
                or CourseApplicationStatus.SupervisorRejected
                or CourseApplicationStatus.Cancelled;

        public static bool CanAct(
            CourseApplicationAction action,
            CourseApplicationStatus status) => action switch
            {
                CourseApplicationAction.Submitted or
                CourseApplicationAction.Resubmitted =>
                    IsWithApplicant(status),

                CourseApplicationAction.ApprovedBySupervisor or
                CourseApplicationAction.RejectedBySupervisor or
                CourseApplicationAction.ReturnedBySupervisor =>
                    IsAwaitingSupervisor(status),

                CourseApplicationAction.ApprovedByHr or
                CourseApplicationAction.RejectedByHr or
                CourseApplicationAction.ReturnedByHr =>
                    IsAwaitingHr(status),

                CourseApplicationAction.ApprovedByManagingConsultant or
                CourseApplicationAction.RejectedByManagingConsultant =>
                    IsAwaitingManagingConsultant(status),

                CourseApplicationAction.Cancelled =>
                    !IsFinal(status),

                _ => false
            };

        private static void EnsureCanAct(
            CourseApplicationAction action,
            CourseApplication application)
        {
            if (CanAct(action, application.Status))
                return;

            throw new InvalidOperationException(
                $"{application.ApplicationNumber} is " +
                $"{application.Status}, so the action {action} is not " +
                "allowed from here.");
        }

        public async Task SubmitAsync(
            CourseApplication application,
            int actedByEmployeeId,
            bool isResubmission)
        {
            EnsureCanAct(
                isResubmission
                    ? CourseApplicationAction.Resubmitted
                    : CourseApplicationAction.Submitted,
                application);

            var previous = application.Status;

            // The applicant's own supervisor sees it first, where
            // they have one. Nobody named means nothing to wait for.
            var supervisor = await _supervisors
                .SupervisorOfAsync(application.EmployeeId);

            application.Status = supervisor != null
                ? CourseApplicationStatus.SupervisorReview
                : CourseApplicationStatus.Submitted;

            application.SubmittedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                isResubmission
                    ? CourseApplicationAction.Resubmitted
                    : CourseApplicationAction.Submitted,
                previous,
                supervisor == null
                    ? null
                    : $"Sent to {supervisor.FirstName} " +
                      $"{supervisor.LastName} for supervisor approval.");

            if (supervisor != null)
            {
                _notifications.Notify(
                    supervisor.Id,
                    "Course application awaiting your approval",
                    $"{application.ApplicationNumber} from " +
                    $"{Who(application)} is waiting for your approval " +
                    "as supervisor.",
                    $"/CourseApproval/Details/{application.Id}");
            }
            else
            {
                await _notifications.NotifyApproverAsync(
                    ApprovalRole.HROfficer,
                    "Course application to review",
                    $"{application.ApplicationNumber} from " +
                    $"{Who(application)} for " +
                    $"{application.ProgramIntended}.",
                    $"/CourseApproval/Details/{application.Id}");
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// The supervisor signs off and the application carries on to
        /// Human Resources, where it would have gone anyway.
        /// </summary>
        public async Task SupervisorApproveAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.ApprovedBySupervisor,
                application);

            var previous = application.Status;

            application.Status = CourseApplicationStatus.Submitted;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.ApprovedBySupervisor,
                previous,
                comment);

            await _notifications.NotifyApproverAsync(
                ApprovalRole.HROfficer,
                "Course application to review",
                $"{application.ApplicationNumber} from " +
                $"{Who(application)} has been approved by their " +
                "supervisor and is waiting for you.",
                $"/CourseApproval/Details/{application.Id}");

            _notifications.Notify(
                application.EmployeeId,
                "Course application approved by your supervisor",
                $"{application.ApplicationNumber} has gone on to HR.",
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task SupervisorRejectAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.RejectedBySupervisor,
                application);

            var previous = application.Status;

            application.Status =
                CourseApplicationStatus.SupervisorRejected;

            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.RejectedBySupervisor,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Course application declined by your supervisor",
                $"{application.ApplicationNumber} was declined." +
                (string.IsNullOrWhiteSpace(comment) ? "" : $" {comment}"),
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task SupervisorReturnAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.ReturnedBySupervisor,
                application);

            var previous = application.Status;

            application.Status =
                CourseApplicationStatus.ReturnedToApplicant;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.ReturnedBySupervisor,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Course application returned by your supervisor",
                $"{application.ApplicationNumber} needs a change " +
                "before it can go on." +
                (string.IsNullOrWhiteSpace(comment) ? "" : $" {comment}"),
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task HrApproveAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.ApprovedByHr,
                application);

            var previous = application.Status;

            application.Status = CourseApplicationStatus.HrApproved;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.ApprovedByHr,
                previous,
                comment);

            await _notifications.NotifyApproverAsync(
                ApprovalRole.MD,
                "Course application to decide",
                $"{application.ApplicationNumber} from " +
                $"{Who(application)} for " +
                $"{application.ProgramIntended} has cleared HR.",
                $"/CourseApproval/Details/{application.Id}");

            _notifications.Notify(
                application.EmployeeId,
                "Course application cleared HR",
                $"{application.ApplicationNumber} is now with the " +
                "Managing Consultant.",
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task HrRejectAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.RejectedByHr,
                application);

            var previous = application.Status;

            application.Status = CourseApplicationStatus.HrRejected;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.RejectedByHr,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Course application declined",
                $"{application.ApplicationNumber} was not approved by " +
                "HR." + Because(comment),
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task HrReturnAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.ReturnedByHr,
                application);

            var previous = application.Status;

            application.Status =
                CourseApplicationStatus.ReturnedToApplicant;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.ReturnedByHr,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Course application sent back",
                $"{application.ApplicationNumber} needs changes " +
                "before it can go further." + Because(comment),
                $"/MyCourses/Edit/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task ApproveAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.ApprovedByManagingConsultant,
                application);

            var previous = application.Status;

            application.Status = CourseApplicationStatus.Approved;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.ApprovedByManagingConsultant,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Course application approved",
                $"{application.ApplicationNumber} for " +
                $"{application.ProgramIntended} was approved.",
                $"/MyCourses/Details/{application.Id}");

            // HR prints the approved form and takes it to Finance,
            // so they are told when one is ready.
            await _notifications.NotifyApproverAsync(
                ApprovalRole.HROfficer,
                "Course application approved",
                $"{application.ApplicationNumber} for " +
                $"{Who(application)} is approved and ready to print " +
                "for Finance.",
                "/CourseApproval/Approved");

            await _context.SaveChangesAsync();
        }

        public async Task RejectAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(
                CourseApplicationAction.RejectedByManagingConsultant,
                application);

            var previous = application.Status;

            application.Status = CourseApplicationStatus.Rejected;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.RejectedByManagingConsultant,
                previous,
                comment);

            _notifications.Notify(
                application.EmployeeId,
                "Course application declined",
                $"{application.ApplicationNumber} was not approved." +
                Because(comment),
                $"/MyCourses/Details/{application.Id}");

            await _context.SaveChangesAsync();
        }

        public async Task CancelAsync(
            CourseApplication application,
            int actedByEmployeeId,
            string? comment)
        {
            EnsureCanAct(CourseApplicationAction.Cancelled, application);

            var previous = application.Status;

            application.Status = CourseApplicationStatus.Cancelled;
            application.CompletedAt = DateTime.UtcNow;

            AddHistory(
                application,
                actedByEmployeeId,
                CourseApplicationAction.Cancelled,
                previous,
                comment);

            await _context.SaveChangesAsync();
        }

        public async Task<string> GenerateApplicationNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"CA-{year}-";

            var lastNumber = await _context.CourseApplications
                .Where(c => c.ApplicationNumber.StartsWith(prefix))
                .OrderByDescending(c => c.ApplicationNumber)
                .Select(c => c.ApplicationNumber)
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

        private static string Who(CourseApplication application) =>
            application.Employee == null
                ? "a member of staff"
                : $"{application.Employee.FirstName} " +
                  $"{application.Employee.LastName}";

        private static string Because(string? comment) =>
            string.IsNullOrWhiteSpace(comment)
                ? string.Empty
                : $" {comment}";

        private void AddHistory(
            CourseApplication application,
            int actedByEmployeeId,
            CourseApplicationAction action,
            CourseApplicationStatus previousStatus,
            string? comment)
        {
            _context.CourseApplicationHistories.Add(
                new CourseApplicationHistory
                {
                    CourseApplication = application,
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
