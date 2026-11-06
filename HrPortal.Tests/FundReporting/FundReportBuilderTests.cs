using HrPortal.Entities;
using HrPortal.Services.FundReporting;

namespace HrPortal.Tests.FundReporting
{
    public class FundReportBuilderTests
    {
        private static readonly FundQuarter Q3 = new(2026, 3);
        private static readonly FundReportingOptions Options = new();

        private static readonly FundAccount A = new() { Id = 1, Manager = "Datvest", AccountNumber = "A1" };
        private static readonly FundAccount B = new() { Id = 2, Manager = "Datvest", AccountNumber = "A2" };
        private static readonly FundAccount C = new() { Id = 3, Manager = "Smartvest", AccountNumber = "C1" };

        private static readonly Dictionary<string, FundSecurity> Securities = new()
        {
            ["EQ"] = new() { SecurityCode = "EQ", Name = "Share", AssetClass = FundAssetClass.EquityVfex, Confirmed = true },
            ["BIG"] = new() { SecurityCode = "BIG", Name = "Big share", AssetClass = FundAssetClass.EquityVfex, Confirmed = true, LargeCap = true },
            ["PA"] = new() { SecurityCode = "PA", Name = "Prescribed bond", AssetClass = FundAssetClass.Bond, Confirmed = true, Prescribed = true }
        };

        private static FundPosition Holding(int account, DateTime month, decimal value, string code = "EQ", string currency = "USD", decimal? units = null) =>
            new() { FundAccountId = account, Month = month, SecurityCode = code, Currency = currency, MarketValue = value, Units = units };

        private static FundTransaction Flow(int account, DateTime date, decimal amount, bool cashIn, FundReturnTreatment treatment = FundReturnTreatment.ExternalFlow, string currency = "USD") =>
            new()
            {
                FundAccountId = account,
                Month = new DateTime(date.Year, date.Month, 1),
                TxnDate = date,
                NetAmount = amount,
                CashIn = cashIn,
                Treatment = treatment,
                Currency = currency,
                SourceLabel = "x"
            };

        private static FundDataSet Data(
            IEnumerable<FundPosition> positions,
            IEnumerable<FundTransaction>? transactions = null,
            IEnumerable<FundAccount>? accounts = null,
            Dictionary<DateTime, decimal>? rates = null) =>
            new()
            {
                Accounts = (accounts ?? [A]).ToList(),
                Positions = positions.ToList(),
                Transactions = (transactions ?? []).ToList(),
                Securities = Securities,
                Rates = rates ?? new()
            };

        private static FundQuarterReport Build(FundDataSet data, IReadOnlyCollection<FundPolicyLine>? policy = null, IReadOnlyCollection<FundMarketQuarter>? markets = null, decimal? scenario = null) =>
            FundReportBuilder.Build(data, Q3, policy ?? [], markets ?? [], Options, scenario);

        [Fact]
        public void The_quarter_return_uses_real_flow_dates_like_the_worked_example()
        {
            var data = Data(
                [Holding(1, Q3.OpeningMonth, 1_000_000), Holding(1, Q3.LastMonth, 1_080_000)],
                [Flow(1, new DateTime(2026, 7, 31), 50_000, true), Flow(1, new DateTime(2026, 8, 30), 20_000, false)]);

            Assert.Equal(0.0487m, Math.Round(FundReportBuilder.GroupReturn(data, [1], Q3)!.Value, 4));
        }

        [Fact]
        public void Income_and_fees_are_not_flows()
        {
            var data = Data(
                [Holding(1, Q3.OpeningMonth, 100_000), Holding(1, Q3.LastMonth, 104_000)],
                [
                    Flow(1, new DateTime(2026, 8, 1), 5_000, true, FundReturnTreatment.Income),
                    Flow(1, new DateTime(2026, 8, 1), 1_000, false, FundReturnTreatment.Fee),
                    Flow(1, new DateTime(2026, 8, 1), 9_000, false, FundReturnTreatment.Internal)
                ]);

            Assert.Equal(0.04m, FundReportBuilder.GroupReturn(data, [1], Q3));
        }

