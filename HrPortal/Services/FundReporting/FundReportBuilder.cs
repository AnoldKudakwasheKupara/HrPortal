using HrPortal.Entities;

namespace HrPortal.Services.FundReporting
{
    public sealed record FundAllocationLine(string Name, decimal Usd, decimal Zwg, decimal Weight, decimal LastQuarterUsd, decimal Change, decimal? ChangePercent);

    public sealed record FundManagerMovement(string Manager, decimal Opening, decimal NetContributions, decimal Gain, decimal Closing);

    public sealed record FundReturnLine(string Name, decimal Opening, decimal Closing, decimal? Return, decimal? Contribution);

    public sealed record FundPolicyPosition(string Name, decimal Weight, decimal Target, decimal Low, decimal High, decimal? IpecLimit, string Position);

    public sealed record FundExposure(string Code, string Name, decimal Usd, decimal Weight, decimal Limit, bool Flagged);

    public sealed record FundPriceWarning(string Code, string Name, decimal Dispersion, decimal Lowest, decimal Highest, string Currency);

    public sealed record FundPerformanceLine(string Period, decimal? FundReturn, decimal? Benchmark)
    {
        public decimal? Variance => FundReturn - Benchmark;
    }

    public sealed class FundScenario
    {
        public decimal Rate { get; init; }

        public decimal ActualValue { get; init; }

        public decimal ScenarioValue { get; init; }

        public decimal Loss => ActualValue - ScenarioValue;
    }

    /// <summary>The figures for one fund's quarter, ready to show.</summary>
    public sealed class FundQuarterReport
    {
        public FundQuarter Quarter { get; init; } = new(2000, 1);

        public decimal FundValue { get; init; }

        public decimal? RateAtEnd { get; init; }

        public IReadOnlyList<FundAllocationLine> Allocation { get; init; } = [];

        public IReadOnlyList<FundAllocationLine> PureUsd { get; init; } = [];

        public IReadOnlyList<FundManagerMovement> Movements { get; init; } = [];

        public IReadOnlyList<FundReturnLine> Managers { get; init; } = [];

        public IReadOnlyList<FundReturnLine> Accounts { get; init; } = [];

        public IReadOnlyList<FundReturnLine> SubFunds { get; init; } = [];

        public decimal? FundReturn { get; init; }

        /// <summary>
        /// What the contributions leave unexplained. The guide's formula does
        /// not sum exactly when there are flows, so it is shown and not hidden.
        /// </summary>
        public decimal? ContributionDifference { get; init; }

        public IReadOnlyList<FundPerformanceLine> Performance { get; init; } = [];

        public decimal? ExcessOverInflation { get; init; }

        public decimal PrescribedValue { get; init; }

        public decimal PrescribedShare { get; init; }

        public decimal PrescribedShortfall { get; init; }

        public IReadOnlyList<FundPolicyPosition> Policy { get; init; } = [];

        public IReadOnlyList<FundExposure> ConcentrationFlags { get; init; } = [];

        public IReadOnlyList<FundExposure> TopEquities { get; init; } = [];

        public IReadOnlyList<FundExposure> TopFixedIncome { get; init; } = [];

        public IReadOnlyList<FundPriceWarning> PriceWarnings { get; init; } = [];

        public FundScenario? Scenario { get; init; }
    }

    /// <summary>
    /// Steps 7 to 12 of the guide. Only ever given a quarter that has passed
    /// reconciliation: every value below assumes rates and classes are present.
    /// </summary>
    public static class FundReportBuilder
    {
        public static decimal? GroupReturn(FundDataSet data, IEnumerable<int> accountIds, FundQuarter quarter)
        {
            var ids = accountIds.ToList();

            var flows = data.Flows(ids, quarter.Start, quarter.LastMonth).Select(t => new FundFlow(
                t.TxnDate is DateTime date ? (date.Date - quarter.Start).Days + 1 : null,
                data.FlowUsd(t)));

            return FundFormulas.ModifiedDietz(
                data.Value(ids, quarter.OpeningMonth),
                data.Value(ids, quarter.LastMonth),
                quarter.Days,
                flows);
        }

        /// <summary>
        /// The fund's return for a quarter, or empty if that quarter has not
        /// passed reconciliation. An unproven quarter never feeds a longer period.
        /// </summary>
        public static decimal? ProvenReturn(FundDataSet data, FundQuarter quarter, FundReportingOptions options) =>
            FundReconciliation.IsReady(FundReconciliation.Run(data, quarter, options)) &&
            data.Value(data.Accounts.Select(a => a.Id), quarter.LastMonth) != 0
                ? GroupReturn(data, data.Accounts.Select(a => a.Id), quarter)
                : null;

