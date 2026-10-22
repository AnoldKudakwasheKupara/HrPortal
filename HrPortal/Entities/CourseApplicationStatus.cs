namespace HrPortal.Entities
{
    public enum CourseApplicationStatus
    {
        Draft = 0,

        /// <summary>With Human Resources.</summary>
        Submitted = 1,

        ReturnedToApplicant = 2,

        /// <summary>Cleared HR, with the Managing Consultant.</summary>
        HrApproved = 3,

        HrRejected = 4,

        Approved = 5,

        Rejected = 6,

        Cancelled = 7
    }
}
