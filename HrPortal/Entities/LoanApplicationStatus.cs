namespace HrPortal.Entities
{
    public enum LoanApplicationStatus
    {
        Draft = 0,

        Submitted = 1,

        ReturnedToApplicant = 2,

        Approved = 3,

        Rejected = 4,

        Cancelled = 5,

        /// <summary>
        /// With the applicant's supervisor, before the Managing
        /// Director sees it. Appended, so recorded history keeps
        /// meaning what it said.
        /// </summary>
        SupervisorReview = 6,

        SupervisorRejected = 7
    }
}
