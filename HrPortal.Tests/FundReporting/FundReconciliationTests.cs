using HrPortal.Entities;
using HrPortal.Services.FundReporting;

namespace HrPortal.Tests.FundReporting
{
    public class FundReconciliationTests
    {
        private static readonly FundQuarter Q3 = new(2026, 3);
        private static readonly FundReportingOptions Options = new();

        private static readonly FundAccount Account = new() { Id = 1, Manager = "Datvest", AccountNumber = "A1" };

        private static readonly Dictionary<string, FundSecurity> Securities = new()
        {
            ["S1"] = new() { SecurityCode = "S1", Name = "Share", AssetClass = FundAssetClass.EquityVfex, Confirmed = true }
        };

        private static FundPosition Holding(DateTime month, decimal value, string currency = "USD", string code = "S1") =>
            new() { FundAccountId = 1, Month = month, SecurityCode = code, Currency = currency, MarketValue = value };

        private static FundStatementBalance Total(DateTime month, decimal printed, string currency = "USD") =>
            new() { FundAccountId = 1, Month = month, Currency = currency, PrintedTotal = printed };

        /// <summary>A quarter that passes: the same holding at the same value each month.</summary>
        private static (List<FundPosition> Positions, List<FundStatementBalance> Balances) Clean()
        {
            var months = Q3.Months.Prepend(Q3.OpeningMonth).ToList();

            return (months.Select(m => Holding(m, 100)).ToList(), months.Select(m => Total(m, 100)).ToList());
        }

        private static FundDataSet Data(
            IEnumerable<FundPosition> positions,
            IEnumerable<FundStatementBalance> balances,
            IEnumerable<FundTransaction>? transactions = null,
            FundAccount? account = null,
            Dictionary<DateTime, decimal>? rates = null,
            Dictionary<string, FundSecurity>? securities = null) =>
            new()
            {
                Accounts = [account ?? Account],
                Positions = positions.ToList(),
                Balances = balances.ToList(),
                Transactions = (transactions ?? []).ToList(),
                Securities = securities ?? Securities,
                Rates = rates ?? new()
            };

        private static IReadOnlyList<FundFinding> Run(FundDataSet data) => FundReconciliation.Run(data, Q3, Options);

        [Fact]
        public void A_clean_quarter_is_ready()
        {
            var (positions, balances) = Clean();

            Assert.Empty(Run(Data(positions, balances)));
        }

        [Fact]
        public void Check_1_rows_that_do_not_reach_the_printed_total_block()
        {
            var (positions, balances) = Clean();

            balances[2].PrintedTotal = 100.06m;

            var finding = Assert.Single(Run(Data(positions, balances)));

            Assert.Equal("1 Statement totals", finding.Check);
            Assert.Equal(FundFindingSeverity.Block, finding.Severity);
        }

        [Fact]
        public void Check_1_a_difference_within_tolerance_passes()
        {
            var (positions, balances) = Clean();

            balances[2].PrintedTotal = 100.05m;

            Assert.Empty(Run(Data(positions, balances)));
        }

        [Fact]
        public void Check_2_cash_that_does_not_reconcile_blocks()
        {
            var (positions, balances) = Clean();

            balances[1].OpeningCash = 10;
            balances[1].ClosingCash = 20;

            var transactions = new[]
            {
                new FundTransaction { FundAccountId = 1, Month = Q3.Start, Currency = "USD", LedgerDelta = 9, SourceLabel = "Dividend", Treatment = FundReturnTreatment.Income }
            };

            Assert.Contains(Run(Data(positions, balances, transactions)), f => f.Check == "2 Cash in month");
        }

        [Fact]
        public void Check_3_a_balance_that_does_not_carry_over_blocks()
        {
            var (positions, balances) = Clean();

            balances[1].OpeningCash = 10;
            balances[1].ClosingCash = 10;
            balances[2].OpeningCash = 12;
            balances[2].ClosingCash = 12;

            Assert.Contains(Run(Data(positions, balances)), f => f.Check == "3 Cash continuity");
        }

