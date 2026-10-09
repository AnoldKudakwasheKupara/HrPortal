namespace HrPortal.Entities
{
    public class Vendor
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>What this vendor supplies us with.</summary>
        public string ProductOrService { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? ContactPerson { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? AlternatePhone { get; set; }

        public string? Website { get; set; }

        public string? Address { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
    }
}
