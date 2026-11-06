using System.Text;
using HrPortal.DbContexts;
using HrPortal.Entities;
using HrPortal.Services.FundReporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HrPortal.Tests.FundReporting
{
    public class FundImportTests
    {
        private const string Positions =
            "Period,Manager,Sub_Acct,Sub_Fund,Security_ID,Security_Name,Asset_Class_Code,Currency,Units_Nominal,Market_Value,Prescribed\n" +
            "Sep-2026,Datvest,A1,Sub A,S1,Delta,ASSET_EQ_ZSE,ZWG,\"1,000\",\"26,800.00\",No\n";

        private const string Transactions =
            "Txn_Date,Period,Manager,Sub_Acct,Currency,Txn_Type,Net_Amount,Cash_Effect,Ledger_Delta,Source_Label\n" +
            "2026-09-15,2026-09,Datvest,A1,USD,Other,500,Cash In,500,Journal\n";

        private const string Balances =
            "Period,Manager,Sub_Acct,Currency,Printed_Total,Opening,Closing\n" +
            "Sep-2026,Datvest,A1,ZWG,26800.00,,\n";

        private static ApplicationDbContext Context()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new ApplicationDbContext(options);

            context.PensionFunds.Add(new PensionFund { Id = 1, Name = "Falcon" });
            context.PensionFunds.Add(new PensionFund { Id = 2, Name = "Other" });
            context.SaveChanges();

            return context;
        }

        private static (string, byte[]) File(string name, string text) => (name, Encoding.UTF8.GetBytes(text));

        private static FundCsvFile Read(string text) =>
            FundCsvReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(text)), "file.csv");

        [Fact]
        public void The_kind_of_file_is_known_from_its_columns()
        {
            Assert.Equal(FundCsvKind.Positions, Read(Positions).Kind);
            Assert.Equal(FundCsvKind.Transactions, Read(Transactions).Kind);
            Assert.Equal(FundCsvKind.CashRecon, Read(Balances).Kind);
            Assert.Null(Read("a,b\n1,2\n").Kind);
        }

        [Fact]
        public void Numbers_read_with_commas_and_brackets()
        {
            var file = Read(
                "Period,Manager,Sub_Acct,Security_ID,Market_Value\n" +
                "2026-09,Datvest,A1,S1,\"(1,250.50)\"\n");

            Assert.Equal(-1250.50m, file.Holdings[0].Value.Position.MarketValue);
        }

        [Fact]
        public void A_bad_row_names_its_line()
        {
            var file = Read(
                "Period,Manager,Sub_Acct,Security_ID,Market_Value\n" +
                "2026-09,Datvest,A1,S1,lots\n");

            Assert.Contains(file.Errors, e => e.Contains("line 2") && e.Contains("Market_Value"));
        }

        [Fact]
        public async Task Importing_creates_accounts_and_proposes_securities_unconfirmed()
        {
            using var context = Context();

            var result = await new FundReportingService(context).ImportAsync(1, [File("p.csv", Positions)], "ana");

            Assert.Empty(result.Errors);

            var account = await context.FundAccounts.SingleAsync();

            Assert.Equal("Sub A", account.SubFund);

            var security = await context.FundSecurities.SingleAsync();

            Assert.False(security.Confirmed);
            Assert.Equal(FundAssetClass.EquityZse, security.AssetClass);
        }

        [Fact]
        public async Task One_bad_file_means_nothing_at_all_is_loaded()
        {
            using var context = Context();

            var result = await new FundReportingService(context).ImportAsync(
                1,
                [File("p.csv", Positions), File("bad.csv", "Period,Manager,Sub_Acct,Security_ID,Market_Value\nnope,Datvest,A1,S1,1\n")],
                "ana");

            Assert.NotEmpty(result.Errors);
            Assert.Equal(0, await context.FundPositions.CountAsync());
            Assert.Equal(0, await context.FundAccounts.CountAsync());
        }

        [Fact]
        public async Task A_reissued_statement_replaces_the_old_one()
        {
            using var context = Context();

            var service = new FundReportingService(context);

            await service.ImportAsync(1, [File("p.csv", Positions)], "ana");
            await service.ImportAsync(1, [File("p2.csv", Positions.Replace("26,800.00", "30,000.00"))], "ana");

            var position = await context.FundPositions.SingleAsync();

            Assert.Equal(30_000m, position.MarketValue);
            Assert.Equal(2, await context.FundStatementImports.CountAsync());
        }

        [Fact]
        public async Task A_file_never_changes_a_security_an_analyst_has_confirmed()
        {
            using var context = Context();

            var service = new FundReportingService(context);

            await service.ImportAsync(1, [File("p.csv", Positions)], "ana");

            var security = await context.FundSecurities.SingleAsync();

            await service.ConfirmSecurityAsync(security.Id, FundAssetClass.UnlistedEquity, true, false, "ana");
            await service.ImportAsync(1, [File("p2.csv", Positions.Replace("Delta", "DELTA CORP"))], "ana");

            Assert.Equal(FundAssetClass.UnlistedEquity, security.AssetClass);
            Assert.True(security.Prescribed);
            Assert.Equal("Delta", security.Name);
        }

        [Fact]
        public async Task An_unknown_label_waits_for_review()
        {
            using var context = Context();

            var service = new FundReportingService(context);

            await service.ImportAsync(1, [File("t.csv", Transactions)], "ana");

            Assert.Equal(FundReturnTreatment.Review, (await context.FundTransactions.SingleAsync()).Treatment);

            var label = Assert.Single(await service.LabelsToDecideAsync());

            Assert.Equal("Journal", label.Label);
        }

        [Fact]
        public async Task A_ruling_applies_to_every_client_of_the_manager_and_to_later_statements()
        {
            using var context = Context();

            var service = new FundReportingService(context);

            await service.ImportAsync(1, [File("t.csv", Transactions)], "ana");
            await service.ImportAsync(2, [File("t.csv", Transactions)], "ana");

            var changed = await service.DecideLabelAsync("Datvest", "Journal", FundReturnTreatment.Income, "ana");

            Assert.Equal(2, changed);
            Assert.Empty(await service.LabelsToDecideAsync());

            await service.ImportAsync(1, [File("t2.csv", Transactions.Replace("500,Cash In,500", "600,Cash In,600"))], "ana");

            Assert.All(await context.FundTransactions.ToListAsync(), t => Assert.Equal(FundReturnTreatment.Income, t.Treatment));
        }

        [Fact]
        public async Task The_same_ruling_does_not_touch_another_manager()
        {
            using var context = Context();

            var service = new FundReportingService(context);

            await service.ImportAsync(1, [File("t.csv", Transactions.Replace("Datvest", "Smartvest"))], "ana");
            await service.DecideLabelAsync("Datvest", "Journal", FundReturnTreatment.Income, "ana");

            Assert.Equal(FundReturnTreatment.Review, (await context.FundTransactions.SingleAsync()).Treatment);
        }

        [Fact]
        public async Task A_loaded_quarter_runs_end_to_end()
        {
            using var context = Context();

            var service = new FundReportingService(context);

            var opening = Positions.Replace("Sep-2026", "Jun-2026").Replace("26,800.00", "26,000.00");
            var july = Positions.Replace("Sep-2026", "Jul-2026");
            var august = Positions.Replace("Sep-2026", "Aug-2026");
            var totals =
                "Period,Manager,Sub_Acct,Currency,Printed_Total\n" +
                "2026-06,Datvest,A1,ZWG,26000\n2026-07,Datvest,A1,ZWG,26800\n2026-08,Datvest,A1,ZWG,26800\n2026-09,Datvest,A1,ZWG,26800\n";

            await service.ImportAsync(1, [File("a.csv", opening), File("b.csv", july), File("c.csv", august), File("d.csv", Positions), File("e.csv", totals)], "ana");

            var quarter = new FundQuarter(2026, 3);
            var options = new FundReportingOptions();

            Assert.False(FundReconciliation.IsReady(FundReconciliation.Run(await service.LoadAsync(1, quarter), quarter, options)));

            await service.ConfirmSecurityAsync((await context.FundSecurities.SingleAsync()).Id, FundAssetClass.EquityZse, false, false, "ana");

            foreach (var month in quarter.Months.Prepend(quarter.OpeningMonth))
                await service.SaveRateAsync(month, 26.8m);

            var data = await service.LoadAsync(1, quarter);

            Assert.True(FundReconciliation.IsReady(FundReconciliation.Run(data, quarter, options)));

            var report = FundReportBuilder.Build(data, quarter, [], [], options);

            Assert.Equal(1_000m, report.FundValue);
            Assert.Equal(Math.Round(800m / 26_000m, 10), Math.Round(report.FundReturn!.Value, 10));
        }

        [Fact]
        public void Companies_other_than_the_consultants_do_not_get_it_unless_they_ask()
        {
            var root = Path.Combine(Path.GetTempPath(), "brand-test-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(root, "Brands"));
            System.IO.File.WriteAllText(Path.Combine(root, "Brands", "other.json"), "{}");

            try
            {
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Brand:Key"] = "other" })
                    .Build();

                Assert.False(HrPortal.Services.Brand.Load(config, root).FundReporting);
                Assert.True(new HrPortal.Services.Brand().FundReporting);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
