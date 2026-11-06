using System.ComponentModel.DataAnnotations;
using HrPortal.Entities;
using HrPortal.Services.FundReporting;

namespace HrPortal.ViewModels
{
    public class FundPageViewModel
    {
        public PensionFund Fund { get; set; } = new();

        public IReadOnlyList<FundQuarter> Quarters { get; set; } = [];

        public IReadOnlyList<FundStatementImport> Imports { get; set; } = [];
    }

    public class FundQuarterViewModel
    {
        public PensionFund Fund { get; set; } = new();

        public FundQuarter Quarter { get; set; } = new(2000, 1);

        public IReadOnlyList<FundFinding> Findings { get; set; } = [];

        /// <summary>Empty until every blocking check is clear.</summary>
        public FundQuarterReport? Report { get; set; }

        public decimal? ScenarioRate { get; set; }
    }

    public class FundReviewViewModel
    {
        public IReadOnlyList<FundLabelToDecide> Labels { get; set; } = [];

        public IReadOnlyList<FundTransactionRule> Rules { get; set; } = [];

        public IReadOnlyList<FundSecurity> Securities { get; set; } = [];
    }

    public class FundMarketDataViewModel
    {
        public IReadOnlyList<FundFxRate> Rates { get; set; } = [];

        public IReadOnlyList<FundMarketQuarter> Quarters { get; set; } = [];
    }

    /// <summary>One class of a fund's policy as typed, in percentages.</summary>
    public class FundPolicyInput
    {
        public FundAssetClass AssetClass { get; set; }

        [Range(0, 100)]
        public decimal? Target { get; set; }

        [Range(0, 100)]
        public decimal? Low { get; set; }

        [Range(0, 100)]
        public decimal? High { get; set; }

        [Range(0, 100)]
        public decimal? Ipec { get; set; }
    }

    /// <summary>A quarter's market returns as typed, in percentages.</summary>
    public class FundMarketQuarterInput
    {
        [Range(2000, 2100)]
        public int Year { get; set; }

        [Range(1, 4)]
        public int Quarter { get; set; }

        [Required]
        public decimal? Cash { get; set; }

        [Required]
        public decimal? MoneyMarket { get; set; }

        [Required]
        public decimal? Bonds { get; set; }

        [Required]
        public decimal? ZseAllShareUsd { get; set; }

        [Required]
        public decimal? VfexAllShare { get; set; }

        [Required]
        public decimal? UsdInflation { get; set; }
    }
}
