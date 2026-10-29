namespace HrPortal.Services.Notifications
{
    /// <summary>
    /// What an emailed notification is, which decides how it looks:
    /// the label at the top, its colour and what the button says.
    /// A notification raised without one stays in the bell.
    /// </summary>
    public enum EmailKind
    {
        ActionNeeded,
        Approved,
        Declined,
        Returned,
        TrainingScheduled,
        TrainerAssigned,
        ContentDue,
        ContentOverdue,
        SessionReminder,
        PasswordReset
    }
}
