namespace HrPortal.Entities
{
    public class AssetMaintenance
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public MaintenanceType Type { get; set; }

        public MaintenanceStatus Status { get; set; }
            = MaintenanceStatus.Scheduled;

        public DateTime ScheduledFor { get; set; }

        public DateTime? CompletedOn { get; set; }

        public int? VendorId { get; set; }

        public string? PerformedBy { get; set; }

        public decimal? Cost { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public Asset Asset { get; set; } = null!;

        public Vendor? Vendor { get; set; }
    }
}
