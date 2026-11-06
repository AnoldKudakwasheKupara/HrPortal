using HrPortal.Entities;

namespace HrPortal.Services.FundReporting
{
    /// <summary>A calendar quarter, and the months and days in it.</summary>
    public sealed record FundQuarter(int Year, int Number)
    {
        public DateTime Start => new(Year, (Number - 1) * 3 + 1, 1);

        public DateTime End => Start.AddMonths(3).AddDays(-1);

        public int Days => (End - Start).Days + 1;

        /// <summary>The month whose closing values open this quarter.</summary>
        public DateTime OpeningMonth => Start.AddMonths(-1);

        public DateTime LastMonth => Start.AddMonths(2);

        public IEnumerable<DateTime> Months => Enumerable.Range(0, 3).Select(i => Start.AddMonths(i));

        public FundQuarter Previous => Number == 1 ? new(Year - 1, 4) : new(Year, Number - 1);

        public string Label => $"Q{Number} {Year}";

        public static FundQuarter Containing(DateTime month) => new(month.Year, (month.Month - 1) / 3 + 1);

        /// <summary>The quarters ending with this one, newest last.</summary>
        public IReadOnlyList<FundQuarter> Trailing(int count)
        {
            var quarters = new List<FundQuarter> { this };

            while (quarters.Count < count)
                quarters.Insert(0, quarters[0].Previous);

            return quarters;
        }
    }

    /// <summary>
    /// Everything read for a fund, in memory. Every later step works from
    /// this and never from the statements themselves.
    /// </summary>
    public sealed class FundDataSet
    {
        public IReadOnlyList<FundAccount> Accounts { get; init; } = [];

        public IReadOnlyList<FundPosition> Positions { get; init; } = [];

        public IReadOnlyList<FundTransaction> Transactions { get; init; } = [];

        public IReadOnlyList<FundStatementBalance> Balances { get; init; } = [];

        public IReadOnlyDictionary<string, FundSecurity> Securities { get; init; } = new Dictionary<string, FundSecurity>();

        public IReadOnlyDictionary<DateTime, decimal> Rates { get; init; } = new Dictionary<DateTime, decimal>();

        /// <summary>
        /// In USD at the month's official rate, or empty if there is no rate.
        /// A rate is never assumed. The guide allows ZAR holdings but gives no
        /// ZAR rate, so a ZAR amount has none either.
        /// </summary>
        public decimal? ToUsd(decimal amount, string currency, DateTime month)
        {
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
                return amount;

            if (string.Equals(currency, "ZWG", StringComparison.OrdinalIgnoreCase) &&
                Rates.TryGetValue(month, out var rate) && rate > 0)
                return amount / rate;

            return null;
        }

        /// <summary>USD value of a holding. Only for months that have passed reconciliation.</summary>
        public decimal Usd(FundPosition position) =>
            ToUsd(position.MarketValue, position.Currency, position.Month) ?? 0;

        public decimal Value(IEnumerable<int> accountIds, DateTime month)
        {
            var ids = accountIds.ToHashSet();

            return Positions
                .Where(p => p.Month == month && ids.Contains(p.FundAccountId))
                .Sum(Usd);
        }

        /// <summary>
        /// External Flows and Transfers. A Transfer is external for an
        /// account; between two accounts of a group the two sides cancel.
        /// </summary>
        public static bool IsFlow(FundTransaction t) =>
            t.Treatment is FundReturnTreatment.ExternalFlow or FundReturnTreatment.Transfer;

        public static decimal Signed(FundTransaction t) =>
            t.CashIn ? Math.Abs(t.NetAmount) : -Math.Abs(t.NetAmount);

        /// <summary>A flow in USD at the month-end rate of the month it happened in.</summary>
        public decimal FlowUsd(FundTransaction t) =>
            ToUsd(Signed(t), t.Currency, t.Month) ?? 0;

        public IEnumerable<FundTransaction> Flows(IEnumerable<int> accountIds, DateTime from, DateTime toMonth)
        {
            var ids = accountIds.ToHashSet();

            return Transactions.Where(t =>
                IsFlow(t) && ids.Contains(t.FundAccountId) && t.Month >= from && t.Month <= toMonth);
        }

        public bool IsClosed(FundAccount account, DateTime month) =>
            account.ClosedFromMonth is DateTime closed && closed <= month;

        public bool HasHoldings(int accountId, DateTime month) =>
            Positions.Any(p => p.FundAccountId == accountId && p.Month == month);
    }
}
