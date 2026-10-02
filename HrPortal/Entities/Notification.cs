namespace HrPortal.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Link { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }


        public Employee Employee { get; set; } = null!;
    }
}