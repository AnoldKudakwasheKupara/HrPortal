using HrPortal.Services.FundReporting;

namespace HrPortal.Tests.FundReporting
{
    /// <summary>The worked examples in section 6 of the reporting guide, to the figures it prints.</summary>
    public class FundFormulasTests
    {
        private static readonly FundFlow[] GuideFlows = [new(31, 50_000m), new(61, -20_000m)];

        [Fact]
        public void Holding_period_return_matches_the_guide() =>
            Assert.Equal(0.04m, FundFormulas.HoldingPeriodReturn(100_000, 104_000));

        [Fact]
        public void Modified_dietz_matches_the_worked_example() =>
            Assert.Equal(0.0487m, Math.Round(FundFormulas.ModifiedDietz(1_000_000, 1_080_000, 92, GuideFlows)!.Value, 4));

        [Fact]
        public void Without_flow_dates_every_flow_counts_as_mid_period()
        {
            FundFlow[] flows = [new(null, 50_000m), new(null, -20_000m)];

            Assert.Equal(0.0493m, Math.Round(FundFormulas.ModifiedDietz(1_000_000, 1_080_000, 92, flows)!.Value, 4));
        }

        [Fact]
        public void A_new_mandate_starts_from_zero_and_its_first_funding_is_a_flow()
        {
            var result = FundFormulas.ModifiedDietz(0, 105_000, 92, [new(1, 100_000m)]);

            Assert.NotNull(result);
            Assert.True(result > 0.05m && result < 0.052m);
        }

        [Fact]
        public void With_nothing_invested_there_is_no_return() =>
            Assert.Null(FundFormulas.ModifiedDietz(0, 0, 92, []));

        [Fact]
        public void Chain_linking_matches_the_guide_and_is_not_addition() =>
            Assert.Equal(0.1447m, Math.Round(FundFormulas.ChainLink([0.05m, -0.02m, 0.08m, 0.03m]), 4));

        [Fact]
        public void Annualising_matches_the_guide() =>
            Assert.Equal(0.1447m, Math.Round(FundFormulas.Annualise(0.4998m, 3), 4));

        [Fact]
        public void Attribution_matches_the_worked_example_and_adds_up()
        {
            AttributionInput equities = new(0.60m, 0.50m, 0.10m, 0.08m);
            AttributionInput bonds = new(0.40m, 0.50m, 0.03m, 0.04m);

            var benchmark = FundFormulas.Composite([(0.50m, 0.08m), (0.50m, 0.04m)]);

            Assert.Equal(0.06m, benchmark);

            var e = FundFormulas.Attribute(equities, benchmark);
            var b = FundFormulas.Attribute(bonds, benchmark);

            Assert.Equal(new AttributionEffect(0.002m, 0.01m, 0.002m), e);
            Assert.Equal(new AttributionEffect(0.002m, -0.005m, 0.001m), b);

            Assert.Equal(
                0.072m - 0.06m,
                e.Allocation + e.Selection + e.Interaction + b.Allocation + b.Selection + b.Interaction);
        }

        [Fact]
        public void Prescribed_shortfall_matches_the_guide()
        {
            Assert.Equal(470_025m, Math.Round(FundFormulas.PrescribedShortfall(177_843m, 3_239_341m, 0.20m)));
            Assert.Equal(0m, FundFormulas.PrescribedShortfall(800_000m, 3_239_341m, 0.20m));
        }

        [Theory]
        [InlineData(0.30, "Above IPEC limit")]
        [InlineData(0.22, "Above corridor")]
        [InlineData(0.05, "Below corridor")]
        [InlineData(0.17, "Above target")]
        [InlineData(0.12, "Below target")]
        [InlineData(0.15, "On target")]
        public void Policy_position_labels(double weight, string expected) =>
            Assert.Equal(expected, FundFormulas.PolicyPosition((decimal)weight, 0.15m, 0.10m, 0.20m, 0.25m));

        [Fact]
        public void Price_dispersion_needs_two_managers()
        {
            Assert.Null(FundFormulas.PriceDispersion([1.00m]));
            Assert.Equal(0.05m, FundFormulas.PriceDispersion([1.00m, 1.05m]));
        }

        [Fact]
        public void A_month_of_ten_percent_is_implied_correctly() =>
            Assert.Equal(0.25m, FundFormulas.ImpliedReturn(100, 125, 0));
    }
}
