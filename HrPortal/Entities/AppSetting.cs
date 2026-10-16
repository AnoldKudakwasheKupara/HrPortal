namespace HrPortal.Entities
{
    /// <summary>
    /// One setting, stored as the configuration key it overrides, such
    /// as "Requisitions:MdApprovalThreshold". Keeping the key in
    /// configuration form means the value arrives through the same
    /// pipeline as appsettings.json and nothing downstream has to know
    /// it came from the database.
    /// </summary>
    public class AppSetting
    {
        public int Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }
    }
}
