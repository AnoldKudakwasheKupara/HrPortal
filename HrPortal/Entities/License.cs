namespace HrPortal.Entities
{
    public class License
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public LicenseType Type { get; set; }

        public LicenseStatus Status { get; set; } = LicenseStatus.Active;

        /// <summary>
        /// Licence key, policy number or certificate reference.
        /// </summary>
        public string? Reference { get; set; }

        public int? VendorId { get; set; }

        /// <summary>
        /// Whoever holds the seat, when the licence belongs to a
        /// person rather than the business as a whole.
        /// </summary>
        public int? AssignedToEmployeeId { get; set; }

        public int? Seats { get; set; }

        public DateTime? StartDate { get; set; }

        /// <summary>The date it must be renewed by.</summary>
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
