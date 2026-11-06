using HrPortal.Entities;

namespace HrPortal.Services.FundReporting
{
    public enum FundFindingSeverity
    {
        /// <summary>Nothing is calculated until it is fixed.</summary>
        Block,

        /// <summary>Someone should look, but it does not stop the quarter.</summary>
        Check
    }

    public sealed record FundFinding(string Check, FundFindingSeverity Severity, DateTime? Month, string Where, string Message);

    /// <summary>
    /// Step 5 of the guide: prove the figures were read completely and
    /// correctly before any calculation. Checks 6 (member flows) and 8 (a PDF
    /// that gave nothing) wait until administrator figures and PDF reading exist.
    /// </summary>
    public static class FundReconciliation
    {
        public static IReadOnlyList<FundFinding> Run(FundDataSet data, FundQuarter quarter, FundReportingOptions options)
        {
            var findings = new List<FundFinding>();

            foreach (var month in quarter.Months)
            {
                foreach (var account in data.Accounts)
                {
                    if (!data.IsClosed(account, month))
                        CheckAccount(data, account, month, options, findings);
                }
            }

            CheckRates(data, quarter, findings);
            CheckClassified(data, quarter, findings);

            return findings
                .OrderBy(f => f.Severity)
                .ThenBy(f => f.Month)
                .ThenBy(f => f.Where)
                .ToList();
        }

        public static bool IsReady(IEnumerable<FundFinding> findings) =>
            findings.All(f => f.Severity != FundFindingSeverity.Block);

        private static void CheckAccount(
            FundDataSet data,
            FundAccount account,
            DateTime month,
            FundReportingOptions options,
            List<FundFinding> findings)
        {
            var where = $"{account.Manager} {account.AccountNumber}";
            var previous = month.AddMonths(-1);

            var positions = data.Positions.Where(p => p.FundAccountId == account.Id && p.Month == month).ToList();
            var transactions = data.Transactions.Where(t => t.FundAccountId == account.Id && t.Month == month).ToList();
            var balances = data.Balances.Where(b => b.FundAccountId == account.Id && b.Month == month).ToList();

            var hadHoldings = data.HasHoldings(account.Id, previous);
            var hasAnything = positions.Count > 0 || transactions.Count > 0 || balances.Count > 0;

            void Add(string check, FundFindingSeverity severity, string message) =>
                findings.Add(new(check, severity, month, where, message));

            // Check 7: a statement missing here would silently understate the fund.
            if (hadHoldings && positions.Count == 0)
            {
                Add("7 Missing mandate", FundFindingSeverity.Block,
                    "Had holdings last month and has none this month. Load the statement, or mark the account closed.");
            }

            if (!hasAnything)
                return;

            // Nothing to open from would make the whole value look like a gain.
            if (positions.Count > 0 && !hadHoldings && !transactions.Any(t => FundDataSet.IsFlow(t) && t.CashIn))
            {
                Add("Opening values", FundFindingSeverity.Block,
                    $"Has holdings but no statement for {previous:MMM yyyy} and no money coming in. Load that month's statement.");
            }

            var currencies = positions.Select(p => p.Currency)
                .Concat(transactions.Select(t => t.Currency))
                .Concat(balances.Select(b => b.Currency))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var currency in currencies)
            {
                var balance = balances.FirstOrDefault(b => Same(b.Currency, currency));
                var rows = positions.Where(p => Same(p.Currency, currency)).ToList();
                var moves = transactions.Where(t => Same(t.Currency, currency)).ToList();

                // Check 1
                if (balance?.PrintedTotal is decimal printed)
                {
                    var read = rows.Sum(p => p.MarketValue);

                    if (Math.Abs(read - printed) > options.StatementTotalTolerance)
                    {
                        Add("1 Statement totals", FundFindingSeverity.Block,
                            $"{currency}: rows read add up to {read:N2}, the statement prints {printed:N2}.");
                    }
                }
                else if (rows.Count > 0)
                {
                    Add("1 Statement totals", FundFindingSeverity.Block,
                        $"{currency}: holdings were read but there is no printed total to check them against.");
                }

                // Check 2
                if (balance is { OpeningCash: decimal opening, ClosingCash: decimal closing })
                {
                    var movement = moves.Sum(t => t.LedgerDelta);

                    if (Math.Abs(opening + movement - closing) > options.CashReconciliationTolerance)
                    {
                        Add("2 Cash in month", FundFindingSeverity.Block,
                            $"{currency}: opening {opening:N2} plus movements {movement:N2} does not reach closing {closing:N2}.");
                    }

                    // Check 3
                    var last = data.Balances.FirstOrDefault(b =>
                        b.FundAccountId == account.Id && b.Month == previous && Same(b.Currency, currency));

                    if (last?.ClosingCash is decimal lastClosing &&
                        Math.Abs(opening - lastClosing) > options.CashContinuityTolerance)
                    {
                        Add("3 Cash continuity", FundFindingSeverity.Block,
                            $"{currency}: opens at {opening:N2}, last month closed at {lastClosing:N2}.");
                    }
                }
                else if (moves.Count > 0)
                {
                    Add("2 Cash in month", FundFindingSeverity.Block,
                        $"{currency}: transactions were read but there is no opening and closing balance to check them against.");
                }
            }

            // Check 4
            var ids = new[] { account.Id };
            var flows = data.Flows(ids, month, month).Sum(data.FlowUsd);
            var implied = FundFormulas.ImpliedReturn(data.Value(ids, previous), data.Value(ids, month), flows);

            if (implied is decimal r && Math.Abs(r) > options.ValueBridgeLimit)
            {
                Add("4 Value bridge", FundFindingSeverity.Check,
                    $"Implied return of {r:P1} in a month: usually a missed flow, a missing holding or a currency error.");
            }

            // Check 5
            foreach (var label in transactions
                .Where(t => t.Treatment == FundReturnTreatment.Review)
                .Select(t => t.SourceLabel)
                .Distinct())
            {
                Add("5 Unclassified", FundFindingSeverity.Block,
                    $"\"{label}\" has no return treatment. An analyst needs to decide it.");
            }
        }

        /// <summary>A missing rate stops the quarter. A rate is never assumed.</summary>
        private static void CheckRates(FundDataSet data, FundQuarter quarter, List<FundFinding> findings)
        {
            var months = quarter.Months.Prepend(quarter.OpeningMonth).ToHashSet();

            var used = data.Positions.Where(p => months.Contains(p.Month)).Select(p => (p.Month, p.Currency))
                .Concat(data.Transactions.Where(t => months.Contains(t.Month)).Select(t => (t.Month, t.Currency)))
                .Distinct();

            foreach (var (month, currency) in used)
            {
                if (data.ToUsd(1, currency, month) == null)
                {
                    findings.Add(new("Exchange rate", FundFindingSeverity.Block, month, currency,
                        $"There is no {currency} rate for {month:MMM yyyy}."));
                }
            }
        }

        private static void CheckClassified(FundDataSet data, FundQuarter quarter, List<FundFinding> findings)
        {
            var months = quarter.Months.Prepend(quarter.OpeningMonth).ToHashSet();

            var codes = data.Positions
                .Where(p => months.Contains(p.Month))
                .Select(p => p.SecurityCode)
                .Distinct();

            foreach (var code in codes)
            {
                if (!data.Securities.TryGetValue(code, out var security) || !security.Confirmed || security.AssetClass == null)
                {
                    findings.Add(new("Security class", FundFindingSeverity.Block, null, code,
                        $"{security?.Name ?? code} needs its asset class confirmed by an analyst."));
                }
            }
        }

        private static bool Same(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
