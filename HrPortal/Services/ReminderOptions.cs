namespace HrPortal.Services
{
    public class ReminderOptions
    {
        public const string Section = "Reminders";

        public bool Enabled { get; set; } = true;

        public int RunAtHour { get; set; } = 7;

        public int ExpiryWindowDays { get; set; } = 30;

        public int RepeatEveryDays { get; set; } = 7;

        public int StaleApprovalDays { get; set; } = 3;
    }
}