        /// <summary>6.7, for a quarter with market returns and a policy to weight them by.</summary>
        public static decimal? Benchmark(
            IReadOnlyCollection<FundPolicyLine> policy,
            FundMarketQuarter? market,
            FundReportingOptions options)
        {
            if (market == null || policy.Count == 0)
                return null;

            return FundFormulas.Composite(policy.Select(line => (
                line.Target,
                FundAssetClasses.Group(line.AssetClass) switch
                {
                    FundBenchmarkGroup.Cash => market.Cash,
                    FundBenchmarkGroup.MoneyMarket => market.MoneyMarket,
                    FundBenchmarkGroup.Bonds => market.Bonds,
                    FundBenchmarkGroup.ListedEquities => market.ZseAllShareUsd,
                    FundBenchmarkGroup.Vfex => market.VfexAllShare,
                    _ => market.UsdInflation + options.AlternativeBenchmarkMargin
                })));
        }

        public static FundQuarterReport Build(
            FundDataSet data,
            FundQuarter quarter,
            IReadOnlyCollection<FundPolicyLine> policy,
            IReadOnlyCollection<FundMarketQuarter> markets,
            FundReportingOptions options,
            decimal? scenarioRate = null)
        {
            var allIds = data.Accounts.Select(a => a.Id).ToList();
            var last = quarter.LastMonth;

            var fundValue = data.Value(allIds, last);
            var fundOpening = data.Value(allIds, quarter.OpeningMonth);
            var holdings = data.Positions.Where(p => p.Month == last).ToList();
            var rate = data.Rates.TryGetValue(last, out var r) ? r : (decimal?)null;
            var prescribed = Prescribed(data, holdings);
            var equities = Exposures(data, holdings, fundValue, options, FundAssetClasses.IsListedEquity);

            var fundReturn = GroupReturn(data, allIds, quarter);

            var managers = data.Accounts
                .GroupBy(a => a.Manager, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => ReturnLine(data, g.Key, g.Select(a => a.Id).ToList(), quarter, fundOpening))
                .ToList();

            return new FundQuarterReport
            {
                Quarter = quarter,
                FundValue = fundValue,
                RateAtEnd = rate,
                Allocation = Allocation(data, quarter, fundValue, rate),
                PureUsd = PureUsd(data, quarter, fundValue, rate),
                Movements = Movements(data, quarter),
                Managers = managers,
                Accounts = data.Accounts
                    .OrderBy(a => a.Manager).ThenBy(a => a.AccountNumber)
                    .Select(a => ReturnLine(data, $"{a.Manager} {a.AccountNumber}", new[] { a.Id }, quarter, fundOpening))
                    .ToList(),
                SubFunds = data.Accounts
                    .Where(a => !string.IsNullOrWhiteSpace(a.SubFund))
                    .GroupBy(a => a.SubFund!, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key)
                    .Select(g => ReturnLine(data, g.Key, g.Select(a => a.Id).ToList(), quarter, fundOpening))
                    .ToList(),
                FundReturn = fundReturn,
                ContributionDifference = fundReturn - managers.Sum(m => m.Contribution ?? 0),
                Performance = Performance(data, quarter, fundReturn, policy, markets, options),
                ExcessOverInflation = fundReturn - Market(markets, quarter)?.UsdInflation,
                PrescribedValue = prescribed,
                PrescribedShare = fundValue == 0 ? 0 : prescribed / fundValue,
                PrescribedShortfall = FundFormulas.PrescribedShortfall(prescribed, fundValue, options.PrescribedAssetsMinimum),
                Policy = Policy(data, holdings, fundValue, policy),
                PriceWarnings = PriceWarnings(data, holdings, options),
                Scenario = Scenario(data, holdings, fundValue, scenarioRate),
                ConcentrationFlags = equities.Where(e => e.Flagged).ToList(),
                TopEquities = equities.Take(5).ToList(),
                TopFixedIncome = Exposures(data, holdings, fundValue, options, FundAssetClasses.IsFixedIncome).Take(5).ToList()
            };
        }

