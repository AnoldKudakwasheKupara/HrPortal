namespace HrPortal.Services
{
    public class LeaveValidationResult
    {
        public const string StartDateField = "StartDate";
        public const string EndDateField = "EndDate";
        public const string FormField = "";

        public decimal NumberOfDays { get; set; }

        public List<LeaveValidationError> Errors { get; } = new();

        public bool IsValid => Errors.Count == 0;

        public void Add(string field, string message)
        {
            Errors.Add(new LeaveValidationError(field, message));
        }
    }

    public record LeaveValidationError(string Field, string Message);
}
