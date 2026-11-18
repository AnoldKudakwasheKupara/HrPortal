using HrPortal.Services;
using Microsoft.Extensions.Configuration;

namespace HrPortal.Tests.Companies
{
    public class PortalSwitchesTests
    {
        private static IConfiguration Settings(params (string Key, string Value)[] values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
                .Build();

        [Fact]
        public void A_portal_runs_its_jobs_and_has_its_vault_unless_told_otherwise()
        {
            var settings = Settings();

            Assert.True(PortalSwitches.RunsBackgroundJobs(settings));
            Assert.True(PortalSwitches.HasVault(settings));
        }

        [Fact]
        public void A_second_copy_on_the_same_database_can_leave_both_to_the_first()
        {
            var settings = Settings(
                (PortalSwitches.BackgroundJobsSetting, "false"),
                (PortalSwitches.VaultSetting, "false"));

            Assert.False(PortalSwitches.RunsBackgroundJobs(settings));
            Assert.False(PortalSwitches.HasVault(settings));
        }

        [Fact]
        public void The_blueprint_gives_mmc_its_own_service_on_the_same_database()
        {
            var blueprint = File.ReadAllText(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "render.yaml")));

            var mmc = blueprint[blueprint.IndexOf("name: mmcportal", StringComparison.Ordinal)..];
            mmc = mmc[..mmc.IndexOf("databases:", StringComparison.Ordinal)];

            Assert.Contains("name: hrportal-db", mmc);
            Assert.Contains("key: Brand__Key", mmc);
            Assert.Contains("value: mmc", mmc);
            Assert.Contains("key: BackgroundJobs__Enabled", mmc);
            Assert.Contains("key: Vault__Enabled", mmc);
        }
    }
}