        private static FundReturnLine ReturnLine(FundDataSet data, string name, IReadOnlyList<int> ids, FundQuarter quarter, decimal fundOpening)
        {
            var begin = data.Value(ids, quarter.OpeningMonth);
            var result = GroupReturn(data, ids, quarter);

            return new(name, begin, data.Value(ids, quarter.LastMonth), result,
                FundFormulas.Contribution(result, begin, fundOpening));
        }

        private static List<FundAllocationLine> Allocation(FundDataSet data, FundQuarter quarter, decimal fundValue, decimal? rate)
        {
            FundAssetClass? ClassOf(FundPosition p) =>
                data.Securities.TryGetValue(p.SecurityCode, out var s) ? s.AssetClass : null;

            var lines = new List<FundAllocationLine>();

            foreach (var assetClass in FundAssetClasses.Each)
            {
                var now = data.Positions.Where(p => p.Month == quarter.LastMonth && ClassOf(p) == assetClass).Sum(data.Usd);
                var before = data.Positions.Where(p => p.Month == quarter.OpeningMonth && ClassOf(p) == assetClass).Sum(data.Usd);

                if (now != 0 || before != 0)
                    lines.Add(Line(FundAssetClasses.Name(assetClass), now, before, fundValue, rate));
            }

            return lines;
        }

        private static List<FundAllocationLine> PureUsd(FundDataSet data, FundQuarter quarter, decimal fundValue, decimal? rate)
        {
            bool Usd(FundPosition p) => string.Equals(p.Currency, "USD", StringComparison.OrdinalIgnoreCase);

            var managerOf = data.Accounts.ToDictionary(a => a.Id, a => a.Manager);

            return data.Accounts
                .Select(a => a.Manager)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m)
                .Select(manager =>
                {
                    bool Theirs(FundPosition p) => string.Equals(managerOf[p.FundAccountId], manager, StringComparison.OrdinalIgnoreCase);

                    return Line(
                        manager,
                        data.Positions.Where(p => p.Month == quarter.LastMonth && Usd(p) && Theirs(p)).Sum(p => p.MarketValue),
                        data.Positions.Where(p => p.Month == quarter.OpeningMonth && Usd(p) && Theirs(p)).Sum(p => p.MarketValue),
                        fundValue,
                        rate);
                })
                .Where(l => l.Usd != 0 || l.LastQuarterUsd != 0)
                .ToList();
        }

        private static FundAllocationLine Line(string name, decimal now, decimal before, decimal fundValue, decimal? rate) =>
            new(name, now, now * (rate ?? 0), fundValue == 0 ? 0 : now / fundValue, before, now - before,
                before > 0 ? (now - before) / Math.Abs(before) : null);

