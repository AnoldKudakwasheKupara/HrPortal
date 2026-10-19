namespace HrPortal.Entities
{
    public class ReminderLog
    {
        public int Id { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string ReferenceKey { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public DateTime LastSentOn { get; set; }

        public Employee Employee { get; set; } = null!;
    }
}
