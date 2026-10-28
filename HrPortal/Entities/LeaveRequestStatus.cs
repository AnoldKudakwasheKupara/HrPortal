namespace HrPortal.Entities
{
    public enum LeaveRequestStatus
    {
        Draft = 0,

        Submitted = 1,

        HRReview = 2,

        ReturnedToEmployee = 3,

        HRApproved = 4,

        HRRejected = 5,

        MDReview = 6,

        Approved = 7,

        Rejected = 8,

        Cancelled = 9,

        /// <summary>
        /// With the employee's supervisor, before HR sees it.
        /// Appended rather than inserted: the column stores the
        /// member name, so these numbers are not what is written,
        /// but renumbering would still rewrite recorded history.
        /// </summary>
        SupervisorReview = 10,

        SupervisorRejected = 11
    }
}