        private static List<FundManagerMovement> Movements(FundDataSet data, FundQuarter quarter) =>
            data.Accounts
                .GroupBy(a => a.Manager, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var ids = g.Select(a => a.Id).ToList();
                    var begin = data.Value(ids, quarter.OpeningMonth);
                    var end = data.Value(ids, quarter.LastMonth);
                    var net = data.Flows(ids, quarter.Start, quarter.LastMonth).Sum(data.FlowUsd);

                    return new FundManagerMovement(g.Key, begin, net, end - begin - net, end);
                })
                .ToList();

        private static FundMarketQuarter? Market(IEnumerable<FundMarketQuarter> markets, FundQuarter quarter) =>
            markets.FirstOrDefault(m => m.Year == quarter.Year && m.Quarter == quarter.Number);

        private static List<FundPerformanceLine> Performance(
            FundDataSet data,
            FundQuarter quarter,
            decimal? fundReturn,
            IReadOnlyCollection<FundPolicyLine> policy,
            IReadOnlyCollection<FundMarketQuarter> markets,
            FundReportingOptions options)
        {
            decimal? Linked(IReadOnlyList<FundQuarter> quarters, Func<FundQuarter, decimal?> value)
            {
                var values = quarters.Select(value).ToList();

                return values.Any(v => v == null) ? null : FundFormulas.ChainLink(values.Select(v => v!.Value));
            }

            decimal? Fund(FundQuarter q) => q == quarter ? fundReturn : ProvenReturn(data, q, options);
            decimal? Bench(FundQuarter q) => Benchmark(policy, Market(markets, q), options);

            var threeYears = quarter.Trailing(12);

            return
            [
                new("Quarter", fundReturn, Bench(quarter)),
                new("Year to date",
                    Linked(quarter.Trailing(quarter.Number), Fund),
                    Linked(quarter.Trailing(quarter.Number), Bench)),
                new("1 year", Linked(quarter.Trailing(4), Fund), Linked(quarter.Trailing(4), Bench)),
                new("3 years (a year)",
                    Linked(threeYears, Fund) is decimal f ? FundFormulas.Annualise(f, 3) : null,
                    Linked(threeYears, Bench) is decimal b ? FundFormulas.Annualise(b, 3) : null)
            ];
        }

        private static decimal Prescribed(FundDataSet data, IEnumerable<FundPosition> holdings) =>
            holdings
                .Where(p => data.Securities.TryGetValue(p.SecurityCode, out var s) && s.Prescribed)
                .Sum(data.Usd);

        private static List<FundPolicyPosition> Policy(
            FundDataSet data,
            IReadOnlyCollection<FundPosition> holdings,
            decimal fundValue,
            IEnumerable<FundPolicyLine> policy) =>
            policy
                .OrderBy(l => l.AssetClass)
                .Select(line =>
                {
                    var value = holdings
                        .Where(p => data.Securities.TryGetValue(p.SecurityCode, out var s) && s.AssetClass == line.AssetClass)
                        .Sum(data.Usd);

                    var weight = fundValue == 0 ? 0 : value / fundValue;

                    return new FundPolicyPosition(
                        FundAssetClasses.Name(line.AssetClass), weight, line.Target, line.CorridorLow, line.CorridorHigh, line.IpecLimit,
                        FundFormulas.PolicyPosition(weight, line.Target, line.CorridorLow, line.CorridorHigh, line.IpecLimit));
                })
                .ToList();

        /// <summary>6.12: largest first, the same security added up across managers.</summary>
        private static List<FundExposure> Exposures(
            FundDataSet data,
            IEnumerable<FundPosition> holdings,
            decimal fundValue,
            FundReportingOptions options,
            Func<FundAssetClass, bool> include) =>
            holdings
                .Where(p => data.Securities.TryGetValue(p.SecurityCode, out var s) && s.AssetClass is FundAssetClass c && include(c))
                .GroupBy(p => p.SecurityCode)
                .Select(g =>
                {
                    var security = data.Securities[g.Key];
                    var usd = g.Sum(data.Usd);
                    var weight = fundValue == 0 ? 0 : usd / fundValue;
                    var limit = security.LargeCap ? options.LargeCapCounterLimit : options.SingleCounterLimit;

                    return new FundExposure(g.Key, security.Name, usd, weight, limit,
                        FundAssetClasses.IsListedEquity(security.AssetClass!.Value) && weight > limit);
                })
                .OrderByDescending(e => e.Usd)
                .ToList();

        private static List<FundPriceWarning> PriceWarnings(
            FundDataSet data,
            IEnumerable<FundPosition> holdings,
            FundReportingOptions options)
        {
            var managerOf = data.Accounts.ToDictionary(a => a.Id, a => a.Manager);
            var warnings = new List<FundPriceWarning>();

            foreach (var group in holdings
                .Where(p => p.Units is > 0)
                .GroupBy(p => (p.SecurityCode, Currency: p.Currency.ToUpperInvariant())))
            {
                if (group.Sum(data.Usd) <= options.PriceDispersionMinimumUsd)
                    continue;

                var prices = group
                    .GroupBy(p => managerOf[p.FundAccountId], StringComparer.OrdinalIgnoreCase)
                    .Select(m => m.Sum(p => p.MarketValue) / m.Sum(p => p.Units!.Value))
                    .ToList();

                if (FundFormulas.PriceDispersion(prices) is decimal dispersion && dispersion > options.PriceDispersionLimit)
                {
                    var name = data.Securities.TryGetValue(group.Key.SecurityCode, out var s) ? s.Name : group.Key.SecurityCode;

                    warnings.Add(new(group.Key.SecurityCode, name, dispersion, prices.Min(), prices.Max(), group.Key.Currency));
                }
            }

            return warnings;
        }

        /// <summary>6.13: only ZWG holdings move; USD holdings are unchanged.</summary>
        private static FundScenario? Scenario(FundDataSet data, IEnumerable<FundPosition> holdings, decimal fundValue, decimal? rate)
        {
            if (rate is not > 0)
                return null;

            var scenarioValue = holdings.Sum(p =>
                string.Equals(p.Currency, "ZWG", StringComparison.OrdinalIgnoreCase)
                    ? p.MarketValue / rate.Value
                    : data.Usd(p));

            return new FundScenario { Rate = rate.Value, ActualValue = fundValue, ScenarioValue = scenarioValue };
        }
    }
}
