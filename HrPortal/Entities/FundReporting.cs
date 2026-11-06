namespace HrPortal.Entities
{
    /// <summary>Every holding belongs to exactly one of these. The codes are in FundAssetClasses.</summary>
    public enum FundAssetClass
    {
        Cash,
        CashNostro,
        MoneyMarket,
        Bond,
        EquityZse,
        EquityVfex,
        PropertyZse,
        PropertyVfex,
        UnlistedProperty,
        UnlistedEquity,
        Offshore,
        Commodities
    }

    /// <summary>The groups benchmarks are worked out by, the same for every client.</summary>
    public enum FundBenchmarkGroup
    {
        Cash,
        MoneyMarket,
        Bonds,
        ListedEquities,
        Vfex,
        Alternatives
    }

    /// <summary>
    /// Whether a cash movement counts as performance. Review means nobody
    /// has decided yet, and blocks the quarter until an analyst does.
    /// </summary>
    public enum FundReturnTreatment
    {
        Review,
        ExternalFlow,
        Transfer,
        Income,
        Fee,
        Internal
    }

    /// <summary>A pension fund the company reports on. One report per fund per quarter.</summary>
    public class PensionFund
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public ICollection<FundAccount> Accounts { get; set; } = new List<FundAccount>();

        public ICollection<FundPolicyLine> PolicyLines { get; set; } = new List<FundPolicyLine>();
    }

    /// <summary>One account at one asset manager: the smallest unit valued.</summary>
    public class FundAccount
    {
        public int Id { get; set; }

        public int PensionFundId { get; set; }

        public PensionFund? Fund { get; set; }

        public string Manager { get; set; } = string.Empty;

        public string AccountNumber { get; set; } = string.Empty;

        public string? SubFund { get; set; }

        /// <summary>
        /// The first month the account is closed. Set by an analyst, so
        /// that an account with no holdings is a decision and not a
        /// statement nobody noticed was missing.
        /// </summary>
        public DateTime? ClosedFromMonth { get; set; }
    }

    /// <summary>
    /// One security, across every fund and manager. The system proposes
    /// its class; an analyst confirms it, and after that only an analyst
    /// changes it.
    /// </summary>
    public class FundSecurity
    {
        public int Id { get; set; }

        public string SecurityCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public FundAssetClass? AssetClass { get; set; }

        public bool Prescribed { get; set; }

        public bool LargeCap { get; set; }

        public bool Confirmed { get; set; }

        public string? ConfirmedByName { get; set; }

        public DateTime? ConfirmedAt { get; set; }
    }

    /// <summary>One holding in one account at one month-end.</summary>
    public class FundPosition
    {
        public int Id { get; set; }

        public int FundAccountId { get; set; }

        public FundAccount? Account { get; set; }

        /// <summary>The first day of the month the statement is for.</summary>
        public DateTime Month { get; set; }

        public string SecurityCode { get; set; } = string.Empty;

        public string SecurityName { get; set; } = string.Empty;

        public string Currency { get; set; } = "USD";

        public decimal? Units { get; set; }

        public decimal? BookCost { get; set; }

        /// <summary>In the holding's own currency, including accrued interest.</summary>
        public decimal MarketValue { get; set; }

        public decimal? AccruedInterest { get; set; }

        public decimal? RatePercent { get; set; }

        public DateTime? ValueDate { get; set; }

        public DateTime? MaturityDate { get; set; }

        public string? SourceFile { get; set; }

        public string? SourceSection { get; set; }

        public string? Confidence { get; set; }
    }

    /// <summary>One movement of cash in one account.</summary>
    public class FundTransaction
    {
        public int Id { get; set; }

        public int FundAccountId { get; set; }

        public FundAccount? Account { get; set; }

        public DateTime Month { get; set; }

        /// <summary>Empty when the statement gives none; the flow then counts as mid-period.</summary>
        public DateTime? TxnDate { get; set; }

        public string Currency { get; set; } = "USD";

        public string? SecurityCode { get; set; }

        public string? SecurityName { get; set; }

        public string TxnType { get; set; } = string.Empty;

        public decimal? Units { get; set; }

        public decimal? Price { get; set; }

        public decimal? GrossAmount { get; set; }

        public decimal? Fees { get; set; }

        public decimal NetAmount { get; set; }

        public bool CashIn { get; set; }

        public FundReturnTreatment Treatment { get; set; }

        /// <summary>Credit minus debit as printed. Used only to reconcile cash.</summary>
        public decimal LedgerDelta { get; set; }

        public string? SourceFile { get; set; }

        /// <summary>The wording on the statement, which the manager's rules go by.</summary>
        public string SourceLabel { get; set; } = string.Empty;

        public string? Comment { get; set; }
    }

    /// <summary>What a statement itself says its totals are, to check the rows against.</summary>
    public class FundStatementBalance
    {
        public int Id { get; set; }

        public int FundAccountId { get; set; }

        public FundAccount? Account { get; set; }

        public DateTime Month { get; set; }

        public string Currency { get; set; } = "USD";

        public decimal? PrintedTotal { get; set; }

        public decimal? OpeningCash { get; set; }

        public decimal? ClosingCash { get; set; }

        public string? SourceFile { get; set; }
    }

    /// <summary>
    /// An analyst's ruling on what a statement label means. It belongs to the
    /// manager, so it applies to all that manager's clients.
    /// </summary>
    public class FundTransactionRule
    {
        public int Id { get; set; }

        public string Manager { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public FundReturnTreatment Treatment { get; set; }

        public string? DecidedByName { get; set; }

        public DateTime DecidedAt { get; set; }
    }

    /// <summary>The official ZWG per 1 USD at a month-end.</summary>
    public class FundFxRate
    {
        public int Id { get; set; }

        public DateTime Month { get; set; }

        public decimal ZwgPerUsd { get; set; }
    }

    /// <summary>
    /// One asset class in a fund's Investment Policy Statement. All are
    /// decimals: 0.05 is 5%.
    /// </summary>
    public class FundPolicyLine
    {
        public int Id { get; set; }

        public int PensionFundId { get; set; }

        public PensionFund? Fund { get; set; }

        public FundAssetClass AssetClass { get; set; }

        public decimal Target { get; set; }

        public decimal CorridorLow { get; set; }

        public decimal CorridorHigh { get; set; }

        public decimal? IpecLimit { get; set; }
    }

    /// <summary>The market returns for one quarter. All are decimals.</summary>
    public class FundMarketQuarter
    {
        public int Id { get; set; }

        public int Year { get; set; }

        public int Quarter { get; set; }

        public decimal Cash { get; set; }

        public decimal MoneyMarket { get; set; }

        public decimal Bonds { get; set; }

        public decimal ZseAllShareUsd { get; set; }

        public decimal VfexAllShare { get; set; }

        public decimal UsdInflation { get; set; }
    }

    /// <summary>A record that a file was loaded, for the audit trail.</summary>
    public class FundStatementImport
    {
        public int Id { get; set; }

        public int PensionFundId { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public int RowCount { get; set; }

        public DateTime UploadedAt { get; set; }

        public string? UploadedByName { get; set; }
    }
}
