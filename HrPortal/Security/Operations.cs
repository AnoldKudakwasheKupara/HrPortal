using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace HrPortal.Security
{
    /// <summary>
    /// What someone might want to do with a record.
    ///
    /// These answer "whose record is this and may you touch it",
    /// not "is this a valid action right now". Whether a leave
    /// request may be edited at this moment is a workflow
    /// question and stays with the workflow.
    /// </summary>
    public static class Operations
    {
        /// <summary>Read it.</summary>
        public static readonly OperationAuthorizationRequirement View =
            new() { Name = nameof(View) };

        /// <summary>Change or remove it.</summary>
        public static readonly OperationAuthorizationRequirement Manage =
            new() { Name = nameof(Manage) };
    }
}
