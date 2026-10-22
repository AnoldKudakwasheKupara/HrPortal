namespace HrPortal.Entities
{
    /// <summary>
    /// The form asks whether an applicant has sat the programme
    /// before and, if so, whether they are going up a level or
    /// re-writing.
    /// </summary>
    public enum CourseSitting
    {
        /// <summary>Has not sat the programme before.</summary>
        FirstAttempt = 0,

        NextLevel = 1,

        Rewriting = 2
    }
}