        [Fact]
        public void Check_4_a_big_monthly_move_is_flagged_without_blocking()
        {
            var (positions, balances) = Clean();

            positions[2].MarketValue = 150;
            positions[3].MarketValue = 150;
            balances[2].PrintedTotal = 150;
            balances[3].PrintedTotal = 150;

            var findings = Run(Data(positions, balances));

            Assert.Contains(findings, f => f.Check == "4 Value bridge" && f.Severity == FundFindingSeverity.Check);
            Assert.True(FundReconciliation.IsReady(findings));
        }

        [Fact]
        public void Check_5_an_unclassified_transaction_blocks()
        {
            var (positions, balances) = Clean();

            balances[1].OpeningCash = 0;
            balances[1].ClosingCash = 5;

            var transactions = new[]
            {
                new FundTransaction { FundAccountId = 1, Month = Q3.Start, Currency = "USD", LedgerDelta = 5, SourceLabel = "Journal", Treatment = FundReturnTreatment.Review }
            };

            var finding = Assert.Single(Run(Data(positions, balances, transactions)));

            Assert.Equal("5 Unclassified", finding.Check);
        }

        [Fact]
        public void Check_7_holdings_that_vanish_block_unless_the_account_is_closed()
        {
            var (positions, balances) = Clean();

            positions.RemoveAll(p => p.Month == Q3.LastMonth);
            balances.RemoveAll(b => b.Month == Q3.LastMonth);

            Assert.Contains(Run(Data(positions, balances)), f => f.Check == "7 Missing mandate");

            var closed = new FundAccount { Id = 1, Manager = "Datvest", AccountNumber = "A1", ClosedFromMonth = Q3.LastMonth };

            Assert.Empty(Run(Data(positions, balances, account: closed)));
        }

        [Fact]
        public void Holdings_with_no_opening_statement_block_so_their_value_is_not_read_as_a_gain()
        {
            var (positions, balances) = Clean();

            positions.RemoveAll(p => p.Month == Q3.OpeningMonth);
            balances.RemoveAll(b => b.Month == Q3.OpeningMonth);

            Assert.Contains(Run(Data(positions, balances)), f => f.Check == "Opening values");
        }

        [Fact]
        public void A_new_mandate_funded_in_the_month_needs_no_opening_statement()
        {
            var (positions, balances) = Clean();

            positions.RemoveAll(p => p.Month == Q3.OpeningMonth);
            balances.RemoveAll(b => b.Month == Q3.OpeningMonth);
            balances[0].OpeningCash = 0;
            balances[0].ClosingCash = 0;

            var transactions = new[]
            {
                new FundTransaction { FundAccountId = 1, Month = Q3.Start, Currency = "USD", NetAmount = 100, CashIn = true, Treatment = FundReturnTreatment.ExternalFlow, SourceLabel = "Capital in" }
            };

            Assert.DoesNotContain(Run(Data(positions, balances, transactions)), f => f.Check == "Opening values");
        }

        [Fact]
        public void A_missing_zwg_rate_blocks_and_is_never_assumed()
        {
            var (positions, balances) = Clean();

            positions.ForEach(p => p.Currency = "ZWG");
            balances.ForEach(b => b.Currency = "ZWG");

            var rates = Q3.Months.Prepend(Q3.OpeningMonth).Skip(1).ToDictionary(m => m, _ => 26.8m);

            var finding = Assert.Single(Run(Data(positions, balances, rates: rates)));

            Assert.Equal("Exchange rate", finding.Check);
        }

        [Fact]
        public void A_currency_with_no_rate_table_blocks()
        {
            var (positions, balances) = Clean();

            positions[3].Currency = "ZAR";
            balances[3].Currency = "ZAR";

            Assert.Contains(Run(Data(positions, balances)), f => f.Check == "Exchange rate" && f.Where == "ZAR");
        }

        [Fact]
        public void A_security_nobody_has_confirmed_blocks()
        {
            var (positions, balances) = Clean();

            var securities = new Dictionary<string, FundSecurity>
            {
                ["S1"] = new() { SecurityCode = "S1", Name = "Share", AssetClass = FundAssetClass.EquityVfex, Confirmed = false }
            };

            Assert.Contains(Run(Data(positions, balances, securities: securities)), f => f.Check == "Security class");
        }
    }
}
