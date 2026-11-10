namespace HrPortal.Entities
{
    public enum CourseApplicationAction
    {
        Submitted,
        Resubmitted,
        ApprovedByHr,
        RejectedByHr,
        ReturnedByHr,
        ApprovedByManagingConsultant,
        RejectedByManagingConsultant,
        Cancelled,
        ApprovedBySupervisor,
        RejectedBySupervisor,
        ReturnedBySupervisor,

        /// <summary>
        /// Approved on paper before the portal, and recorded here
        /// afterwards by HR or IT on the employee's behalf.
        /// </summary>
        CapturedAsApproved
    }
}
