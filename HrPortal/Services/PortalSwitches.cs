namespace HrPortal.Services
{
    /// <summary>
    /// What a running copy of the portal does besides serving pages.
    /// When a second copy shares the database, such as MMC Capital's own
    /// service, only one copy should send the daily reminders and add the
    /// monthly leave days, and only the copy whose disk holds the vault
    /// key can open the password vault. Both default to on.
    /// </summary>
    public static class PortalSwitches
    {
        public const string BackgroundJobsSetting = "BackgroundJobs:Enabled";

        public const string VaultSetting = "Vault:Enabled";

        public static bool RunsBackgroundJobs(IConfiguration configuration) =>
            configuration.GetValue(BackgroundJobsSetting, true);

        public static bool HasVault(IConfiguration configuration) =>
            configuration.GetValue(VaultSetting, true);
    }
}
