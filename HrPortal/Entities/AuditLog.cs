namespace HrPortal.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }

        public int? EmployeeId { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public string? EntityId { get; set; }

        public string? Description { get; set; }

        public DateTime Timestamp { get; set; }

        public string? IpAddress { get; set; }


        public Employee? Employee { get; set; }
    }
}