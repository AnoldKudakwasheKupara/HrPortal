using HrPortal.Entities;

namespace HrPortal.Services
{
    /// <summary>
    /// What to call a stage, and what colour to show it in.
    /// </summary>
    public readonly record struct StatusBadge(string Label, string Css);

    /// <summary>
    /// The one place a request's stage gets its name.
    ///
    /// These used to live in the badge partials, which was fine while
    /// a stage was only ever shown on a page. A printed queue needs
    /// the same words, and a second copy of them is how a request
    /// came to be "With HR" on one screen and with its supervisor in
    /// fact.
    ///
    /// Every status is named and there is no catch-all: a stage
    /// nobody has named here prints its own name rather than
    /// borrowing a label that would be wrong.
    /// </summary>
    public static class StatusNames
    {
        private const string Waiting = "badge bg-info";
        private const string Done = "badge bg-success";
        private const string Refused = "badge bg-danger";
        private const string Back = "badge bg-warning";
        private const string Quiet = "badge bg-secondary";

        public static StatusBadge Of(LeaveRequestStatus status) =>
            status switch
            {
                LeaveRequestStatus.Draft =>
                    new("Draft", Quiet),
                LeaveRequestStatus.SupervisorReview =>
                    new("With the supervisor", Waiting),
                LeaveRequestStatus.SupervisorRejected =>
                    new("Rejected by the supervisor", Refused),
                LeaveRequestStatus.Submitted or
                LeaveRequestStatus.HRReview =>
                    new("With HR", Waiting),
                LeaveRequestStatus.ReturnedToEmployee =>
                    new("Returned", Back),
                LeaveRequestStatus.HRApproved or
                LeaveRequestStatus.MDReview =>
                    new("With the MD", Waiting),
                LeaveRequestStatus.HRRejected =>
                    new("Rejected by HR", Refused),
                LeaveRequestStatus.Approved =>
                    new("Approved", Done),
                LeaveRequestStatus.Rejected =>
                    new("Rejected", Refused),
                LeaveRequestStatus.Cancelled =>
                    new("Cancelled", Quiet),
                _ => new(status.ToString(), Quiet)
            };

        public static StatusBadge Of(LoanApplicationStatus status) =>
            status switch
            {
                LoanApplicationStatus.Draft =>
                    new("Draft", Quiet),
                LoanApplicationStatus.SupervisorReview =>
                    new("With the supervisor", Waiting),
                LoanApplicationStatus.SupervisorRejected =>
                    new("Declined by the supervisor", Refused),
                LoanApplicationStatus.Submitted =>
                    new("With the MD", Waiting),
                LoanApplicationStatus.ReturnedToApplicant =>
                    new("Returned", Back),
                LoanApplicationStatus.Approved =>
                    new("Sanctioned", Done),
                LoanApplicationStatus.Rejected =>
                    new("Declined", Refused),
                LoanApplicationStatus.Cancelled =>
                    new("Cancelled", Quiet),
                _ => new(status.ToString(), Quiet)
            };

        public static StatusBadge Of(CourseApplicationStatus status) =>
            status switch
            {
                CourseApplicationStatus.Draft =>
                    new("Draft", Quiet),
                CourseApplicationStatus.SupervisorReview =>
                    new("With the supervisor", Waiting),
                CourseApplicationStatus.SupervisorRejected =>
                    new("Declined by the supervisor", Refused),
                CourseApplicationStatus.Submitted =>
                    new("With HR", Waiting),
                CourseApplicationStatus.ReturnedToApplicant =>
                    new("Returned", Back),
                CourseApplicationStatus.HrApproved =>
                    new("With the Managing Consultant", Waiting),
                CourseApplicationStatus.HrRejected =>
                    new("Declined by HR", Refused),
                CourseApplicationStatus.Approved =>
                    new("Approved", Done),
                CourseApplicationStatus.Rejected =>
                    new("Declined", Refused),
                CourseApplicationStatus.Cancelled =>
                    new("Cancelled", Quiet),
                _ => new(status.ToString(), Quiet)
            };
    }
}
