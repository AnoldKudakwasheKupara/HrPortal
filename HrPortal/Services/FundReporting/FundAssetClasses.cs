using HrPortal.Entities;

namespace HrPortal.Services.FundReporting
{
    /// <summary>The statement codes and names of the asset classes, and how they group for benchmarks.</summary>
    public static class FundAssetClasses
    {
        private static readonly (FundAssetClass Class, string Code, string Name, FundBenchmarkGroup Group)[] All =
        {
            (FundAssetClass.Cash, "ASSET_CASH", "Cash (ZWG)", FundBenchmarkGroup.Cash),
            (FundAssetClass.CashNostro, "ASSET_FCA", "Cash nostro (USD)", FundBenchmarkGroup.Cash),
            (FundAssetClass.MoneyMarket, "ASSET_MM", "Money market", FundBenchmarkGroup.MoneyMarket),
            (FundAssetClass.Bond, "ASSET_BOND", "Bonds and debentures", FundBenchmarkGroup.Bonds),
            (FundAssetClass.EquityZse, "ASSET_EQ_ZSE", "Listed equity: ZSE", FundBenchmarkGroup.ListedEquities),
            (FundAssetClass.EquityVfex, "ASSET_EQ_VFEX", "Listed equity: VFEX", FundBenchmarkGroup.Vfex),
            (FundAssetClass.PropertyZse, "ASSET_PROP_ZSE", "Listed property: ZSE", FundBenchmarkGroup.ListedEquities),
            (FundAssetClass.PropertyVfex, "ASSET_PROP_VFEX", "Listed property: VFEX", FundBenchmarkGroup.Vfex),
            (FundAssetClass.UnlistedProperty, "ASSET_UPROP", "Unlisted property", FundBenchmarkGroup.Alternatives),
            (FundAssetClass.UnlistedEquity, "ASSET_UEQ", "Unlisted equity", FundBenchmarkGroup.Alternatives),
            (FundAssetClass.Offshore, "ASSET_EQ_OFF", "Offshore", FundBenchmarkGroup.Alternatives),
            (FundAssetClass.Commodities, "ASSET_COMM", "Commodities", FundBenchmarkGroup.Alternatives)
        };

        public static IEnumerable<FundAssetClass> Each => All.Select(a => a.Class);

        public static string Name(FundAssetClass value) => All.First(a => a.Class == value).Name;

        public static FundBenchmarkGroup Group(FundAssetClass value) => All.First(a => a.Class == value).Group;

        public static FundAssetClass? FromCode(string? code)
        {
            var match = All.FirstOrDefault(a => string.Equals(a.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

            return match.Code == null ? null : match.Class;
        }

        public static bool IsListedEquity(FundAssetClass value) =>
            value is FundAssetClass.EquityZse or FundAssetClass.EquityVfex or FundAssetClass.PropertyZse or FundAssetClass.PropertyVfex;

        public static bool IsFixedIncome(FundAssetClass value) =>
            value is FundAssetClass.MoneyMarket or FundAssetClass.Bond;
    }
}