        [Fact]
        public void A_transfer_counts_for_each_account_and_cancels_for_the_fund()
        {
            var date = new DateTime(2026, 8, 15);

            var data = Data(
                [
                    Holding(1, Q3.OpeningMonth, 100_000), Holding(1, Q3.LastMonth, 80_000),
                    Holding(2, Q3.OpeningMonth, 100_000), Holding(2, Q3.LastMonth, 120_000)
                ],
                [Flow(1, date, 20_000, false, FundReturnTreatment.Transfer), Flow(2, date, 20_000, true, FundReturnTreatment.Transfer)],
                [A, B]);

            Assert.Equal(0m, FundReportBuilder.GroupReturn(data, [1], Q3));
            Assert.Equal(0m, FundReportBuilder.GroupReturn(data, [2], Q3));
            Assert.Equal(0m, FundReportBuilder.GroupReturn(data, [1, 2], Q3));
        }

        [Fact]
        public void A_group_return_is_not_the_average_of_its_accounts()
        {
            var data = Data(
                [
                    Holding(1, Q3.OpeningMonth, 900_000), Holding(1, Q3.LastMonth, 990_000),
                    Holding(2, Q3.OpeningMonth, 100_000), Holding(2, Q3.LastMonth, 100_000)
                ],
                accounts: [A, B]);

            Assert.Equal(0.09m, FundReportBuilder.GroupReturn(data, [1, 2], Q3));
        }

        [Fact]
        public void Zwg_values_convert_at_each_month_end_and_a_weaker_rate_shows_as_a_loss_in_usd()
        {
            var rates = new Dictionary<DateTime, decimal> { [Q3.OpeningMonth] = 25m, [Q3.LastMonth] = 50m };

            var data = Data(
                [Holding(1, Q3.OpeningMonth, 1_000, currency: "ZWG"), Holding(1, Q3.LastMonth, 1_000, currency: "ZWG")],
                rates: rates);

            Assert.Equal(-0.5m, FundReportBuilder.GroupReturn(data, [1], Q3));
        }

        [Fact]
        public void The_exchange_rate_scenario_matches_the_guide()
        {
            var rates = new Dictionary<DateTime, decimal> { [Q3.OpeningMonth] = 26.80m, [Q3.LastMonth] = 26.80m };

            var data = Data(
                [
                    Holding(1, Q3.OpeningMonth, 1_000_000, currency: "ZWG"),
                    Holding(1, Q3.LastMonth, 1_000_000, currency: "ZWG"),
                    Holding(1, Q3.LastMonth, 500, code: "BIG")
                ],
                rates: rates);

            var scenario = Build(data, scenario: 35m).Scenario!;

            Assert.Equal(8_742m, Math.Round(scenario.Loss));
        }

        [Fact]
        public void A_manager_bridge_explains_the_change_in_value()
        {
            var data = Data(
                [Holding(1, Q3.OpeningMonth, 100_000), Holding(1, Q3.LastMonth, 130_000)],
                [Flow(1, new DateTime(2026, 8, 1), 20_000, true)]);

            var movement = Assert.Single(Build(data).Movements);

            Assert.Equal(20_000m, movement.NetContributions);
            Assert.Equal(10_000m, movement.Gain);
        }

        [Fact]
        public void Change_percent_is_not_meaningful_from_nothing()
        {
            var line = Assert.Single(Build(Data([Holding(1, Q3.LastMonth, 100)])).Allocation);

            Assert.Null(line.ChangePercent);
        }

