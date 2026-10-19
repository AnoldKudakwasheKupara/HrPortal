namespace HrPortal.Entities
{
    public class LicenseRenewal
    {
        public int Id { get; set; }

        public int LicenseId { get; set; }

        public DateTime RenewedOn { get; set; }

        public DateTime PreviousExpiry { get; set; }

        public DateTime NewExpiry { get; set; }

        public decimal? Cost { get; set; }

        public string? Notes { get; set; }

        public License License { get; set; } = null!;
    }
}
