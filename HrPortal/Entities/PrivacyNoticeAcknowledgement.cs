using HrPortal.Data;

namespace HrPortal.Entities
{
    /// <summary>
    /// Somebody confirming they have read a version of the privacy
    /// notice. A new version of the notice is a new row, so the
    /// record shows who read which wording and when.
    /// </summary>
    public class PrivacyNoticeAcknowledgement
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Version { get; set; } = string.Empty;

        public DateTime AcknowledgedAt { get; set; }

        public string? IpAddress { get; set; }

        public ApplicationUser User { get; set; } = null!;
    }
}
