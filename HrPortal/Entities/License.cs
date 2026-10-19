namespace HrPortal.Entities
{
    public class License
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public LicenseType Type { get; set; }

        public LicenseStatus Status { get; set; } = LicenseStatus.Active;

        public string? Reference { get; set; }

        public int? VendorId { get; set; }

        public int? AssignedToEmployeeId { get; set; }

        public int? Seats { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public decimal? Cost { get; set; }

        public bool AutoRenews { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public Vendor? Vendor { get; set; }

        public Employee? AssignedToEmployee { get; set; }

        public ICollection<LicenseRenewal> Renewals { get; set; }
            = new List<LicenseRenewal>();
    }
}
