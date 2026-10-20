namespace HrPortal.Services.Reminders
{
    public interface IReminderRule
    {
        string Kind { get; }

        Task<int> RunAsync(ReminderRunContext context);
    }

    public static class ReminderKinds
    {
        public const string LicenceExpiry = "Licence expiry";
        public const string DocumentExpiry = "Document expiry";
        public const string ContractEnding = "Contract ending";
        public const string LeaveWaiting = "Leave awaiting a decision";
        public const string RequisitionWaiting = "Requisition awaiting a decision";
        public const string MaintenanceDue = "Maintenance due";
        public const string TrainingDue = "Training due";
        public const string CertificationExpiry = "Certification expiry";
        public const string PolicyReview = "Policy review due";
        public const string PolicyUnread = "Policy not acknowledged";
    }
}