        [Fact]
        public void Prescribed_assets_and_the_shortfall_come_from_the_security_master()
        {
            var data = Data(
            [
                Holding(1, Q3.OpeningMonth, 100),
                Holding(1, Q3.LastMonth, 90),
                Holding(1, Q3.LastMonth, 10, code: "PA")
            ]);

            var report = Build(data);

            Assert.Equal(0.10m, report.PrescribedShare);
            Assert.Equal(10m, report.PrescribedShortfall);
        }

        [Fact]
        public void The_same_security_adds_up_across_managers_and_large_caps_get_the_higher_limit()
        {
            var data = Data(
                [
                    Holding(1, Q3.LastMonth, 6, code: "EQ"),
                    Holding(3, Q3.LastMonth, 6, code: "EQ"),
                    Holding(1, Q3.LastMonth, 12, code: "BIG"),
                    Holding(1, Q3.LastMonth, 76, code: "PA")
                ],
                accounts: [A, C]);

            var report = Build(data);

            var flagged = Assert.Single(report.ConcentrationFlags);

            Assert.Equal("EQ", flagged.Code);
            Assert.Equal(0.12m, flagged.Weight);
        }

        [Fact]
        public void Managers_pricing_a_security_far_apart_are_flagged()
        {
            var data = Data(
                [
                    Holding(1, Q3.LastMonth, 1_000, units: 1_000),
                    Holding(3, Q3.LastMonth, 1_050, units: 1_000)
                ],
                accounts: [A, C]);

            var warning = Assert.Single(Build(data).PriceWarnings);

            Assert.Equal(0.05m, warning.Dispersion);
        }

        [Fact]
        public void A_small_position_is_not_flagged_for_price()
        {
            var data = Data(
                [
                    Holding(1, Q3.LastMonth, 100, units: 100),
                    Holding(3, Q3.LastMonth, 105, units: 100)
                ],
                accounts: [A, C]);

            Assert.Empty(Build(data).PriceWarnings);
        }

        [Fact]
        public void The_composite_benchmark_uses_the_group_rates_and_inflation_plus_margin_for_alternatives()
        {
            FundPolicyLine[] policy =
            [
                new() { AssetClass = FundAssetClass.EquityVfex, Target = 0.5m },
                new() { AssetClass = FundAssetClass.Offshore, Target = 0.5m }
            ];

            var market = new FundMarketQuarter { Year = 2026, Quarter = 3, VfexAllShare = 0.10m, UsdInflation = 0.01m };

            Assert.Equal(0.065m, FundReportBuilder.Benchmark(policy, market, Options));
        }

        [Fact]
        public void A_longer_period_is_not_shown_if_an_earlier_quarter_has_no_figures()
        {
            var data = Data([Holding(1, Q3.OpeningMonth, 100), Holding(1, Q3.LastMonth, 110)]);

            var report = Build(data);

            Assert.Equal(0.10m, report.Performance[0].FundReturn);
            Assert.Null(report.Performance[1].FundReturn);
        }

        [Fact]
        public void An_earlier_quarter_that_fails_its_checks_does_not_feed_the_year_to_date()
        {
            var q2 = Q3.Previous;

            var positions = new[]
            {
                Holding(1, q2.OpeningMonth, 100),
                Holding(1, q2.LastMonth, 100),
                Holding(1, Q3.OpeningMonth, 100),
                Holding(1, Q3.LastMonth, 110)
            };

            var data = Data(positions);

            Assert.Null(FundReportBuilder.ProvenReturn(data, q2, Options));
        }

        [Fact]
        public void Contributions_show_what_they_leave_unexplained_when_there_are_flows()
        {
            var data = Data(
                [
                    Holding(1, Q3.OpeningMonth, 100_000), Holding(1, Q3.LastMonth, 160_000),
                    Holding(3, Q3.OpeningMonth, 100_000), Holding(3, Q3.LastMonth, 110_000)
                ],
                [Flow(1, new DateTime(2026, 7, 2), 50_000, true)],
                [A, C]);

            var report = Build(data);

            Assert.NotEqual(0m, report.ContributionDifference);
        }
    }
}
