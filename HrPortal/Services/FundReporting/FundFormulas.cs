namespace HrPortal.Services.FundReporting
{
    /// <summary>A flow of money into (+) or out of (-) a portfolio.</summary>
    /// <param name="Day">Days from the start of the period to the end of the flow date; empty if the date is unknown.</param>
    /// <param name="Amount">The amount, positive for money in.</param>
    public readonly record struct FundFlow(int? Day, decimal Amount);

    /// <summary>One asset class's line in an attribution.</summary>
    public readonly record struct AttributionInput(decimal PortfolioWeight, decimal BenchmarkWeight, decimal PortfolioReturn, decimal BenchmarkReturn);

    public readonly record struct AttributionEffect(decimal Allocation, decimal Selection, decimal Interaction);

    /// <summary>
    /// The formulas of section 6 of the reporting guide, with nothing else in
    /// them. Returns are decimals: 0.05 is 5%. Nothing is rounded here.
    /// </summary>
    public static class FundFormulas
    {
        /// <summary>6.1: only valid when no money came in or went out.</summary>
        public static decimal? HoldingPeriodReturn(decimal begin, decimal end) =>
            begin == 0 ? null : (end - begin) / begin;

        /// <summary>
        /// 6.2: Modified Dietz. A flow with no known date counts as mid-period.
        /// Empty when there was nothing invested to earn a return on.
        /// </summary>
        public static decimal? ModifiedDietz(decimal begin, decimal end, int days, IEnumerable<FundFlow> flows)
        {
            var total = 0m;
            var weighted = 0m;

            foreach (var flow in flows)
            {
                total += flow.Amount;

                var weight = flow.Day is int day ? (decimal)(days - day) / days : 0.5m;

                weighted += weight * flow.Amount;
            }

            var denominator = begin + weighted;

            return denominator <= 0 ? null : (end - begin - total) / denominator;
        }

        /// <summary>6.3, as the guide gives it.</summary>
        public static decimal? Contribution(decimal? managerReturn, decimal managerBegin, decimal fundBegin) =>
            managerReturn == null || fundBegin == 0 ? null : managerReturn * managerBegin / fundBegin;

        /// <summary>6.4: returns compound. They are never added.</summary>
        public static decimal ChainLink(IEnumerable<decimal> returns) =>
            returns.Aggregate(1m, (total, r) => total * (1 + r)) - 1;

        /// <summary>6.5: only for periods longer than a year.</summary>
        public static decimal Annualise(decimal totalReturn, double years) =>
            (decimal)(Math.Pow((double)(1 + totalReturn), 1 / years) - 1);

        /// <summary>6.7: each weight times its benchmark return.</summary>
        public static decimal Composite(IEnumerable<(decimal Weight, decimal Return)> lines) =>
            lines.Sum(l => l.Weight * l.Return);

        /// <summary>6.8: Brinson-Fachler, for one class given the total benchmark return.</summary>
        public static AttributionEffect Attribute(AttributionInput line, decimal totalBenchmark) =>
            new(
                (line.PortfolioWeight - line.BenchmarkWeight) * (line.BenchmarkReturn - totalBenchmark),
                line.BenchmarkWeight * (line.PortfolioReturn - line.BenchmarkReturn),
                (line.PortfolioWeight - line.BenchmarkWeight) * (line.PortfolioReturn - line.BenchmarkReturn));

        /// <summary>6.9: the rough monthly return the reconciliation checks against its limit.</summary>
        public static decimal? ImpliedReturn(decimal begin, decimal end, decimal flows)
        {
            var denominator = begin + 0.5m * flows;

            return denominator == 0 ? null : (end - begin - flows) / denominator;
        }

        /// <summary>6.10: how far prescribed assets fall short of the minimum.</summary>
        public static decimal PrescribedShortfall(decimal prescribed, decimal fundValue, decimal minimum) =>
            Math.Max(0, minimum * fundValue - prescribed);

        /// <summary>6.11: where a class's weight sits against the fund's policy.</summary>
        public static string PolicyPosition(decimal weight, decimal target, decimal low, decimal high, decimal? ipecLimit)
        {
            if (ipecLimit is decimal limit && weight > limit) return "Above IPEC limit";
            if (weight > high) return "Above corridor";
            if (weight < low) return "Below corridor";
            if (weight > target) return "Above target";
            if (weight < target) return "Below target";

            return "On target";
        }

        /// <summary>6.14: how far apart managers price the same security.</summary>
        public static decimal? PriceDispersion(IReadOnlyCollection<decimal> prices)
        {
            if (prices.Count < 2 || prices.Min() <= 0)
                return null;

            return (prices.Max() - prices.Min()) / prices.Min();
        }
    }
}
