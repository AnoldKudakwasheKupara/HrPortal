namespace HrPortal.Services.FundReporting
{
    /// <summary>
    /// The tolerances and limits from section 8 of the reporting guide, kept
    /// out of the code. Override any of them under "FundReporting" in
    /// configuration. Percentages are decimals: 0.10 is 10%.
    /// </summary>
    public sealed class FundReportingOptions
    {
        public const string Section = "FundReporting";

        public decimal StatementTotalTolerance { get; set; } = 0.05m;

        public decimal CashReconciliationTolerance { get; set; } = 0.05m;

        public decimal CashContinuityTolerance { get; set; } = 1.00m;

        public decimal ValueBridgeLimit { get; set; } = 0.10m;

        public decimal PrescribedAssetsMinimum { get; set; } = 0.20m;

        public decimal SingleCounterLimit { get; set; } = 0.10m;

        public decimal LargeCapCounterLimit { get; set; } = 0.15m;

        public decimal PriceDispersionLimit { get; set; } = 0.02m;

        public decimal PriceDispersionMinimumUsd { get; set; } = 500m;

        /// <summary>Added to USD inflation for the quarter to benchmark alternative assets.</summary>
        public decimal AlternativeBenchmarkMargin { get; set; } = 0.02m;
    }
}
